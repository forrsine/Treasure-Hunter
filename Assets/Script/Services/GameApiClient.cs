using Network;
using SkillBridge.Message;
using System;
using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>
/// 当前客户端会话的数据来源。在线账号走服务端，游客账号走本地 JSON 存档。
/// </summary>
public enum GameSessionMode
{
    None,
    Online,
    Guest
}

/// <summary>
/// 客户端业务 API 门面：向 UI 提供注册、登录、创建角色等协程接口，
/// 内部根据会话模式把角色操作路由到服务端或本地游客存档。
/// UI 不需要关心底层使用 Socket、Protobuf 还是 JSON 文件。
/// </summary>
public class GameApiClient : MonoBehaviour
{
    [SerializeField] private string serverIp = "127.0.0.1";
    [SerializeField] private int serverPort = 8000;
    [SerializeField] private float requestTimeout = 10f;

    public static GameApiClient Instance { get; private set; }

    public string Token { get; private set; }
    public string Username { get; private set; }
    public GameSessionMode SessionMode { get; private set; }
    public bool IsLoggedIn => SessionMode != GameSessionMode.None;
    public bool IsGuest => SessionMode == GameSessionMode.Guest;

    private const string UsernameKey = "AuthUsername";

    private NetClient netClient;
    private Action<UserRegisterResponse> registerCallback;
    private Action<UserLoginResponse> loginCallback;
    private Action<UserCreateCharacterResponse> createCharacterCallback;
    private Action<UserGameEnterResponse> gameEnterCallback;
    private Action<UserGameLeaveResponse> gameLeaveCallback;
    private Action<UserSaveCharacterProgressResponse> saveCharacterProgressCallback;
    private LocalGuestSaveService localGuestSaveService;
    private NCharacter[] cachedCharacters = new NCharacter[0];
    private int highScore;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (GetComponent<CharacterProgressSaveService>() == null)
        {
            gameObject.AddComponent<CharacterProgressSaveService>();
        }

        EnsureNetClient();
        LoadSession();

