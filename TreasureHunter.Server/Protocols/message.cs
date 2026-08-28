using ProtoBuf;

namespace SkillBridge.Message;

/// <summary>网络传输最外层消息，同一包通常只携带 Request 或 Response。</summary>
[ProtoContract]
public sealed class NetMessage
{
    [ProtoMember(1)]
    public NetMessageRequest? Request { get; set; }

    [ProtoMember(2)]
    public NetMessageResponse? Response { get; set; }
}

/// <summary>客户端请求集合，由具体非空字段决定业务类型。</summary>
[ProtoContract]
public sealed class NetMessageRequest
{
    [ProtoMember(1)]
    public UserRegisterRequest? userRegister { get; set; }

    [ProtoMember(2)]
    public UserLoginRequest? userLogin { get; set; }

    [ProtoMember(3)]
    public UserCreateCharacterRequest? createChar { get; set; }

    [ProtoMember(4)]
    public UserGameEnterRequest? gameEnter { get; set; }

    [ProtoMember(5)]
    public UserGameLeaveRequest? gameLeave { get; set; }

    [ProtoMember(6)]
    public UserSaveCharacterProgressRequest? saveCharacterProgress { get; set; }
}

/// <summary>服务端响应集合，ProtoMember 编号必须与客户端协议严格一致。</summary>
[ProtoContract]
public sealed class NetMessageResponse
{
    [ProtoMember(1)]
    public UserRegisterResponse? userRegister { get; set; }

    [ProtoMember(2)]
    public UserLoginResponse? userLogin { get; set; }

    [ProtoMember(3)]
    public UserCreateCharacterResponse? createChar { get; set; }

    [ProtoMember(4)]
    public UserGameEnterResponse? gameEnter { get; set; }

    [ProtoMember(5)]
    public UserGameLeaveResponse? gameLeave { get; set; }

    [ProtoMember(6)]
    public UserSaveCharacterProgressResponse? saveCharacterProgress { get; set; }
}

/// <summary>注册账号请求，密码不得写入日志。</summary>
[ProtoContract]
public sealed class UserRegisterRequest
{
    [ProtoMember(1, Name = "user")]
    public string User { get; set; } = "";

    [ProtoMember(2, Name = "password")]
    public string Password { get; set; } = "";
}

/// <summary>注册处理结果。</summary>
[ProtoContract]
public sealed class UserRegisterResponse
{
    [ProtoMember(1, Name = "result")]
    public Result Result { get; set; }

    [ProtoMember(2, Name = "errormsg")]
    public string Errormsg { get; set; } = "";
}

/// <summary>账号密码登录请求。</summary>
[ProtoContract]
public sealed class UserLoginRequest
{
    [ProtoMember(1, Name = "user")]
    public string User { get; set; } = "";

    [ProtoMember(2, Name = "password")]
    public string Password { get; set; } = "";
}

/// <summary>登录结果及用户角色数据。</summary>
[ProtoContract]
public sealed class UserLoginResponse
{
    [ProtoMember(1, Name = "result")]
    public Result Result { get; set; }

    [ProtoMember(2, Name = "errormsg")]
    public string Errormsg { get; set; } = "";

    [ProtoMember(3, Name = "userinfo")]
    public NUserInfo? Userinfo { get; set; }
}

/// <summary>在指定槽位创建职业角色的请求。</summary>
[ProtoContract]
public sealed class UserCreateCharacterRequest
{
    [ProtoMember(1, Name = "name")]
    public string Name { get; set; } = "";

    [ProtoMember(2, Name = "class")]
    public CharacterClass Class { get; set; }

    [ProtoMember(3, Name = "slot")]
    public int SlotIndex { get; set; }
}

/// <summary>创建角色结果与最新角色列表。</summary>
[ProtoContract]
public sealed class UserCreateCharacterResponse
{
    [ProtoMember(1, Name = "result")]
    public Result Result { get; set; }

    [ProtoMember(2, Name = "errormsg")]
    public string Errormsg { get; set; } = "";

    [ProtoMember(3, Name = "characters")]
    public List<NCharacterInfo> Characters { get; } = new();
}

/// <summary>选择账号角色列表中的下标进入游戏。</summary>
[ProtoContract]
public sealed class UserGameEnterRequest
{
    [ProtoMember(1, Name = "characterIdx")]
    public int characterIdx { get; set; }

