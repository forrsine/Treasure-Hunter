using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家输入集中读取器。
/// 
/// 新手理解：
/// 1. 这个脚本每帧读取键盘、鼠标输入。
/// 2. 其他脚本不要重复写 Input.GetAxis，而是从 InputCo.Instance 里拿结果。
/// 3. 这样做的好处是：以后如果要换按键、接手柄、做暂停禁用输入，只需要优先改这里。
/// </summary>
public class InputCo : MonoBehaviour
{
    // 单例引用：其他脚本可以用 InputCo.Instance 访问这个脚本。
    // 注意：场景中最好只放一个 InputCo，否则后 Awake 的会覆盖前一个。
    public static InputCo Instance;

    // 横向输入：A/D 或 左/右方向键通常会给 -1 到 1。
    public float Xinput;

    // 纵向输入：W/S 或 上/下方向键通常会给 -1 到 1。
    public float Yinput;

    // 鼠标输入打包到一个 Vector3：
    // x = 鼠标左右移动，y = 鼠标上下移动，z = 鼠标滚轮。
    public Vector3 MouseInput;

    // 鼠标左键“按下的那一瞬间”为 true，只持续一帧，适合触发攻击。
    public bool leftMouseDown;

    /// <summary>
    /// 注册单例，供玩家、摄像机等脚本读取统一输入。
    /// </summary>
    private void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// 每帧采样一次键盘和鼠标，把结果缓存到公开字段。
    /// </summary>
    void Update()
    {
        // Horizontal / Vertical 是 Unity 旧输入系统里的默认轴名称。
        Xinput = Input.GetAxis("Horizontal");
        Yinput = Input.GetAxis("Vertical");

        //Debug.Log(Xinput);
        // Debug.Log(Yinput);

        // Vector3.Set 可以直接改已有结构体的三个值，少写一次 new Vector3。
        MouseInput.Set(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"), Input.GetAxis("Mouse ScrollWheel"));
        leftMouseDown = Input.GetMouseButtonDown(0);
    }
}
