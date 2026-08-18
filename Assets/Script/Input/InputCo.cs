using UnityEngine;

/// <summary>
/// 玩家输入集中读取器。
/// 其他脚本不要重复调用 Input.GetAxis / Input.GetMouseButtonDown，
/// 而是通过 IGameplayInput 读取这一帧缓存好的输入结果，方便后续改键位、接手柄或做输入禁用。
/// </summary>
[DefaultExecutionOrder(-100)]
public class InputCo : MonoBehaviour, IGameplayInput
{
    /// <summary>
    /// 兼容旧脚本的单例入口；新代码优先通过 GameplayRuntime.CurrentInput 访问输入接口。
    /// </summary>
    public static InputCo Instance;

    public float Xinput;
    public float Yinput;
    public Vector3 MouseInput;
    public bool leftMouseDown;
    public bool rollDown;
    public bool developerModeToggleDown;
    public bool debugAddLevelsDown;
    public bool debugAddExpDown;
    public bool debugRestoreManaDown;
    public bool debugBreakVaultDown;
    public bool inventoryToggleDown;
    public bool skill1Down;
    public bool skill1Held;
    public bool skill1Up;
    public bool skill2Down;
    public bool skill2Held;
    public bool skill2Up;
    public bool skill3Down;
    public bool skill3Held;
    public bool skill3Up;

    public float XInput => Xinput;
    public float YInput => Yinput;
    public bool LeftMouseDown => leftMouseDown;
    public bool RollDown => rollDown;
    public bool DeveloperModeToggleDown => developerModeToggleDown;
    public bool DebugAddLevelsDown => debugAddLevelsDown;
    public bool DebugAddExpDown => debugAddExpDown;
    public bool DebugRestoreManaDown => debugRestoreManaDown;
    public bool DebugBreakVaultDown => debugBreakVaultDown;
    public bool InventoryToggleDown => inventoryToggleDown;
    Vector3 IGameplayInput.MouseInput => MouseInput;

    public bool Skill1Down => skill1Down;
    public bool Skill1Held => skill1Held;
    public bool Skill1Up => skill1Up;
    public bool Skill2Down => skill2Down;
    public bool Skill2Held => skill2Held;
    public bool Skill2Up => skill2Up;
    public bool Skill3Down => skill3Down;
    public bool Skill3Held => skill3Held;
    public bool Skill3Up => skill3Up;

    private void Awake()
    {
        Instance = this;
        GameplayRuntime.Instance.RegisterInput(this);
    }

    private void OnDestroy()
    {
        GameplayRuntime.Instance.UnregisterInput(this);

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 每帧采样一次输入，把结果缓存到字段中。
    /// 翻滚默认右键触发，额外支持 LeftAlt。
    /// F1、L、P、O、N 只负责采样开发者快捷键，是否允许执行由独立的开发者模式组件判断。
    /// B 是正式背包快捷键，不依赖开发者模式。
    /// </summary>
    private void Update()
    {
        Xinput = Input.GetAxis("Horizontal");
        Yinput = Input.GetAxis("Vertical");
        MouseInput.Set(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"), Input.GetAxis("Mouse ScrollWheel"));

        leftMouseDown = Input.GetMouseButtonDown(0);
        rollDown = Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.LeftAlt);
        developerModeToggleDown = Input.GetKeyDown(KeyCode.F1);
        debugAddLevelsDown = Input.GetKeyDown(KeyCode.L);
        debugAddExpDown = Input.GetKeyDown(KeyCode.P);
        debugRestoreManaDown = Input.GetKeyDown(KeyCode.O);
        debugBreakVaultDown = Input.GetKeyDown(KeyCode.N);
        inventoryToggleDown = Input.GetKeyDown(KeyCode.B);

        skill1Down = Input.GetKeyDown(KeyCode.Alpha1);
        skill1Held = Input.GetKey(KeyCode.Alpha1);
        skill1Up = Input.GetKeyUp(KeyCode.Alpha1);
        skill2Down = Input.GetKeyDown(KeyCode.Alpha2);
        skill2Held = Input.GetKey(KeyCode.Alpha2);
        skill2Up = Input.GetKeyUp(KeyCode.Alpha2);
        skill3Down = Input.GetKeyDown(KeyCode.Alpha3);
        skill3Held = Input.GetKey(KeyCode.Alpha3);
        skill3Up = Input.GetKeyUp(KeyCode.Alpha3);
    }
}
