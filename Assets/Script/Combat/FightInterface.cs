/// <summary>
/// 所有“能被攻击的东西”都要实现这个接口。
/// 
/// 新手理解：
/// 1. interface 只规定“必须有什么方法”，不写具体怎么扣血。
/// 2. 玩家武器、怪物攻击、子弹不需要关心打到的是史莱姆、金库还是测试方块，
///    只要对方实现了 FighterInterface，就可以统一调用 Hit。
/// 3. 每个被攻击对象自己决定 Hit 里要做什么，比如扣血、缩小、死亡、播放特效等。
/// </summary>
public interface FighterInterface
{
    /// <summary>
    /// 被攻击时调用。
    /// AtkPower 表示这次攻击传进来的伤害数值。
    /// </summary>
    public void Hit(int AtkPower);
}