    [ProtoMember(2, Name = "character_id")]
    public int CharacterId { get; set; }
}

/// <summary>进入游戏结果和角色同步数据。</summary>
[ProtoContract]
public sealed class UserGameEnterResponse
{
    [ProtoMember(1, Name = "result")]
    public Result Result { get; set; }

    [ProtoMember(2, Name = "errormsg")]
    public string Errormsg { get; set; } = "";

    [ProtoMember(3, Name = "character")]
    public NCharacterInfo? Character { get; set; }
}

/// <summary>离开当前游戏角色请求。</summary>
[ProtoContract]
public sealed class UserGameLeaveRequest
{
}

/// <summary>离开游戏处理结果。</summary>
[ProtoContract]
public sealed class UserGameLeaveResponse
{
    [ProtoMember(1, Name = "result")]
    public Result Result { get; set; }

    [ProtoMember(2, Name = "errormsg")]
    public string Errormsg { get; set; } = "";
}

/// <summary>保存当前 Session 已进入角色的长期成长数据。</summary>
[ProtoContract]
public sealed class UserSaveCharacterProgressRequest
{
    [ProtoMember(1, Name = "level")]
    public int Level { get; set; }

    [ProtoMember(2, Name = "exp")]
    public int Exp { get; set; }

    [ProtoMember(3, Name = "pending_attribute_upgrade_count")]
    public int PendingAttributeUpgradeCount { get; set; }

    [ProtoMember(4, Name = "vault_destroyed_count")]
    public int VaultDestroyedCount { get; set; }

    [ProtoMember(5, Name = "completed_boss_count")]
    public int CompletedBossCount { get; set; }

    [ProtoMember(6, Name = "attribute_upgrades")]
    public List<NAttributeUpgradeInfo> AttributeUpgrades { get; } = new();

    [ProtoMember(7, Name = "reset_after_death")]
    public bool ResetAfterDeath { get; set; }

    [ProtoMember(8, Name = "inventory_items")]
    public List<NInventoryItemInfo> InventoryItems { get; } = new();

    [ProtoMember(9, Name = "equipped_items")]
    public List<NEquippedItemInfo> EquippedItems { get; } = new();

    [ProtoMember(10, Name = "gold")]
    public long Gold { get; set; }

    [ProtoMember(11, Name = "merchant_intro_completed")]
    public bool MerchantIntroCompleted { get; set; }

    [ProtoMember(12, Name = "purchased_limited_shop_item_ids")]
    public List<string> PurchasedLimitedShopItemIds { get; } = new();

    [ProtoMember(13, Name = "quest_progress")]
    public List<NQuestProgressInfo> QuestProgress { get; } = new();
}

/// <summary>成长保存结果以及数据库确认后的角色数据。</summary>
[ProtoContract]
public sealed class UserSaveCharacterProgressResponse
{
    [ProtoMember(1, Name = "result")]
    public Result Result { get; set; }

    [ProtoMember(2, Name = "errormsg")]
    public string Errormsg { get; set; } = "";

    [ProtoMember(3, Name = "character")]
    public NCharacterInfo? Character { get; set; }
}

/// <summary>用户网络数据。</summary>
[ProtoContract]
public sealed class NUserInfo
{
    [ProtoMember(1, Name = "id")]
    public int Id { get; set; }

    [ProtoMember(2, Name = "player")]
    public NPlayerInfo? Player { get; set; }
}

/// <summary>玩家账号和其角色集合。</summary>
[ProtoContract]
public sealed class NPlayerInfo
{
    [ProtoMember(1, Name = "id")]
    public int Id { get; set; }

    [ProtoMember(2, Name = "characters")]
    public List<NCharacterInfo> Characters { get; } = new();
}

/// <summary>客户端与服务端共享的角色基础同步数据。</summary>
[ProtoContract]
public sealed class NCharacterInfo
{
    [ProtoMember(1, Name = "id")]
    public int Id { get; set; }

    [ProtoMember(2, Name = "config_id")]
    public int ConfigId { get; set; }

    [ProtoMember(3, Name = "entity_id")]
    public int EntityId { get; set; }

    [ProtoMember(4, Name = "name")]
    public string Name { get; set; } = "";

