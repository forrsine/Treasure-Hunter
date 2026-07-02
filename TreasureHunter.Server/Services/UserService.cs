using Common;
using GameServer.Entities;
using GameServer.Managers;
using Network;
using SkillBridge.Message;

namespace GameServer.Services;

public sealed class UserService : Singleton<UserService>
{
    public UserService()
    {
        MessageDistributer<NetConnection<NetSession>>.Instance.Subscribe<UserLoginRequest>(OnLogin);
        MessageDistributer<NetConnection<NetSession>>.Instance.Subscribe<UserRegisterRequest>(OnRegister);
        MessageDistributer<NetConnection<NetSession>>.Instance.Subscribe<UserCreateCharacterRequest>(OnCreateCharacter);
        MessageDistributer<NetConnection<NetSession>>.Instance.Subscribe<UserGameEnterRequest>(OnGameEnter);
        MessageDistributer<NetConnection<NetSession>>.Instance.Subscribe<UserGameLeaveRequest>(OnGameLeave);
    }

    public void Init()
    {
    }

    private void OnLogin(NetConnection<NetSession> sender, UserLoginRequest request)
    {
        Log.InfoFormat("UserLoginRequest: User:{0} Pass:{1}", request.User, request.Password);

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

    private void OnRegister(NetConnection<NetSession> sender, UserRegisterRequest request)
    {
        Log.InfoFormat("UserRegisterRequest: User:{0} Pass:{1}", request.User, request.Password);

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

    private void OnGameEnter(NetConnection<NetSession> sender, UserGameEnterRequest request)
    {
        TUser? user = sender.Session.User;
        sender.Session.Response.gameEnter = new UserGameEnterResponse();

        if (user == null)
        {
            sender.Session.Response.gameEnter.Result = Result.Failed;
            sender.Session.Response.gameEnter.Errormsg = "请先登录";
            sender.SendResponse();
            return;
        }

        if (request.characterIdx < 0 || request.characterIdx >= user.Player.Characters.Count)
        {
            sender.Session.Response.gameEnter.Result = Result.Failed;
            sender.Session.Response.gameEnter.Errormsg = "角色不存在";
            sender.SendResponse();
            return;
        }

        TCharacter dbCharacter = user.Player.Characters[request.characterIdx];
        Log.InfoFormat("UserGameEnterRequest: characterID:{0}:{1}", dbCharacter.ID, dbCharacter.Name);

        Character character = CharacterManager.Instance.AddCharacter(dbCharacter);
        sender.Session.Character = character;
        sender.Session.PostResponser = character;

        sender.Session.Response.gameEnter.Result = Result.Success;
        sender.Session.Response.gameEnter.Errormsg = "None";
        sender.Session.Response.gameEnter.Character = character.Info;

        sender.SendResponse();
    }

    private void OnGameLeave(NetConnection<NetSession> sender, UserGameLeaveRequest request)
    {
        Character? character = sender.Session.Character;
        sender.Session.Response.gameLeave = new UserGameLeaveResponse();

        if (character != null)
        {
            CharacterLeave(character);
            sender.Session.Character = null;
        }

        sender.Session.Response.gameLeave.Result = Result.Success;
        sender.Session.Response.gameLeave.Errormsg = "None";
        sender.SendResponse();
    }

    public void CharacterLeave(Character character)
    {
        Log.InfoFormat("CharacterLeave: characterID:{0}:{1}", character.Id, character.Info.Name);
        CharacterManager.Instance.RemoveCharacter(character.Id);
        character.Clear();
    }

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

    private static NCharacterInfo BuildCharacterInfo(TCharacter character)
    {
        return new NCharacterInfo
        {
            Id = checked((int)character.ID),
            ConfigId = character.TID,
            EntityId = checked((int)character.ID),
            Name = character.Name,
            Type = CharacterType.Player,
            Class = (CharacterClass)character.Class,
            Level = character.Level,
            mapId = character.MapID,
            Gold = character.Gold,
            SlotIndex = character.SlotIndex
        };
    }
}
