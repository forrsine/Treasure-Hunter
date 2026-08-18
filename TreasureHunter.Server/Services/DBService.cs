using Common;
using Microsoft.Data.SqlClient;

namespace GameServer.Services;

/// <summary>
/// SQL Server 数据访问服务：集中管理连接创建、账号查询、注册事务和角色存档读写。
/// 本层只处理数据库模型，不直接构造网络响应，避免持久化代码与协议代码耦合。
/// </summary>
public sealed class DBService : Singleton<DBService>
{
    private string _connectionString = "";

    public void Init()
    {
        // 启动阶段只验证配置；每次业务操作按需打开并及时释放独立连接。
        Settings.Load();
        _connectionString = Settings.ConnectionString;

        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException("Database connection string is not configured.");
        }
    }

    /// <summary>
    /// 打开一个新的数据库连接。
    /// 当前服务端采用“短连接按需打开”的方式，简单直观，也方便 using 自动释放。
    /// </summary>
    public SqlConnection OpenConnection()
    {
        var connection = new SqlConnection(_connectionString);
        connection.Open();
        return connection;
    }

    /// <summary>
    /// 查询当前连接到的数据库名称，主要用于本地联调验证配置。
    /// </summary>
    public string? GetDatabaseName()
    {
        using SqlConnection connection = OpenConnection();
        using var command = new SqlCommand("SELECT DB_NAME()", connection);
        return command.ExecuteScalar()?.ToString();
    }

    /// <summary>
    /// 确保角色表存在。
    /// 原型阶段允许服务端启动后自动补表，减少本地搭环境成本；
    /// 正式项目更推荐用数据库迁移脚本统一管理表结构版本。
    /// </summary>
    public void EnsurePlayerCharactersTable(SqlConnection connection, SqlTransaction? transaction = null)
    {
        // 原型阶段自动补表便于运行，正式生产环境更适合使用可追踪版本的数据库迁移脚本。
        using var command = new SqlCommand(
            """
            IF OBJECT_ID(N'dbo.PlayerCharacters', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.PlayerCharacters (
                    Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PlayerCharacters PRIMARY KEY,
                    UserId BIGINT NOT NULL,
                    SlotIndex INT NOT NULL,
                    Name NVARCHAR(32) NOT NULL,
                    ClassId INT NOT NULL,
                    Level INT NOT NULL CONSTRAINT DF_PlayerCharacters_Level DEFAULT 1,
                    Exp INT NOT NULL CONSTRAINT DF_PlayerCharacters_Exp DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_PlayerCharacters_CreatedAt DEFAULT SYSUTCDATETIME(),
                    UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_PlayerCharacters_UpdatedAt DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_PlayerCharacters_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id),
                    CONSTRAINT UQ_PlayerCharacters_User_Slot UNIQUE (UserId, SlotIndex),
                    CONSTRAINT CK_PlayerCharacters_Slot CHECK (SlotIndex >= 0 AND SlotIndex <= 3),
                    CONSTRAINT CK_PlayerCharacters_Class CHECK (ClassId IN (1, 2, 3,4))
                );
            END
            """,
            connection,
            transaction);

        command.ExecuteNonQuery();
    }

    /// <summary>
    /// 根据用户名查找用户，并顺带加载该账号的玩家聚合信息。
    /// </summary>
    public TUser? FindUserByUsername(string username)
    {
        using SqlConnection connection = OpenConnection();
        TUser? user = null;

        using (var command = new SqlCommand(
            """
            SELECT Id, Username, PasswordHash
            FROM dbo.Users
            WHERE Username = @Username
            """,
            connection))
        {
            command.Parameters.AddWithValue("@Username", username);

            using SqlDataReader reader = command.ExecuteReader();
            if (reader.Read())
            {
                user = new TUser
                {
                    ID = reader.GetInt64(0),
                    Username = reader.GetString(1),
                    PasswordHash = reader.GetString(2)
                };
            }
        }

        if (user != null)
        {
            LoadPlayer(connection, user);
        }

        return user;
    }

    /// <summary>
    /// 判断用户名是否已存在。
    /// </summary>
    public bool UsernameExists(SqlConnection connection, string username, SqlTransaction? transaction = null)
    {
        using var command = new SqlCommand(
            "SELECT COUNT(1) FROM dbo.Users WHERE Username = @Username",
            connection,
            transaction);

        command.Parameters.AddWithValue("@Username", username);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    /// <summary>
    /// 注册一个新用户。
    /// 用户表和玩家档案表必须在同一事务里一起成功，否则会产生脏数据。
    /// </summary>
    public TUser RegisterUser(string username, string passwordHash)
    {
        // 用户和玩家基础记录必须同时成功，因此放在同一个事务中提交。
        using SqlConnection connection = OpenConnection();
        using SqlTransaction transaction = connection.BeginTransaction();

        try
        {
            if (UsernameExists(connection, username, transaction))
            {
                throw new InvalidOperationException("Username already exists.");
            }

            using var insertUserCommand = new SqlCommand(
                """
                INSERT INTO dbo.Users (Username, PasswordHash)
                OUTPUT INSERTED.Id
                VALUES (@Username, @PasswordHash);
                """,
                connection,
                transaction);

            insertUserCommand.Parameters.AddWithValue("@Username", username);
            insertUserCommand.Parameters.AddWithValue("@PasswordHash", passwordHash);

            long userId = Convert.ToInt64(insertUserCommand.ExecuteScalar());

            using var insertProfileCommand = new SqlCommand(
                """
                INSERT INTO dbo.PlayerProfiles (UserId)
                OUTPUT INSERTED.UserId, INSERTED.HighScore
                VALUES (@UserId);
                """,
                connection,
                transaction);

            insertProfileCommand.Parameters.AddWithValue("@UserId", userId);

            long playerId = userId;
            int highScore = 0;
            using (SqlDataReader reader = insertProfileCommand.ExecuteReader())
            {
                if (reader.Read())
                {
                    playerId = reader.GetInt64(0);
                    highScore = reader.GetInt32(1);
                }
            }

            transaction.Commit();

            return new TUser
            {
                ID = userId,
                Username = username,
                PasswordHash = passwordHash,
                Player = new TPlayer
                {
                    ID = playerId,
                    UserId = userId,
                    HighScore = highScore
                }
            };
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// 创建或覆盖某个角色槽位的角色。
    /// 这里使用事务和行锁，避免并发请求同时写入同一个槽位。
    /// </summary>
    public TCharacter CreateCharacter(long userId, int slotIndex, string name, int classId)
    {
        // 槽位校验和写入位于同一事务，防止并发请求创建两个相同槽位角色。
        using SqlConnection connection = OpenConnection();
        EnsurePlayerCharactersTable(connection);
        using SqlTransaction transaction = connection.BeginTransaction();

        try
        {
            // 同一个请求既可以创建空槽位，也可以覆盖已有槽位。
            // UPDLOCK + HOLDLOCK 会在当前事务内锁住目标槽位，避免两个并发请求
            // 同时判断槽位为空并重复插入，最终触发唯一索引冲突。
            using var saveCommand = new SqlCommand(
                """
                IF EXISTS (
                    SELECT 1
                    FROM dbo.PlayerCharacters WITH (UPDLOCK, HOLDLOCK)
                    WHERE UserId = @UserId AND SlotIndex = @SlotIndex
                )
                BEGIN
                    UPDATE dbo.PlayerCharacters
                    SET Name = @Name,
                        ClassId = @ClassId,
                        Level = 1,
                        Exp = 0,
                        UpdatedAt = SYSUTCDATETIME()
                    OUTPUT INSERTED.Id,
                           INSERTED.UserId,
                           INSERTED.SlotIndex,
                           INSERTED.Name,
                           INSERTED.ClassId,
                           INSERTED.Level,
                           INSERTED.Exp
                    WHERE UserId = @UserId AND SlotIndex = @SlotIndex;
                END
                ELSE
                BEGIN
                    INSERT INTO dbo.PlayerCharacters
                        (UserId, SlotIndex, Name, ClassId, Level, Exp)
                    OUTPUT INSERTED.Id,
                           INSERTED.UserId,
                           INSERTED.SlotIndex,
                           INSERTED.Name,
                           INSERTED.ClassId,
                           INSERTED.Level,
                           INSERTED.Exp
                    VALUES (@UserId, @SlotIndex, @Name, @ClassId, 1, 0);
                END
                """,
                connection,
                transaction);

            saveCommand.Parameters.AddWithValue("@UserId", userId);
            saveCommand.Parameters.AddWithValue("@SlotIndex", slotIndex);
            saveCommand.Parameters.AddWithValue("@Name", name);
            saveCommand.Parameters.AddWithValue("@ClassId", classId);

            TCharacter? character = null;
            using (SqlDataReader reader = saveCommand.ExecuteReader())
            {
                if (reader.Read())
                {
                    character = ReadCharacter(reader);
                }
            }

            transaction.Commit();

            return character ?? throw new InvalidOperationException("Character create failed.");
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// 读取某个账号下的全部角色列表。
    /// </summary>
    public List<TCharacter> LoadCharacters(long userId)
    {
        using SqlConnection connection = OpenConnection();
        EnsurePlayerCharactersTable(connection);
        return LoadCharacters(connection, userId);
    }

    private static void LoadPlayer(SqlConnection connection, TUser user)
    {
        using (var command = new SqlCommand(
            """
            SELECT UserId, HighScore
            FROM dbo.PlayerProfiles
            WHERE UserId = @UserId
            """,
            connection))
        {
            command.Parameters.AddWithValue("@UserId", user.ID);

            using SqlDataReader reader = command.ExecuteReader();
            if (reader.Read())
            {
                user.Player = new TPlayer
                {
                    ID = reader.GetInt64(0),
                    UserId = reader.GetInt64(0),
                    HighScore = reader.GetInt32(1)
                };
            }
        }

        foreach (TCharacter character in LoadCharacters(connection, user.ID))
        {
            user.Player.Characters.Add(character);
        }
    }

    private static List<TCharacter> LoadCharacters(SqlConnection connection, long userId)
    {
        var characters = new List<TCharacter>();

        using var command = new SqlCommand(
            """
            SELECT Id, UserId, SlotIndex, Name, ClassId, Level, Exp
            FROM dbo.PlayerCharacters
            WHERE UserId = @UserId
            ORDER BY SlotIndex;
            """,
            connection);

        command.Parameters.AddWithValue("@UserId", userId);

        using SqlDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            characters.Add(ReadCharacter(reader));
        }

        return characters;
    }

    private static TCharacter ReadCharacter(SqlDataReader reader)
    {
        // 数据库字段到内存模型的映射集中在一处，表结构变化时只需调整这里。
        long id = reader.GetInt64(0);
        int classId = reader.GetInt32(4);

        return new TCharacter
        {
            ID = id,
            UserId = reader.GetInt64(1),
            SlotIndex = reader.GetInt32(2),
            Name = reader.GetString(3),
            Class = classId,
            Level = reader.GetInt32(5),
            Exp = reader.GetInt32(6),
            TID = classId,
            MapID = 1,
            Gold = 0
        };
    }
}
