using QFramework;
using UnityEngine;

/// <summary>
/// 技能模型测试脚本。
/// 只用于第二步测试 PlayerSkillModel 是否能正常学习和升级技能。
/// 测完可以从场景中移除。
/// </summary>
public class SkillModelTest : MonoBehaviour, IController
{
    public IArchitecture GetArchitecture()
    {
        return TreasureHunterArchitecture.Interface;
    }

    private void Start()
    {
        PlayerSkillModel skillModel = this.GetModel<PlayerSkillModel>();

        // 测试学习大火球。
        skillModel.LearnSkill(1001);

        // 测试升级大火球。
        skillModel.UpgradeSkill(1001);

        int level = skillModel.GetSkillLevel(1001);
        Debug.Log($"测试技能等级：大火球 Lv.{level}");

        // 测试技能选择次数。
        skillModel.AddPendingSkillSelection();
        Debug.Log($"当前待处理技能选择次数：{skillModel.PendingSkillSelectionCount}");
    }
}