        // 订阅与注销必须成对出现，避免跨场景后旧对象仍收到网络响应。
        MessageDistributer.Instance.Subscribe<UserRegisterResponse>(OnUserRegister);
        MessageDistributer.Instance.Subscribe<UserLoginResponse>(OnUserLogin);
        MessageDistributer.Instance.Subscribe<UserCreateCharacterResponse>(OnUserCreateCharacter);
        MessageDistributer.Instance.Subscribe<UserGameEnterResponse>(OnUserGameEnter);
        MessageDistributer.Instance.Subscribe<UserGameLeaveResponse>(OnUserGameLeave);
        MessageDistributer.Instance.Subscribe<UserSaveCharacterProgressResponse>(OnSaveCharacterProgress);
    }

    private void OnDestroy()
    {
        MessageDistributer.Instance.Unsubscribe<UserRegisterResponse>(OnUserRegister);
        MessageDistributer.Instance.Unsubscribe<UserLoginResponse>(OnUserLogin);
        MessageDistributer.Instance.Unsubscribe<UserCreateCharacterResponse>(OnUserCreateCharacter);
        MessageDistributer.Instance.Unsubscribe<UserGameEnterResponse>(OnUserGameEnter);
        MessageDistributer.Instance.Unsubscribe<UserGameLeaveResponse>(OnUserGameLeave);
        MessageDistributer.Instance.Unsubscribe<UserSaveCharacterProgressResponse>(OnSaveCharacterProgress);
    }

    /// <summary>
    /// 保存本地会话痕迹。
    /// 当前原型阶段主要缓存用户名；正式项目通常会在这里保存可验证的 Token。
    /// </summary>
    public void SaveSession(string token, string username)
    {
        Token = token;
        Username = username;
        SessionMode = GameSessionMode.Online;
        highScore = 0;
        cachedCharacters = new NCharacter[0];
        localGuestSaveService?.ResetRuntimeSession();

        PlayerPrefs.SetString(UsernameKey, username);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 从本地读取上次登录信息。
    /// </summary>
    public void LoadSession()
    {
        Username = PlayerPrefs.GetString(UsernameKey, "");
        Token = "";
        SessionMode = GameSessionMode.None;
    }

    /// <summary>
    /// 清理登录态和本地角色缓存。
    /// 退出登录时必须一起清空缓存，避免下一位用户看到上一位的数据。
    /// </summary>
    public void ClearSession()
    {
        Token = "";
        Username = "";
        SessionMode = GameSessionMode.None;
        highScore = 0;
        cachedCharacters = new NCharacter[0];
        localGuestSaveService?.ResetRuntimeSession();

        PlayerPrefs.DeleteKey(UsernameKey);
        PlayerPrefs.DeleteKey("AuthToken");
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 发送注册请求并等待响应。
    /// UI 可以通过协程自然地先显示“正在注册”，再在回调里处理结果。
    /// </summary>
    public IEnumerator Register(string username, string password, Action<bool, string> onDone)
    {
        bool done = false;
        bool success = false;
        string message = "";

        registerCallback = response =>
        {
            success = response.Result == Result.Success;
            message = response.Errormsg;
            done = true;
        };

        NetMessage request = new NetMessage
        {
            Request = new NetMessageRequest
            {
                userRegister = new UserRegisterRequest
                {
                    User = username,
                    Password = password
                }
            }
        };

        Send(request);
        yield return WaitForResponse(() => done);

        if (!done)
        {
            registerCallback = null;
            onDone?.Invoke(false, "服务器响应超时，请确认服务端已启动。");
            yield break;
        }

        onDone?.Invoke(success, NormalizeServerMessage(message, success ? "注册成功。" : "注册失败。"));
    }

    /// <summary>
    /// 发送登录请求，并在成功后缓存服务端返回的角色列表。
    /// </summary>
    public IEnumerator Login(string username, string password, Action<bool, string> onDone)
    {
        bool done = false;
        bool success = false;
        string message = "";

        loginCallback = response =>
        {
            success = response.Result == Result.Success;
            message = response.Errormsg;

            if (success)
            {
                SaveSession("", username);
                cachedCharacters = ToCharacters(response.Userinfo);
            }

            done = true;
        };

        NetMessage request = new NetMessage
        {
            Request = new NetMessageRequest
            {
                userLogin = new UserLoginRequest
                {
                    User = username,
                    Password = password
                }
            }
        };

        Send(request);
        yield return WaitForResponse(() => done);

        if (!done)
        {
            loginCallback = null;
            onDone?.Invoke(false, "服务器响应超时，请确认服务端已启动。");
            yield break;
        }

        onDone?.Invoke(success, NormalizeServerMessage(message, success ? "登录成功。" : "登录失败。"));
    }

    /// <summary>
    /// 进入单一本地游客账号。这里只读取电脑上的 JSON，不发送任何网络请求。
    /// 游客会话不会写入 PlayerPrefs，重新启动游戏后需要再次点击游客模式。
    /// </summary>
    public IEnumerator LoginAsGuest(Action<bool, string> onDone)
    {
        yield return null;

        if (localGuestSaveService == null)
        {
            localGuestSaveService = new LocalGuestSaveService();
        }

        // 切换数据来源前先清掉上一会话的运行时缓存，避免在线角色和游客角色相互串档。
        Token = "";
        Username = "";
        SessionMode = GameSessionMode.None;
        highScore = 0;
        cachedCharacters = new NCharacter[0];
        PlayerPrefs.DeleteKey(UsernameKey);
        PlayerPrefs.DeleteKey("AuthToken");
        PlayerPrefs.Save();

        if (!localGuestSaveService.TryLoad(out NCharacter[] characters, out string message))
        {
            onDone?.Invoke(false, string.IsNullOrEmpty(message) ? "游客存档读取失败。" : message);
            yield break;
        }

        Username = "游客";
        SessionMode = GameSessionMode.Guest;
        cachedCharacters = characters ?? new NCharacter[0];
        onDone?.Invoke(true, string.IsNullOrEmpty(message) ? "游客模式已进入。" : message);
    }

    public IEnumerator ValidateSession(Action<bool, string> onDone)
    {
        yield return null;
        onDone?.Invoke(IsLoggedIn, IsLoggedIn ? "已登录。" : "请先登录。");
    }

    public IEnumerator GetProfile(Action<bool, string, int> onDone)
    {
        yield return null;
        onDone?.Invoke(IsLoggedIn, Username, highScore);
    }

    public IEnumerator SubmitHighScore(int score, Action<bool, string, int> onDone)
    {
        yield return null;
        highScore = Mathf.Max(highScore, score);
        onDone?.Invoke(IsLoggedIn, IsLoggedIn ? "最高分已保存到本地。" : "请先登录。", highScore);
    }

    public IEnumerator GetCharacters(Action<bool, string, NCharacter[]> onDone)
    {
        yield return null;

        if (!IsLoggedIn)
        {
            onDone?.Invoke(false, "请先登录。", new NCharacter[0]);
            yield break;
        }

        onDone?.Invoke(true, "角色存档加载完成。", cachedCharacters ?? new NCharacter[0]);
    }

    /// <summary>
    /// 创建角色并刷新本地角色缓存。
    /// 因为网络请求是异步完成的，所以这里通过回调把结果还给 UI。
    /// </summary>
    public IEnumerator CreateCharacter(int slotIndex, string characterName, int classId, Action<bool, string, NCharacter> onDone)
    {
        if (!IsLoggedIn)
        {
            onDone?.Invoke(false, "请先登录。", null);
            yield break;
        }

        if (IsGuest)
        {
            yield return null;
            NCharacter localCharacter = null;
            NCharacter[] localCharacters = new NCharacter[0];
            string localMessage = "游客存档服务未初始化。";
            bool localSuccess = localGuestSaveService != null &&
                localGuestSaveService.TryCreateCharacter(
                    slotIndex,
                    characterName,
                    classId,
                    out localCharacter,
                    out localCharacters,
                    out localMessage);

            if (localSuccess)
            {
                cachedCharacters = localCharacters ?? new NCharacter[0];
            }

            onDone?.Invoke(
                localSuccess,
                string.IsNullOrEmpty(localMessage)
                    ? localSuccess ? "游客角色创建成功。" : "游客角色创建失败。"
                    : localMessage,
                localCharacter);
            yield break;
        }

        bool done = false;
        bool success = false;
        string message = "";
        NCharacter createdCharacter = null;

        createCharacterCallback = response =>
        {
            success = response.Result == Result.Success;
            message = response.Errormsg;

            if (success)
            {
                cachedCharacters = ToCharacters(response.Characters);
                createdCharacter = cachedCharacters.FirstOrDefault(character => character != null && character.slotIndex == slotIndex);
            }

            done = true;
        };

        NetMessage request = new NetMessage
        {
            Request = new NetMessageRequest
            {
                createChar = new UserCreateCharacterRequest
                {
                    SlotIndex = slotIndex,
                    Name = characterName,
                    Class = (CharacterClass)classId
                }
            }
        };

        Send(request);
        yield return WaitForResponse(() => done);

        if (!done)
        {
            createCharacterCallback = null;
            onDone?.Invoke(false, "服务器响应超时，请确认服务端已启动。", null);
            yield break;
        }

        onDone?.Invoke(success, NormalizeServerMessage(message, success ? "角色创建成功。" : "角色创建失败。"), createdCharacter);
    }

    /// <summary>
    /// 让当前存档来源确认实际进入的角色。后续存档只允许写入这个 Session 角色。
    /// </summary>
    public IEnumerator EnterCharacter(NCharacter selectedCharacter, Action<bool, string, NCharacter> onDone)
    {
        if (!IsLoggedIn || selectedCharacter == null || selectedCharacter.id <= 0)
        {
            onDone?.Invoke(false, "请选择有效角色。", null);
            yield break;
        }

        if (IsGuest)
        {
            yield return null;
            NCharacter localCharacter = null;
            string localMessage = "游客存档服务未初始化。";
            bool localSuccess = localGuestSaveService != null &&
                localGuestSaveService.TryEnterCharacter(
                    selectedCharacter,
                    out localCharacter,
                    out localMessage);
            if (localSuccess)
            {
                UpdateCachedCharacter(localCharacter);
            }

            onDone?.Invoke(
                localSuccess,
                string.IsNullOrEmpty(localMessage)
                    ? localSuccess ? "游客角色进入成功。" : "游客角色进入失败。"
                    : localMessage,
                localCharacter);
            yield break;
        }

        bool done = false;
        bool success = false;
        string message = "";
        NCharacter enteredCharacter = null;

        gameEnterCallback = response =>
        {
            success = response.Result == Result.Success && response.Character != null;
            message = response.Errormsg;
            if (success)
            {
                enteredCharacter = ToCharacter(response.Character);
                UpdateCachedCharacter(enteredCharacter);
            }

            done = true;
        };

        int legacyIndex = Array.FindIndex(cachedCharacters, character =>
            character != null && character.id == selectedCharacter.id);
        Send(new NetMessage
        {
            Request = new NetMessageRequest
            {
                gameEnter = new UserGameEnterRequest
                {
                    characterIdx = legacyIndex,
                    CharacterId = checked((int)selectedCharacter.id)
                }
            }
        });

        yield return WaitForResponse(() => done);
        if (!done)
        {
            gameEnterCallback = null;
            onDone?.Invoke(false, "进入角色超时，请确认服务端已启动。", null);
            yield break;
        }

        onDone?.Invoke(success, NormalizeServerMessage(message, success ? "角色进入成功。" : "角色进入失败。"), enteredCharacter);
    }

    /// <summary>把当前玩家成长快照写入在线数据库或本地游客文件。</summary>
    public IEnumerator SaveCharacterProgress(
        PlayerProgressSaveData progress,
        int vaultDestroyedCount,
        int completedBossCount,
        Action<bool, string, NCharacter> onDone)
    {
        if (!IsLoggedIn || progress == null)
        {
            onDone?.Invoke(false, "当前没有可保存的登录角色。", null);
            yield break;
        }

        if (IsGuest)
        {
            yield return null;
            NCharacter localCharacter = null;
            string localMessage = "游客存档服务未初始化。";
            bool localSuccess = localGuestSaveService != null &&
                localGuestSaveService.TrySaveCharacterProgress(
                    progress,
                    vaultDestroyedCount,
                    completedBossCount,
                    out localCharacter,
                    out localMessage);
            if (localSuccess)
            {
                UpdateCachedCharacter(localCharacter);
            }

            onDone?.Invoke(
                localSuccess,
                string.IsNullOrEmpty(localMessage)
                    ? localSuccess ? "游客角色进度已保存。" : "游客角色进度保存失败。"
                    : localMessage,
                localCharacter);
            yield break;
        }

        bool done = false;
        bool success = false;
        string message = "";
        NCharacter savedCharacter = null;

        saveCharacterProgressCallback = response =>
        {
            success = response.Result == Result.Success && response.Character != null;
            message = response.Errormsg;
            if (success)
            {
                savedCharacter = ToCharacter(response.Character);
                UpdateCachedCharacter(savedCharacter);
            }

            done = true;
        };

        var request = new UserSaveCharacterProgressRequest
        {
            Level = progress.Level,
            Exp = progress.Exp,
            PendingAttributeUpgradeCount = progress.PendingAttributeUpgradeCount,
            VaultDestroyedCount = vaultDestroyedCount,
            CompletedBossCount = completedBossCount
        };

        for (int i = 0; i < progress.AttributeUpgrades.Count; i++)
        {
            NAttributeUpgradeSave upgrade = progress.AttributeUpgrades[i];
            if (upgrade == null)
            {
                continue;
            }

            request.AttributeUpgrades.Add(new NAttributeUpgradeInfo
            {
                AttributeType = upgrade.attributeType,
                UpgradeCount = upgrade.upgradeCount
            });
        }

        Send(new NetMessage
        {
            Request = new NetMessageRequest { saveCharacterProgress = request }
        });

        yield return WaitForResponse(() => done);
        if (!done)
        {
            saveCharacterProgressCallback = null;
            onDone?.Invoke(false, "角色存档保存超时。", null);
            yield break;
        }

        onDone?.Invoke(success, NormalizeServerMessage(message, success ? "角色进度已保存。" : "角色进度保存失败。"), savedCharacter);
    }

    /// <summary>结束当前角色会话；在线模式通知服务端，游客模式只清理本地活动角色。</summary>
    public IEnumerator LeaveCharacter(Action<bool, string> onDone)
    {
        if (IsGuest)
        {
            yield return null;
            localGuestSaveService?.LeaveCharacter();
            onDone?.Invoke(true, "已离开游客角色。");
            yield break;
        }

        bool done = false;
        bool success = false;
        string message = "";

        gameLeaveCallback = response =>
        {
            success = response.Result == Result.Success;
            message = response.Errormsg;
            done = true;
        };

        Send(new NetMessage
        {
            Request = new NetMessageRequest { gameLeave = new UserGameLeaveRequest() }
        });

        yield return WaitForResponse(() => done);
        if (!done)
        {
            gameLeaveCallback = null;
            onDone?.Invoke(false, "离开角色请求超时。");
            yield break;
        }

        onDone?.Invoke(success, NormalizeServerMessage(message, success ? "已离开角色。" : "离开角色失败。"));
    }

    /// <summary>
    /// 底层发送入口。
    /// 发送前会确保 NetClient 已存在且服务端地址已配置。
    /// </summary>
    private void Send(NetMessage message)
    {
        EnsureNetClient();
        netClient.Init(serverIp, serverPort);
        netClient.SendMessage(message);
    }

    /// <summary>
    /// 等待某个异步响应完成，直到收到结果或超时。
    /// </summary>
    private IEnumerator WaitForResponse(Func<bool> isDone)
    {
        float deadline = Time.realtimeSinceStartup + requestTimeout;
        while (!isDone() && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }
    }

    private static string NormalizeServerMessage(string message, string fallback)
    {
        if (string.IsNullOrWhiteSpace(message) || string.Equals(message, "None", StringComparison.OrdinalIgnoreCase))
        {
            return fallback;
        }

        switch (message)
        {
            case "Login failed.":
                return "登录失败。";
            case "Register failed.":
                return "注册失败。";
            case "Username already exists.":
                return "账号已存在。";
            case "This slot already has a character.":
                return "这个存档位已经有角色了。";
            case "Character create failed.":
                return "角色创建失败。";
            default:
                return message;
        }
    }

    private void EnsureNetClient()
    {
        if (netClient != null)
        {
            return;
        }

        netClient = NetClient.Instance;
        if (netClient != null)
        {
            return;
        }

        GameObject netClientObject = new GameObject("NetClient");
        netClient = netClientObject.AddComponent<NetClient>();
    }

    /// <summary>
    /// 收到注册响应后，转交给当前等待中的回调。
    /// 先取出再置空，可以避免同一响应被重复消费。
    /// </summary>
    private void OnUserRegister(object sender, UserRegisterResponse response)
    {
        Action<UserRegisterResponse> callback = registerCallback;
        registerCallback = null;
        callback?.Invoke(response);
    }

    /// <summary>
    /// 收到登录响应后的转发入口。
    /// </summary>
    private void OnUserLogin(object sender, UserLoginResponse response)
    {
        Action<UserLoginResponse> callback = loginCallback;
        loginCallback = null;
        callback?.Invoke(response);
    }

    /// <summary>
    /// 收到创建角色响应后的转发入口。
    /// </summary>
    private void OnUserCreateCharacter(object sender, UserCreateCharacterResponse response)
    {
        Action<UserCreateCharacterResponse> callback = createCharacterCallback;
        createCharacterCallback = null;
        callback?.Invoke(response);
    }

    private void OnUserGameEnter(object sender, UserGameEnterResponse response)
    {
        Action<UserGameEnterResponse> callback = gameEnterCallback;
        gameEnterCallback = null;
        callback?.Invoke(response);
    }

    private void OnUserGameLeave(object sender, UserGameLeaveResponse response)
    {
        Action<UserGameLeaveResponse> callback = gameLeaveCallback;
        gameLeaveCallback = null;
        callback?.Invoke(response);
    }

    private void OnSaveCharacterProgress(object sender, UserSaveCharacterProgressResponse response)
    {
        Action<UserSaveCharacterProgressResponse> callback = saveCharacterProgressCallback;
        saveCharacterProgressCallback = null;
        callback?.Invoke(response);
    }

    private static NCharacter[] ToCharacters(NUserInfo userInfo)
    {
        if (userInfo == null || userInfo.Player == null)
        {
            return new NCharacter[0];
        }

        return ToCharacters(userInfo.Player.Characters);
    }

    private static NCharacter[] ToCharacters(System.Collections.Generic.IEnumerable<NCharacterInfo> infos)
    {
        if (infos == null)
        {
            return new NCharacter[0];
        }

        return infos
            .Where(info => info != null)
            .Select(ToCharacter)
            .ToArray();
    }

    private static NCharacter ToCharacter(NCharacterInfo info)
    {
        NCharacter character = new NCharacter
        {
            id = info.Id,
            slotIndex = info.SlotIndex,
            name = info.Name,
            classId = (int)info.Class,
            level = info.Level,
            exp = info.Exp,
            pendingAttributeUpgradeCount = info.PendingAttributeUpgradeCount,
            vaultDestroyedCount = info.VaultDestroyedCount,
            completedBossCount = info.CompletedBossCount
        };

        foreach (NAttributeUpgradeInfo upgrade in info.AttributeUpgrades)
        {
            character.attributeUpgrades.Add(new NAttributeUpgradeSave
            {
                attributeType = upgrade.AttributeType,
                upgradeCount = upgrade.UpgradeCount
            });
        }

        return character;
    }

    private void UpdateCachedCharacter(NCharacter updatedCharacter)
    {
        if (updatedCharacter == null)
        {
            return;
        }

        int index = Array.FindIndex(cachedCharacters, character =>
            character != null && character.id == updatedCharacter.id);
        if (index >= 0)
        {
            cachedCharacters[index] = updatedCharacter.Clone();
            return;
        }

        cachedCharacters = cachedCharacters
            .Concat(new[] { updatedCharacter.Clone() })
            .ToArray();
    }
}
