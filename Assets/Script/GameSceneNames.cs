/// <summary>
/// 统一保存场景名字，避免多个脚本里手写字符串。
/// 
/// 新手理解：
/// 如果场景名写错，SceneManager.LoadScene 会找不到场景。
/// 所以把名字集中放在这里，其他脚本只引用 GameSceneNames.GameplayScene。
/// </summary>
public static class GameSceneNames
{
    /// <summary>
    /// 主玩法场景名。这个名字必须和 Build Settings 里的场景名一致。
    /// </summary>
    public const string GameplayScene = "MainScene";
}
