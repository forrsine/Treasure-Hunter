using Common;
using Microsoft.Data.SqlClient;

namespace GameServer.Services;

public sealed class DBService : Singleton<DBService>
{
    private string _connectionString = "";

    public void Init()
    {
        Settings.Load();
        _connectionString = Settings.ConnectionString;

        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException("Database connection string is not configured.");
        }
    }

    public SqlConnection OpenConnection()
    {
        var connection = new SqlConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public string? GetDatabaseName()
    {
        using SqlConnection connection = OpenConnection();
        using var command = new SqlCommand("SELECT DB_NAME()", connection);
        return command.ExecuteScalar()?.ToString();
    }

    public void EnsurePlayerCharactersTable(SqlConnection connection, SqlTransaction? transaction = null)
    {
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

    public bool UsernameExists(SqlConnection connection, string username, SqlTransaction? transaction = null)
    {
        using var command = new SqlCommand(
            "SELECT COUNT(1) FROM dbo.Users WHERE Username = @Username",
            connection,
            transaction);

        command.Parameters.AddWithValue("@Username", username);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    public TUser RegisterUser(string username, string passwordHash)
    {
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

    public TCharacter CreateCharacter(long userId, int slotIndex, string name, int classId)
    {
        using SqlConnection connection = OpenConnection();
        EnsurePlayerCharactersTable(connection);
        using SqlTransaction transaction = connection.BeginTransaction();

        try
        {
            using var checkCommand = new SqlCommand(
                """
                SELECT COUNT(1)
                FROM dbo.PlayerCharacters
                WHERE UserId = @UserId AND SlotIndex = @SlotIndex;
                """,
                connection,
                transaction);

            checkCommand.Parameters.AddWithValue("@UserId", userId);
            checkCommand.Parameters.AddWithValue("@SlotIndex", slotIndex);

            if (Convert.ToInt32(checkCommand.ExecuteScalar()) > 0)
            {
                throw new InvalidOperationException("This slot already has a character.");
            }

            using var insertCommand = new SqlCommand(
                """
                INSERT INTO dbo.PlayerCharacters (UserId, SlotIndex, Name, ClassId, Level, Exp)
                OUTPUT INSERTED.Id, INSERTED.UserId, INSERTED.SlotIndex, INSERTED.Name, INSERTED.ClassId, INSERTED.Level, INSERTED.Exp
                VALUES (@UserId, @SlotIndex, @Name, @ClassId, 1, 0);
                """,
                connection,
                transaction);

            insertCommand.Parameters.AddWithValue("@UserId", userId);
            insertCommand.Parameters.AddWithValue("@SlotIndex", slotIndex);
            insertCommand.Parameters.AddWithValue("@Name", name);
            insertCommand.Parameters.AddWithValue("@ClassId", classId);

            TCharacter? character = null;
            using (SqlDataReader reader = insertCommand.ExecuteReader())
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
