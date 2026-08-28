using Common;
using GameServer.Entities;
using GameServer.Managers;
using Network;
using SkillBridge.Message;

namespace GameServer.Services;

/// <summary>
/// 用户与角色业务服务：处理注册、登录、创建角色、进入游戏和离开游戏。
/// 构造函数只完成消息订阅，具体数据访问交给 DBService，在线实体交给 CharacterManager。
/// </summary>
public sealed class UserService : Singleton<UserService>
{
    public UserService()
    {
        // 服务实例在网络启动前创建，因此首个客户端请求到达时处理器已经注册完成。
        MessageDistributer<NetConnection<NetSession>>.Instance.Subscribe<UserLoginRequest>(OnLogin);
        MessageDistributer<NetConnection<NetSession>>.Instance.Subscribe<UserRegisterRequest>(OnRegister);
        MessageDistributer<NetConnection<NetSession>>.Instance.Subscribe<UserCreateCharacterRequest>(OnCreateCharacter);
        MessageDistributer<NetConnection<NetSession>>.Instance.Subscribe<UserGameEnterRequest>(OnGameEnter);
        MessageDistributer<NetConnection<NetSession>>.Instance.Subscribe<UserGameLeaveRequest>(OnGameLeave);
        MessageDistributer<NetConnection<NetSession>>.Instance.Subscribe<UserSaveCharacterProgressRequest>(OnSaveCharacterProgress);
    }

    public void Init()
    {
    }

    /// <summary>
    /// 处理登录请求。
    /// 核心顺序是：查账号 -> 校验密码哈希 -> 把用户挂到 Session -> 返回用户与角色列表。
    /// </summary>
    private void OnLogin(NetConnection<NetSession> sender, UserLoginRequest request)
    {
        // 安全要求：日志只能记录账号和结果，绝不能输出明文密码。
        Log.InfoFormat("UserLoginRequest: User:{0}", request.User);

        sender.Session.Response.userLogin = new UserLoginResponse();

        string username = request.User?.Trim() ?? "";
        string password = request.Password ?? "";

        try
        {
            TUser? user = DBService.Instance.FindUserByUsername(username);
        if (user == null)
        {
            sender.Session.Response.userLogin.Result = Result.Failed;
            sender.Session.Response.userLogin.Errormsg = "用户不存在";
        }
        else if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            sender.Session.Response.userLogin.Result = Result.Failed;
            sender.Session.Response.userLogin.Errormsg = "密码错误";
        }
        else
        {
            sender.Session.User = user;
            sender.Session.Response.userLogin.Result = Result.Success;
            sender.Session.Response.userLogin.Errormsg = "None";
            sender.Session.Response.userLogin.Userinfo = BuildUserInfo(user);
        }
        }
        catch (Exception ex)
        {
            Log.ErrorFormat("User login failed: {0}", ex);
            sender.Session.Response.userLogin.Result = Result.Failed;
            sender.Session.Response.userLogin.Errormsg = "Login failed.";
        }

