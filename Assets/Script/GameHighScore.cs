using UnityEngine;

/// <summary>
/// 历史最高分工具类。
/// 
/// 新手理解：
/// 1. static class 不需要挂到物体上，也不用 new。
/// 2. PlayerPrefs 是 Unity 自带的小型本地存储，适合保存最高分、音量设置这类简单数据。
/// 3. 这里统一负责读取和更新最高分，其他 UI 只需要调用它。
/// </summary>
public static class GameHighScore
{
    // PlayerPrefs 用字符串 key 来找数据；统一成常量可以避免拼写错误。
    private const string HighScoreKey = "HighScore";

    /// <summary>
    /// 读取历史最高分。
    /// 如果本地还没保存过，就默认返回 0。
    /// </summary>
    public static int GetHighScore()
    {
        // Mathf.Max 防止存档里出现负数，保证 UI 永远显示非负分数。
        return Mathf.Max(0, PlayerPrefs.GetInt(HighScoreKey, 0));
    }

    /// <summary>
    /// 尝试用本局分数刷新最高分。
    /// 返回 true 表示刷新成功，false 表示没有超过旧纪录。
    /// </summary>
    public static bool UpdateHighScore(int score)
    {
        // 先把传入分数修正为 >= 0，避免异常数据污染存档。
        int sanitizedScore = Mathf.Max(0, score);
        int currentHighScore = GetHighScore();
        if (sanitizedScore <= currentHighScore)
        {
            return false;
        }

        // SetInt 只是写入内存/等待保存，Save 会立刻写到本地。
        PlayerPrefs.SetInt(HighScoreKey, sanitizedScore);
        PlayerPrefs.Save();
        return true;
    }
}
