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
        // 启动阶段先验证配置并执行幂等结构检查，让权限不足或迁移失败尽早暴露，
        // 不要等到玩家登录后才发现存档表不可用。
        Settings.Load();
        _connectionString = Settings.ConnectionString;

        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException("Database connection string is not configured.");
        }

        using SqlConnection connection = OpenConnection();
        EnsurePlayerCharactersTable(connection);
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
                    PendingAttributeUpgradeCount INT NOT NULL CONSTRAINT DF_PlayerCharacters_PendingAttributeUpgradeCount DEFAULT 0,
                    VaultDestroyedCount INT NOT NULL CONSTRAINT DF_PlayerCharacters_VaultDestroyedCount DEFAULT 0,
                    CompletedBossCount INT NOT NULL CONSTRAINT DF_PlayerCharacters_CompletedBossCount DEFAULT 0,
                    Gold BIGINT NOT NULL CONSTRAINT DF_PlayerCharacters_Gold DEFAULT 0,
                    MerchantIntroCompleted BIT NOT NULL CONSTRAINT DF_PlayerCharacters_MerchantIntroCompleted DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_PlayerCharacters_CreatedAt DEFAULT SYSUTCDATETIME(),
                    UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_PlayerCharacters_UpdatedAt DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_PlayerCharacters_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id),
                    CONSTRAINT UQ_PlayerCharacters_User_Slot UNIQUE (UserId, SlotIndex),
                    CONSTRAINT CK_PlayerCharacters_Slot CHECK (SlotIndex >= 0 AND SlotIndex <= 3),
                    CONSTRAINT CK_PlayerCharacters_Class CHECK (ClassId IN (1, 2, 3,4))
                );
            END

            IF COL_LENGTH(N'dbo.PlayerCharacters', N'PendingAttributeUpgradeCount') IS NULL
            BEGIN
                ALTER TABLE dbo.PlayerCharacters
                ADD PendingAttributeUpgradeCount INT NOT NULL
                    CONSTRAINT DF_PlayerCharacters_PendingAttributeUpgradeCount DEFAULT 0;
            END

            IF COL_LENGTH(N'dbo.PlayerCharacters', N'VaultDestroyedCount') IS NULL
            BEGIN
                ALTER TABLE dbo.PlayerCharacters
                ADD VaultDestroyedCount INT NOT NULL
                    CONSTRAINT DF_PlayerCharacters_VaultDestroyedCount DEFAULT 0;
            END

            IF COL_LENGTH(N'dbo.PlayerCharacters', N'CompletedBossCount') IS NULL
            BEGIN
                ALTER TABLE dbo.PlayerCharacters
                ADD CompletedBossCount INT NOT NULL
                    CONSTRAINT DF_PlayerCharacters_CompletedBossCount DEFAULT 0;
            END

            IF COL_LENGTH(N'dbo.PlayerCharacters', N'Gold') IS NULL
            BEGIN
                ALTER TABLE dbo.PlayerCharacters
                ADD Gold BIGINT NOT NULL CONSTRAINT DF_PlayerCharacters_Gold DEFAULT 0;
            END

            IF COL_LENGTH(N'dbo.PlayerCharacters', N'MerchantIntroCompleted') IS NULL
            BEGIN
                ALTER TABLE dbo.PlayerCharacters
                ADD MerchantIntroCompleted BIT NOT NULL
                    CONSTRAINT DF_PlayerCharacters_MerchantIntroCompleted DEFAULT 0;
            END

            IF OBJECT_ID(N'dbo.CharacterAttributeUpgrades', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.CharacterAttributeUpgrades (
                    CharacterId BIGINT NOT NULL,
                    AttributeType INT NOT NULL,
                    UpgradeCount INT NOT NULL,
                    CONSTRAINT PK_CharacterAttributeUpgrades PRIMARY KEY (CharacterId, AttributeType),
                    CONSTRAINT FK_CharacterAttributeUpgrades_Character
                        FOREIGN KEY (CharacterId) REFERENCES dbo.PlayerCharacters(Id) ON DELETE CASCADE,
                    CONSTRAINT CK_CharacterAttributeUpgrades_Type CHECK (AttributeType >= 1 AND AttributeType <= 8),
                    CONSTRAINT CK_CharacterAttributeUpgrades_Count CHECK (UpgradeCount >= 0)
                );
            END

            IF OBJECT_ID(N'dbo.CharacterInventoryItems', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.CharacterInventoryItems (
                    CharacterId BIGINT NOT NULL,
                    SlotIndex INT NOT NULL,
                    ItemId NVARCHAR(64) NOT NULL,
                    ItemCount INT NOT NULL,
                    CONSTRAINT PK_CharacterInventoryItems PRIMARY KEY (CharacterId, SlotIndex),
                    CONSTRAINT FK_CharacterInventoryItems_Character
                        FOREIGN KEY (CharacterId) REFERENCES dbo.PlayerCharacters(Id) ON DELETE CASCADE,
                    CONSTRAINT CK_CharacterInventoryItems_Slot CHECK (SlotIndex >= 0 AND SlotIndex < 24),
                    CONSTRAINT CK_CharacterInventoryItems_Count CHECK (ItemCount > 0)
                );
            END

            IF OBJECT_ID(N'dbo.CharacterEquippedItems', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.CharacterEquippedItems (
                    CharacterId BIGINT NOT NULL,
                    EquipmentSlot INT NOT NULL,
                    ItemId NVARCHAR(64) NOT NULL,
                    CONSTRAINT PK_CharacterEquippedItems PRIMARY KEY (CharacterId, EquipmentSlot),
                    CONSTRAINT FK_CharacterEquippedItems_Character
                        FOREIGN KEY (CharacterId) REFERENCES dbo.PlayerCharacters(Id) ON DELETE CASCADE,
                    CONSTRAINT CK_CharacterEquippedItems_Slot CHECK (EquipmentSlot >= 1 AND EquipmentSlot <= 6)
                );
            END


            IF OBJECT_ID(N'dbo.CharacterShopPurchases', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.CharacterShopPurchases (
                    CharacterId BIGINT NOT NULL,
                    ItemId NVARCHAR(64) NOT NULL,
                    CONSTRAINT PK_CharacterShopPurchases PRIMARY KEY (CharacterId, ItemId),
                    CONSTRAINT FK_CharacterShopPurchases_Character
                        FOREIGN KEY (CharacterId) REFERENCES dbo.PlayerCharacters(Id) ON DELETE CASCADE
                );
            END

            IF OBJECT_ID(N'dbo.CharacterQuestProgress', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.CharacterQuestProgress (
                    CharacterId BIGINT NOT NULL,
                    QuestId NVARCHAR(64) NOT NULL,
                    QuestState INT NOT NULL,
                    CurrentCount INT NOT NULL,
                    CONSTRAINT PK_CharacterQuestProgress PRIMARY KEY (CharacterId, QuestId),
                    CONSTRAINT FK_CharacterQuestProgress_Character
                        FOREIGN KEY (CharacterId) REFERENCES dbo.PlayerCharacters(Id) ON DELETE CASCADE,
                    CONSTRAINT CK_CharacterQuestProgress_State CHECK (QuestState >= 1 AND QuestState <= 3),
                    CONSTRAINT CK_CharacterQuestProgress_Count CHECK (CurrentCount >= 0)
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
            EnsurePlayerCharactersTable(connection);
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
                        PendingAttributeUpgradeCount = 0,
                        VaultDestroyedCount = 0,
                        CompletedBossCount = 0,
                        Gold = 0,
                        MerchantIntroCompleted = 0,
                        UpdatedAt = SYSUTCDATETIME()
                    OUTPUT INSERTED.Id,
                           INSERTED.UserId,
                           INSERTED.SlotIndex,
                           INSERTED.Name,
                           INSERTED.ClassId,
                           INSERTED.Level,
                           INSERTED.Exp,
                           INSERTED.PendingAttributeUpgradeCount,
                           INSERTED.VaultDestroyedCount,
                           INSERTED.CompletedBossCount,
                           INSERTED.Gold,
                           INSERTED.MerchantIntroCompleted
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
                           INSERTED.Exp,
                           INSERTED.PendingAttributeUpgradeCount,
                           INSERTED.VaultDestroyedCount,
                           INSERTED.CompletedBossCount,
                           INSERTED.Gold,
                           INSERTED.MerchantIntroCompleted
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

            if (character != null)
            {
                using var clearUpgradesCommand = new SqlCommand(
                    "DELETE FROM dbo.CharacterAttributeUpgrades WHERE CharacterId = @CharacterId",
                    connection,
                    transaction);
                clearUpgradesCommand.Parameters.AddWithValue("@CharacterId", character.ID);
                clearUpgradesCommand.ExecuteNonQuery();

                using var clearInventoryCommand = new SqlCommand(
                    "DELETE FROM dbo.CharacterInventoryItems WHERE CharacterId = @CharacterId",
                    connection,
                    transaction);
                clearInventoryCommand.Parameters.AddWithValue("@CharacterId", character.ID);
                clearInventoryCommand.ExecuteNonQuery();

                using var clearEquipmentCommand = new SqlCommand(
                    "DELETE FROM dbo.CharacterEquippedItems WHERE CharacterId = @CharacterId",
                    connection,
                    transaction);
                clearEquipmentCommand.Parameters.AddWithValue("@CharacterId", character.ID);
                clearEquipmentCommand.ExecuteNonQuery();

                using var clearShopPurchasesCommand = new SqlCommand(
                    "DELETE FROM dbo.CharacterShopPurchases WHERE CharacterId = @CharacterId",
                    connection,
                    transaction);
                clearShopPurchasesCommand.Parameters.AddWithValue("@CharacterId", character.ID);
                clearShopPurchasesCommand.ExecuteNonQuery();
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

    /// <summary>
    /// 原子保存角色成长。角色主记录和属性强化子表必须一起成功，避免只保存了一半数据。
    /// </summary>
    public TCharacter SaveCharacterProgress(
        long userId,
        long characterId,
        int level,
        int exp,
        int pendingAttributeUpgradeCount,
        int vaultDestroyedCount,
        int completedBossCount,
        long gold,
        bool merchantIntroCompleted,
        IReadOnlyDictionary<int, int> attributeUpgradeCounts,
        IReadOnlyList<TInventoryItem> inventoryItems,
        IReadOnlyList<TEquippedItem> equippedItems,
        IReadOnlyList<string> purchasedLimitedShopItemIds,
        IReadOnlyList<TQuestProgress> questProgress)
    {
        using SqlConnection connection = OpenConnection();
        EnsurePlayerCharactersTable(connection);
        using SqlTransaction transaction = connection.BeginTransaction();

        try
        {
            using var updateCommand = new SqlCommand(
                """
                UPDATE dbo.PlayerCharacters
                SET Level = @Level,
                    Exp = @Exp,
                    PendingAttributeUpgradeCount = @PendingAttributeUpgradeCount,
                    VaultDestroyedCount = @VaultDestroyedCount,
                    CompletedBossCount = @CompletedBossCount,
                    Gold = @Gold,
                    MerchantIntroCompleted = @MerchantIntroCompleted,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE Id = @CharacterId AND UserId = @UserId;
                """,
                connection,
                transaction);

            updateCommand.Parameters.AddWithValue("@Level", level);
            updateCommand.Parameters.AddWithValue("@Exp", exp);
            updateCommand.Parameters.AddWithValue("@PendingAttributeUpgradeCount", pendingAttributeUpgradeCount);
            updateCommand.Parameters.AddWithValue("@VaultDestroyedCount", vaultDestroyedCount);
            updateCommand.Parameters.AddWithValue("@CompletedBossCount", completedBossCount);
            updateCommand.Parameters.AddWithValue("@Gold", gold);
            updateCommand.Parameters.AddWithValue("@MerchantIntroCompleted", merchantIntroCompleted);
            updateCommand.Parameters.AddWithValue("@CharacterId", characterId);
            updateCommand.Parameters.AddWithValue("@UserId", userId);

            if (updateCommand.ExecuteNonQuery() != 1)
            {
                throw new InvalidOperationException("Character does not belong to the current user.");
            }

            using (var deleteCommand = new SqlCommand(
                       "DELETE FROM dbo.CharacterAttributeUpgrades WHERE CharacterId = @CharacterId",
                       connection,
                       transaction))
            {
                deleteCommand.Parameters.AddWithValue("@CharacterId", characterId);
                deleteCommand.ExecuteNonQuery();
            }

            foreach ((int attributeType, int upgradeCount) in attributeUpgradeCounts)
            {
                if (upgradeCount <= 0)
                {
                    continue;
                }

                using var insertCommand = new SqlCommand(
                    """
                    INSERT INTO dbo.CharacterAttributeUpgrades (CharacterId, AttributeType, UpgradeCount)
                    VALUES (@CharacterId, @AttributeType, @UpgradeCount);
                    """,
                    connection,
                    transaction);
                insertCommand.Parameters.AddWithValue("@CharacterId", characterId);
                insertCommand.Parameters.AddWithValue("@AttributeType", attributeType);
                insertCommand.Parameters.AddWithValue("@UpgradeCount", upgradeCount);
                insertCommand.ExecuteNonQuery();
            }

            using (var deleteInventoryCommand = new SqlCommand(
                       "DELETE FROM dbo.CharacterInventoryItems WHERE CharacterId = @CharacterId",
                       connection,
                       transaction))
            {
                deleteInventoryCommand.Parameters.AddWithValue("@CharacterId", characterId);
                deleteInventoryCommand.ExecuteNonQuery();
            }

            foreach (TInventoryItem item in inventoryItems)
            {
                using var insertInventoryCommand = new SqlCommand(
                    """
                    INSERT INTO dbo.CharacterInventoryItems (CharacterId, SlotIndex, ItemId, ItemCount)
                    VALUES (@CharacterId, @SlotIndex, @ItemId, @ItemCount);
                    """,
                    connection,
                    transaction);
                insertInventoryCommand.Parameters.AddWithValue("@CharacterId", characterId);
                insertInventoryCommand.Parameters.AddWithValue("@SlotIndex", item.SlotIndex);
                insertInventoryCommand.Parameters.AddWithValue("@ItemId", item.ItemId);
                insertInventoryCommand.Parameters.AddWithValue("@ItemCount", item.Count);
                insertInventoryCommand.ExecuteNonQuery();
            }

            using (var deleteEquipmentCommand = new SqlCommand(
                       "DELETE FROM dbo.CharacterEquippedItems WHERE CharacterId = @CharacterId",
                       connection,
                       transaction))
            {
                deleteEquipmentCommand.Parameters.AddWithValue("@CharacterId", characterId);
                deleteEquipmentCommand.ExecuteNonQuery();
            }

            foreach (TEquippedItem item in equippedItems)
            {
                using var insertEquipmentCommand = new SqlCommand(
                    "INSERT INTO dbo.CharacterEquippedItems (CharacterId, EquipmentSlot, ItemId) VALUES (@CharacterId, @EquipmentSlot, @ItemId);",
                    connection,
                    transaction);
                insertEquipmentCommand.Parameters.AddWithValue("@CharacterId", characterId);
                insertEquipmentCommand.Parameters.AddWithValue("@EquipmentSlot", item.EquipmentSlot);
                insertEquipmentCommand.Parameters.AddWithValue("@ItemId", item.ItemId);
                insertEquipmentCommand.ExecuteNonQuery();
            }

            using (var deleteShopPurchasesCommand = new SqlCommand(
                       "DELETE FROM dbo.CharacterShopPurchases WHERE CharacterId = @CharacterId",
                       connection,
                       transaction))
            {
                deleteShopPurchasesCommand.Parameters.AddWithValue("@CharacterId", characterId);
                deleteShopPurchasesCommand.ExecuteNonQuery();
            }

            foreach (string itemId in purchasedLimitedShopItemIds)
            {
                using var insertShopPurchaseCommand = new SqlCommand(
                    "INSERT INTO dbo.CharacterShopPurchases (CharacterId, ItemId) VALUES (@CharacterId, @ItemId);",
                    connection,
                    transaction);
                insertShopPurchaseCommand.Parameters.AddWithValue("@CharacterId", characterId);
                insertShopPurchaseCommand.Parameters.AddWithValue("@ItemId", itemId);
                insertShopPurchaseCommand.ExecuteNonQuery();
            }

            using (var deleteQuestCommand = new SqlCommand(
                       "DELETE FROM dbo.CharacterQuestProgress WHERE CharacterId = @CharacterId",
                       connection,
                       transaction))
            {
                deleteQuestCommand.Parameters.AddWithValue("@CharacterId", characterId);
                deleteQuestCommand.ExecuteNonQuery();
            }

            foreach (TQuestProgress progress in questProgress)
            {
                using var insertQuestCommand = new SqlCommand(
                    "INSERT INTO dbo.CharacterQuestProgress (CharacterId, QuestId, QuestState, CurrentCount) VALUES (@CharacterId, @QuestId, @QuestState, @CurrentCount);",
                    connection,
                    transaction);
                insertQuestCommand.Parameters.AddWithValue("@CharacterId", characterId);
                insertQuestCommand.Parameters.AddWithValue("@QuestId", progress.QuestId);
                insertQuestCommand.Parameters.AddWithValue("@QuestState", progress.State);
                insertQuestCommand.Parameters.AddWithValue("@CurrentCount", progress.CurrentCount);
                insertQuestCommand.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        List<TCharacter> characters = LoadCharacters(userId);
        TCharacter? savedCharacter = characters.Find(character => character.ID == characterId);
        return savedCharacter ?? throw new InvalidOperationException("Saved character could not be reloaded.");
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

        using (var command = new SqlCommand(
                   """
                   SELECT Id, UserId, SlotIndex, Name, ClassId, Level, Exp,
                           PendingAttributeUpgradeCount, VaultDestroyedCount, CompletedBossCount,
                           Gold, MerchantIntroCompleted
                   FROM dbo.PlayerCharacters
                   WHERE UserId = @UserId
                   ORDER BY SlotIndex;
                   """,
                   connection))
        {
            command.Parameters.AddWithValue("@UserId", userId);

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                characters.Add(ReadCharacter(reader));
            }
        }

        LoadAttributeUpgrades(connection, userId, characters);
        LoadInventoryItems(connection, userId, characters);
        LoadEquippedItems(connection, userId, characters);
        LoadShopPurchases(connection, userId, characters);
        LoadQuestProgress(connection, userId, characters);

        return characters;
    }

    private static void LoadAttributeUpgrades(
        SqlConnection connection,
        long userId,
        List<TCharacter> characters)
    {
        if (characters.Count == 0)
        {
            return;
        }

        Dictionary<long, TCharacter> charactersById = characters.ToDictionary(character => character.ID);
        using var command = new SqlCommand(
            """
            SELECT upgrades.CharacterId, upgrades.AttributeType, upgrades.UpgradeCount
            FROM dbo.CharacterAttributeUpgrades AS upgrades
            INNER JOIN dbo.PlayerCharacters AS characters ON characters.Id = upgrades.CharacterId
            WHERE characters.UserId = @UserId;
            """,
            connection);
        command.Parameters.AddWithValue("@UserId", userId);

        using SqlDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            long characterId = reader.GetInt64(0);
            if (charactersById.TryGetValue(characterId, out TCharacter? character))
            {
                character.AttributeUpgradeCounts[reader.GetInt32(1)] = reader.GetInt32(2);
            }
        }
    }

    /// <summary>批量读取账号下全部角色背包，避免角色列表中的每个角色各发一次查询。</summary>
    private static void LoadInventoryItems(
        SqlConnection connection,
        long userId,
        List<TCharacter> characters)
    {
        if (characters.Count == 0)
        {
            return;
        }

        Dictionary<long, TCharacter> charactersById = characters.ToDictionary(character => character.ID);
        using var command = new SqlCommand(
            """
            SELECT inventory.CharacterId, inventory.SlotIndex, inventory.ItemId, inventory.ItemCount
            FROM dbo.CharacterInventoryItems AS inventory
            INNER JOIN dbo.PlayerCharacters AS characters ON characters.Id = inventory.CharacterId
            WHERE characters.UserId = @UserId
            ORDER BY inventory.CharacterId, inventory.SlotIndex;
            """,
            connection);
        command.Parameters.AddWithValue("@UserId", userId);

        using SqlDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            long characterId = reader.GetInt64(0);
            if (charactersById.TryGetValue(characterId, out TCharacter? character))
            {
                character.InventoryItems.Add(new TInventoryItem
                {
                    SlotIndex = reader.GetInt32(1),
                    ItemId = reader.GetString(2),
                    Count = reader.GetInt32(3)
                });
            }
        }
    }

    private static void LoadEquippedItems(SqlConnection connection, long userId, List<TCharacter> characters)
    {
        if (characters.Count == 0)
        {
            return;
        }

        Dictionary<long, TCharacter> charactersById = characters.ToDictionary(character => character.ID);
        using var command = new SqlCommand(
            "SELECT equipped.CharacterId, equipped.EquipmentSlot, equipped.ItemId FROM dbo.CharacterEquippedItems AS equipped INNER JOIN dbo.PlayerCharacters AS characters ON characters.Id = equipped.CharacterId WHERE characters.UserId = @UserId ORDER BY equipped.CharacterId, equipped.EquipmentSlot;",
            connection);
        command.Parameters.AddWithValue("@UserId", userId);
        using SqlDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            long characterId = reader.GetInt64(0);
            if (charactersById.TryGetValue(characterId, out TCharacter? character))
            {
                character.EquippedItems.Add(new TEquippedItem
                {
                    EquipmentSlot = reader.GetInt32(1),
                    ItemId = reader.GetString(2)
                });
            }
        }
    }

    private static void LoadShopPurchases(SqlConnection connection, long userId, List<TCharacter> characters)
    {
        if (characters.Count == 0)
        {
            return;
        }

        Dictionary<long, TCharacter> charactersById = characters.ToDictionary(character => character.ID);
        using var command = new SqlCommand(
            "SELECT purchases.CharacterId, purchases.ItemId FROM dbo.CharacterShopPurchases AS purchases INNER JOIN dbo.PlayerCharacters AS characters ON characters.Id = purchases.CharacterId WHERE characters.UserId = @UserId ORDER BY purchases.CharacterId, purchases.ItemId;",
            connection);
        command.Parameters.AddWithValue("@UserId", userId);
        using SqlDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            long characterId = reader.GetInt64(0);
            if (charactersById.TryGetValue(characterId, out TCharacter? character))
            {
                character.PurchasedLimitedShopItemIds.Add(reader.GetString(1));
            }
        }
    }

    private static void LoadQuestProgress(SqlConnection connection, long userId, List<TCharacter> characters)
    {
        if (characters.Count == 0)
        {
            return;
        }

        Dictionary<long, TCharacter> charactersById = characters.ToDictionary(character => character.ID);
        using var command = new SqlCommand(
            "SELECT progress.CharacterId, progress.QuestId, progress.QuestState, progress.CurrentCount FROM dbo.CharacterQuestProgress AS progress INNER JOIN dbo.PlayerCharacters AS characters ON characters.Id = progress.CharacterId WHERE characters.UserId = @UserId ORDER BY progress.CharacterId, progress.QuestId;",
            connection);
        command.Parameters.AddWithValue("@UserId", userId);
        using SqlDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            long characterId = reader.GetInt64(0);
            if (charactersById.TryGetValue(characterId, out TCharacter? character))
            {
                character.QuestProgress.Add(new TQuestProgress
                {
                    QuestId = reader.GetString(1),
                    State = reader.GetInt32(2),
                    CurrentCount = reader.GetInt32(3)
                });
            }
        }
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
            PendingAttributeUpgradeCount = reader.GetInt32(7),
            VaultDestroyedCount = reader.GetInt32(8),
            CompletedBossCount = reader.GetInt32(9),
            Gold = reader.GetInt64(10),
            MerchantIntroCompleted = reader.GetBoolean(11),
            TID = classId,
            MapID = 1
        };
    }
}