        sender.SendResponse();
    }

    /// <summary>
    /// 处理注册请求。
    /// 账号长度和密码长度先做基础校验，再进入数据库创建流程。
    /// </summary>
    private void OnRegister(NetConnection<NetSession> sender, UserRegisterRequest request)
    {
        Log.InfoFormat("UserRegisterRequest: User:{0}", request.User);

        sender.Session.Response.userRegister = new UserRegisterResponse();

        string username = request.User?.Trim() ?? "";
        string password = request.Password ?? "";

        if (username.Length < 3 || username.Length > 32)
        {
            sender.Session.Response.userRegister.Result = Result.Failed;
            sender.Session.Response.userRegister.Errormsg = "用户名长度必须是 3-32";
            sender.SendResponse();
            return;
        }

        if (password.Length < 6 || password.Length > 64)
        {
            sender.Session.Response.userRegister.Result = Result.Failed;
            sender.Session.Response.userRegister.Errormsg = "密码长度必须是 6-64";
            sender.SendResponse();
            return;
        }

        try
        {
            TUser? existing = DBService.Instance.FindUserByUsername(username);
            if (existing != null)
            {
                sender.Session.Response.userRegister.Result = Result.Failed;
                sender.Session.Response.userRegister.Errormsg = "用户已存在";
            }
            else
            {
                // 数据库只保存带随机盐的 BCrypt 哈希，即使数据库泄露也不会直接暴露明文密码。
                string passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
                DBService.Instance.RegisterUser(username, passwordHash);

                sender.Session.Response.userRegister.Result = Result.Success;
                sender.Session.Response.userRegister.Errormsg = "None";
            }
        }
        catch (Exception ex)
        {
            Log.ErrorFormat("User register failed: {0}", ex);
            sender.Session.Response.userRegister.Result = Result.Failed;
            sender.Session.Response.userRegister.Errormsg = "注册失败";
        }

        sender.SendResponse();
    }

    /// <summary>
    /// 处理创建角色请求。
    /// 这里先校验登录态、槽位、职业和角色名，再交给 DBService 用事务写入。
    /// </summary>
    private void OnCreateCharacter(NetConnection<NetSession> sender, UserCreateCharacterRequest request)
    {
        Log.InfoFormat("UserCreateCharacterRequest: Name:{0} Class:{1}", request.Name, request.Class);

        sender.Session.Response.createChar = new UserCreateCharacterResponse();

        TUser? user = sender.Session.User;
        if (user == null)
        {
            sender.Session.Response.createChar.Result = Result.Failed;
            sender.Session.Response.createChar.Errormsg = "请先登录";
            sender.SendResponse();
            return;
        }

        string characterName = request.Name?.Trim() ?? "";
        int slotIndex = request.SlotIndex;
        int classId = (int)request.Class;

        if (slotIndex < 0 || slotIndex > 3)
        {
            sender.Session.Response.createChar.Result = Result.Failed;
            sender.Session.Response.createChar.Errormsg = "角色槽位必须是 0-3";
            sender.SendResponse();
            return;
        }

        if (classId < 1 || classId > 4)
        {
            sender.Session.Response.createChar.Result = Result.Failed;
            sender.Session.Response.createChar.Errormsg = "职业不存在";
            sender.SendResponse();
            return;
        }

        if (characterName.Length < 1 || characterName.Length > 32)
        {
            sender.Session.Response.createChar.Result = Result.Failed;
            sender.Session.Response.createChar.Errormsg = "角色名长度必须是 1-32";
            sender.SendResponse();
            return;
        }

        try
        {
            TCharacter character = DBService.Instance.CreateCharacter(user.ID, slotIndex, characterName, classId);
            user.Player.Characters.Clear();
            user.Player.Characters.AddRange(DBService.Instance.LoadCharacters(user.ID));

            sender.Session.Response.createChar.Result = Result.Success;
            sender.Session.Response.createChar.Errormsg = "None";

            foreach (TCharacter dbCharacter in user.Player.Characters)
            {
                sender.Session.Response.createChar.Characters.Add(BuildCharacterInfo(dbCharacter));
            }
        }
        catch (Exception ex)
        {
            Log.ErrorFormat("Create character failed: {0}", ex);
            sender.Session.Response.createChar.Result = Result.Failed;
            sender.Session.Response.createChar.Errormsg = ex.Message;
        }

        sender.SendResponse();
    }

    /// <summary>
    /// 处理进入游戏请求。
    /// 服务端不会直接信任客户端传来的角色 ID，而是只允许从当前 Session 已登录账号的角色列表中选择。
    /// 这是网络安全里很常见的一条原则：客户端发来的只是“意图”，最终对象由服务端自己确认。
    /// </summary>
    private void OnGameEnter(NetConnection<NetSession> sender, UserGameEnterRequest request)
    {
        // 服务端根据已登录 Session 中的角色列表决定进入对象，不接受客户端任意角色 ID。
        TUser? user = sender.Session.User;
        sender.Session.Response.gameEnter = new UserGameEnterResponse();

        if (user == null)
        {
            sender.Session.Response.gameEnter.Result = Result.Failed;
            sender.Session.Response.gameEnter.Errormsg = "请先登录";
            sender.SendResponse();
            return;
        }

        TCharacter? dbCharacter = request.CharacterId > 0
            ? user.Player.Characters.Find(character => character.ID == request.CharacterId)
            : request.characterIdx >= 0 && request.characterIdx < user.Player.Characters.Count
                ? user.Player.Characters[request.characterIdx]
                : null;

        if (dbCharacter == null)
        {
            sender.Session.Response.gameEnter.Result = Result.Failed;
            sender.Session.Response.gameEnter.Errormsg = "角色不存在";
            sender.SendResponse();
            return;
        }

        Log.InfoFormat("UserGameEnterRequest: characterID:{0}:{1}", dbCharacter.ID, dbCharacter.Name);

        if (sender.Session.Character != null)
        {
            CharacterLeave(sender.Session.Character);
            sender.Session.PostResponser = null;
        }

        Character character = CharacterManager.Instance.AddCharacter(dbCharacter);
        sender.Session.Character = character;
        sender.Session.PostResponser = character;

        sender.Session.Response.gameEnter.Result = Result.Success;
        sender.Session.Response.gameEnter.Errormsg = "None";
        sender.Session.Response.gameEnter.Character = character.Info;

        sender.SendResponse();
    }

    /// <summary>
    /// 保存当前在线角色。服务端只使用 Session.Character，客户端不能指定其他角色 ID。
    /// </summary>
    private void OnSaveCharacterProgress(
        NetConnection<NetSession> sender,
        UserSaveCharacterProgressRequest request)
    {
        sender.Session.Response.saveCharacterProgress = new UserSaveCharacterProgressResponse();

        TUser? user = sender.Session.User;
        Character? onlineCharacter = sender.Session.Character;
        if (user == null || onlineCharacter == null)
        {
            sender.Session.Response.saveCharacterProgress.Result = Result.Failed;
            sender.Session.Response.saveCharacterProgress.Errormsg = "请先进入角色";
            sender.SendResponse();
            return;
        }

        if (!TryValidateCharacterProgress(
                request,
                onlineCharacter.Data,
                out Dictionary<int, int> upgrades,
                out List<TInventoryItem> inventoryItems,
                out List<TEquippedItem> equippedItems,
                out List<string> purchasedLimitedShopItemIds,
                out List<TQuestProgress> questProgress,
                out string error))
        {
            sender.Session.Response.saveCharacterProgress.Result = Result.Failed;
            sender.Session.Response.saveCharacterProgress.Errormsg = error;
            sender.SendResponse();
            return;
        }

        try
        {
            TCharacter saved = DBService.Instance.SaveCharacterProgress(
                user.ID,
                onlineCharacter.Id,
                request.Level,
                request.Exp,
                request.PendingAttributeUpgradeCount,
                request.VaultDestroyedCount,
                request.CompletedBossCount,
                request.Gold,
                request.MerchantIntroCompleted,
                upgrades,
                inventoryItems,
                equippedItems,
                purchasedLimitedShopItemIds,
                questProgress);

            int characterIndex = user.Player.Characters.FindIndex(character => character.ID == saved.ID);
            if (characterIndex >= 0)
            {
                user.Player.Characters[characterIndex] = saved;
            }

            onlineCharacter.ApplyPersistedData(saved);
            sender.Session.Response.saveCharacterProgress.Result = Result.Success;
            sender.Session.Response.saveCharacterProgress.Errormsg = "None";
            sender.Session.Response.saveCharacterProgress.Character = BuildCharacterInfo(saved);
        }
        catch (Exception ex)
        {
            Log.ErrorFormat("Save character progress failed: {0}", ex);
            sender.Session.Response.saveCharacterProgress.Result = Result.Failed;
            sender.Session.Response.saveCharacterProgress.Errormsg = "角色存档保存失败";
        }

        sender.SendResponse();
    }

    private static bool TryValidateCharacterProgress(
        UserSaveCharacterProgressRequest request,
        TCharacter current,
        out Dictionary<int, int> upgrades,
        out List<TInventoryItem> inventoryItems,
        out List<TEquippedItem> equippedItems,
        out List<string> purchasedLimitedShopItemIds,
        out List<TQuestProgress> questProgress,
        out string error)
    {
        upgrades = new Dictionary<int, int>();
        inventoryItems = new List<TInventoryItem>();
        equippedItems = new List<TEquippedItem>();
        purchasedLimitedShopItemIds = new List<string>();
        questProgress = new List<TQuestProgress>();
        error = "";

        if (!TryValidateInventory(
                request.InventoryItems,
                request.ResetAfterDeath,
                out inventoryItems,
                out error))
        {
            return false;
        }

        if (!TryValidateEquipment(request.EquippedItems, out equippedItems, out error))
        {
            return false;
        }

        if (!TryValidateShopProgress(request, current, out purchasedLimitedShopItemIds, out error))
        {
            return false;
        }

        if (!QuestPersistenceRules.TryValidate(request.QuestProgress, current.QuestProgress, out questProgress, out error))
        {
            return false;
        }

        if (request.ResetAfterDeath)
        {
            bool isExactDeathReset = request.Level == 1 &&
                request.Exp == 0 &&
                request.PendingAttributeUpgradeCount == 0 &&
                request.VaultDestroyedCount == 0 &&
                request.CompletedBossCount == 0 &&
                request.AttributeUpgrades.Count == 0;
            if (!isExactDeathReset)
            {
                error = "死亡重置的成长和关卡数据必须全部归零";
                return false;
            }

            // 只有固定的 1 级空进度可以绕过防回档校验，客户端不能借此写入任意旧数据。
            return true;
        }

        if (request.Level < 1 || request.Level > 999 || request.Exp < 0)
        {
            error = "等级或经验数据非法";
            return false;
        }

        if (request.PendingAttributeUpgradeCount < 0 ||
            request.VaultDestroyedCount < 0 ||
            request.CompletedBossCount < 0 ||
            request.CompletedBossCount > request.VaultDestroyedCount)
        {
            error = "角色进度数据非法";
            return false;
        }

        if (request.Level < current.Level ||
            (request.Level == current.Level && request.Exp < current.Exp) ||
            request.VaultDestroyedCount < current.VaultDestroyedCount ||
            request.CompletedBossCount < current.CompletedBossCount)
        {
            error = "不能用旧进度覆盖服务器存档";
            return false;
        }

        long totalUpgradeCount = request.PendingAttributeUpgradeCount;
        foreach (NAttributeUpgradeInfo upgrade in request.AttributeUpgrades)
        {
            if (upgrade.AttributeType < 1 || upgrade.AttributeType > 8 || upgrade.UpgradeCount < 0)
            {
                error = "属性强化数据非法";
                return false;
            }

            if (!upgrades.TryAdd(upgrade.AttributeType, upgrade.UpgradeCount))
            {
                error = "属性强化类型重复";
                return false;
            }

            totalUpgradeCount += upgrade.UpgradeCount;
        }

        if (totalUpgradeCount > Math.Max(0, request.Level - 1))
        {
            error = "属性强化次数超过当前等级可获得数量";
            return false;
        }

        return true;
    }

    /// <summary>校验金币边界、首次对话单向状态和限购物品白名单，拒绝删除既有购买记录。</summary>
    private static bool TryValidateShopProgress(
        UserSaveCharacterProgressRequest request,
        TCharacter current,
        out List<string> purchasedLimitedShopItemIds,
        out string error)
    {
        purchasedLimitedShopItemIds = new List<string>();
        if (request.Gold < 0L || request.Gold > 9_999_999L)
        {
            error = "金币超出允许范围";
            return false;
        }

        if (current.MerchantIntroCompleted && !request.MerchantIntroCompleted)
        {
            error = "商人首次对话状态不能回滚";
            return false;
        }

        var usedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (string itemId in request.PurchasedLimitedShopItemIds)
        {
            if (!usedIds.Add(itemId) || !InventoryPersistenceRules.IsLimitedShopItem(itemId))
            {
                error = "限购记录包含重复或未知商品";
                return false;
            }

            purchasedLimitedShopItemIds.Add(itemId);
        }

        foreach (string existingId in current.PurchasedLimitedShopItemIds)
        {
            if (!usedIds.Contains(existingId))
            {
                error = "已购买的限购商品不能回滚";
                return false;
            }
        }

        purchasedLimitedShopItemIds.Sort(StringComparer.Ordinal);
        error = "";
        return true;
    }

    /// <summary>
    /// 校验客户端提交的背包结构。死亡重置请求只允许携带材料和任务物品，不能保留药水。
    /// </summary>
    private static bool TryValidateInventory(
        IEnumerable<NInventoryItemInfo> requestedItems,
        bool resetAfterDeath,
        out List<TInventoryItem> inventoryItems,
        out string error)
    {
        inventoryItems = new List<TInventoryItem>();
        var usedSlots = new HashSet<int>();

        foreach (NInventoryItemInfo item in requestedItems)
        {
            if (item == null ||
                item.SlotIndex < 0 || item.SlotIndex >= InventoryPersistenceRules.Capacity ||
                !usedSlots.Add(item.SlotIndex) ||
                !InventoryPersistenceRules.TryGetRule(item.ItemId, out InventoryItemRule rule) ||
                item.Count <= 0 || item.Count > rule.MaxStack)
            {
                error = "背包格子、物品ID或数量非法";
                return false;
            }

            if (resetAfterDeath && !rule.PersistsAfterDeath)
            {
                error = "死亡重置存档不能保留消耗品";
                return false;
            }

            inventoryItems.Add(new TInventoryItem
            {
                SlotIndex = item.SlotIndex,
                ItemId = item.ItemId,
                Count = item.Count
            });
        }

        inventoryItems.Sort((left, right) => left.SlotIndex.CompareTo(right.SlotIndex));
        error = "";
        return true;
    }

    /// <summary>校验穿戴槽不重复、物品属于服务端白名单且配置槽位完全匹配。</summary>
    private static bool TryValidateEquipment(
        IEnumerable<NEquippedItemInfo> requestedItems,
        out List<TEquippedItem> equippedItems,
        out string error)
    {
        equippedItems = new List<TEquippedItem>();
        var usedSlots = new HashSet<int>();
        foreach (NEquippedItemInfo item in requestedItems)
        {
            if (item == null || item.EquipmentSlot < 1 || item.EquipmentSlot > 6 ||
                !usedSlots.Add(item.EquipmentSlot) ||
                !InventoryPersistenceRules.TryGetRule(item.ItemId, out InventoryItemRule rule) ||
                rule.EquipmentSlot != item.EquipmentSlot)
            {
                error = "已穿戴装备包含重复槽位、未知物品或错误槽位";
                return false;
            }

            equippedItems.Add(new TEquippedItem { EquipmentSlot = item.EquipmentSlot, ItemId = item.ItemId });
        }

        equippedItems.Sort((left, right) => left.EquipmentSlot.CompareTo(right.EquipmentSlot));
        error = "";
        return true;
    }

    /// <summary>
    /// 处理离开游戏请求。
    /// 离场时要把在线角色从 CharacterManager 中移除，避免服务器残留“幽灵在线角色”。
    /// </summary>
    private void OnGameLeave(NetConnection<NetSession> sender, UserGameLeaveRequest request)
    {
        Character? character = sender.Session.Character;
        sender.Session.Response.gameLeave = new UserGameLeaveResponse();

        if (character != null)
        {
            CharacterLeave(character);
            sender.Session.Character = null;
            sender.Session.PostResponser = null;
        }

        sender.Session.Response.gameLeave.Result = Result.Success;
        sender.Session.Response.gameLeave.Errormsg = "None";
        sender.SendResponse();
    }

    /// <summary>
    /// 在线角色离场的统一清理入口。
    /// 主动退出和异常断线都走这里，避免清理逻辑分散后前后不一致。
    /// </summary>
    public void CharacterLeave(Character character)
    {
        // 主动离开和异常断线共用同一清理入口，避免在线角色残留。
        Log.InfoFormat("CharacterLeave: characterID:{0}:{1}", character.Id, character.Info.Name);
        CharacterManager.Instance.RemoveCharacter(character.Id);
        character.Clear();
    }

    /// <summary>
    /// 把数据库用户模型转换成网络层用户信息。
    /// 这样数据库表结构与网络协议结构就能保持解耦。
    /// </summary>
    private static NUserInfo BuildUserInfo(TUser user)
    {
        var info = new NUserInfo
        {
            Id = checked((int)user.ID),
            Player = new NPlayerInfo
            {
                Id = checked((int)user.Player.ID)
            }
        };

        foreach (TCharacter character in user.Player.Characters)
        {
            info.Player.Characters.Add(BuildCharacterInfo(character));
        }

        return info;
    }

    /// <summary>
    /// 把数据库角色记录转换成客户端可识别的角色信息。
    /// </summary>
    private static NCharacterInfo BuildCharacterInfo(TCharacter character)
    {
        var info = new NCharacterInfo
        {
            Id = checked((int)character.ID),
            ConfigId = character.TID,
            EntityId = checked((int)character.ID),
            Name = character.Name,
            Type = CharacterType.Player,
            Class = (CharacterClass)character.Class,
            Level = character.Level,
            Exp = character.Exp,
            PendingAttributeUpgradeCount = character.PendingAttributeUpgradeCount,
            VaultDestroyedCount = character.VaultDestroyedCount,
            CompletedBossCount = character.CompletedBossCount,
            mapId = character.MapID,
            Gold = character.Gold,
            SlotIndex = character.SlotIndex,
            MerchantIntroCompleted = character.MerchantIntroCompleted
        };

        foreach ((int attributeType, int upgradeCount) in character.AttributeUpgradeCounts)
        {
            info.AttributeUpgrades.Add(new NAttributeUpgradeInfo
            {
                AttributeType = attributeType,
                UpgradeCount = upgradeCount
            });
        }

        foreach (TInventoryItem item in character.InventoryItems)
        {
            info.InventoryItems.Add(new NInventoryItemInfo
            {
                SlotIndex = item.SlotIndex,
                ItemId = item.ItemId,
                Count = item.Count
            });
        }

        foreach (TEquippedItem item in character.EquippedItems)
        {
            info.EquippedItems.Add(new NEquippedItemInfo
            {
                EquipmentSlot = item.EquipmentSlot,
                ItemId = item.ItemId
            });
        }

        info.PurchasedLimitedShopItemIds.AddRange(character.PurchasedLimitedShopItemIds);
        foreach (TQuestProgress progress in character.QuestProgress)
        {
            info.QuestProgress.Add(new NQuestProgressInfo
            {
                QuestId = progress.QuestId,
                State = progress.State,
                CurrentCount = progress.CurrentCount
            });
        }

        return info;
    }
}