    [ProtoMember(5, Name = "type")]
    public CharacterType Type { get; set; }

    [ProtoMember(6, Name = "class")]
    public CharacterClass Class { get; set; }

    [ProtoMember(7, Name = "level")]
    public int Level { get; set; }

    [ProtoMember(8)]
    public int mapId { get; set; }

    [ProtoMember(9, Name = "gold")]
    public long Gold { get; set; }

    [ProtoMember(10, Name = "slot_index")]
    public int SlotIndex { get; set; }

    [ProtoMember(11, Name = "exp")]
    public int Exp { get; set; }

    [ProtoMember(12, Name = "pending_attribute_upgrade_count")]
    public int PendingAttributeUpgradeCount { get; set; }

    [ProtoMember(13, Name = "vault_destroyed_count")]
    public int VaultDestroyedCount { get; set; }

    [ProtoMember(14, Name = "completed_boss_count")]
    public int CompletedBossCount { get; set; }

    [ProtoMember(15, Name = "attribute_upgrades")]
    public List<NAttributeUpgradeInfo> AttributeUpgrades { get; } = new();

    [ProtoMember(16, Name = "inventory_items")]
    public List<NInventoryItemInfo> InventoryItems { get; } = new();

    [ProtoMember(17, Name = "equipped_items")]
    public List<NEquippedItemInfo> EquippedItems { get; } = new();

    [ProtoMember(18, Name = "merchant_intro_completed")]
    public bool MerchantIntroCompleted { get; set; }

    [ProtoMember(19, Name = "purchased_limited_shop_item_ids")]
    public List<string> PurchasedLimitedShopItemIds { get; } = new();

    [ProtoMember(20, Name = "quest_progress")]
    public List<NQuestProgressInfo> QuestProgress { get; } = new();
}

[ProtoContract]
public sealed class NQuestProgressInfo
{
    [ProtoMember(1, Name = "quest_id")]
    public string QuestId { get; set; } = "";

    [ProtoMember(2, Name = "state")]
    public int State { get; set; }

    [ProtoMember(3, Name = "current_count")]
    public int CurrentCount { get; set; }
}

[ProtoContract]
public sealed class NEquippedItemInfo
{
    [ProtoMember(1, Name = "equipment_slot")]
    public int EquipmentSlot { get; set; }

    [ProtoMember(2, Name = "item_id")]
    public string ItemId { get; set; } = "";
}

/// <summary>角色背包中一个非空格子的网络数据。</summary>
[ProtoContract]
public sealed class NInventoryItemInfo
{
    [ProtoMember(1, Name = "slot_index")]
    public int SlotIndex { get; set; }

    [ProtoMember(2, Name = "item_id")]
    public string ItemId { get; set; } = "";

    [ProtoMember(3, Name = "count")]
    public int Count { get; set; }
}

/// <summary>某种属性强化的持久化次数。</summary>
[ProtoContract]
public sealed class NAttributeUpgradeInfo
{
    [ProtoMember(1, Name = "attribute_type")]
    public int AttributeType { get; set; }

    [ProtoMember(2, Name = "upgrade_count")]
    public int UpgradeCount { get; set; }
}

/// <summary>通用业务结果。</summary>
[ProtoContract(Name = "RESULT")]
public enum Result
{
    [ProtoEnum(Name = "SUCCESS")]
    Success = 0,

    [ProtoEnum(Name = "FAILED")]
    Failed = 1
}

/// <summary>网络实体类型。</summary>
[ProtoContract(Name = "CHARACTER_TYPE")]
public enum CharacterType
{
    Player = 0,

    [ProtoEnum(Name = "NPC")]
    Npc = 1,

    Monster = 2
}

/// <summary>职业编号，数值同时用于协议、数据库和客户端职业配置。</summary>
[ProtoContract(Name = "CHARACTER_CLASS")]
public enum CharacterClass
{
    [ProtoEnum(Name = "NONE")]
    None = 0,

    [ProtoEnum(Name = "WARRIOR")]
    Warrior = 1,

    [ProtoEnum(Name = "WIZARD")]
    Wizard = 2,

    [ProtoEnum(Name = "ARCHER")]
    Archer = 3,

    [ProtoEnum(Name = "ASSASSIN")]
    Assassin = 4
}
