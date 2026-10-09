/*
* ┌──────────────────────────────────┐
* │  描    述: 下潜属性包接口，下潜层与背包层之间的唯一接口
* │  类    名: IFishingStats.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 背包物品聚合出这个只读属性包，下潜层禁止读任何背包概念
* 接口先立起来是为了防止下潜层散落硬编码深度与容量常量，背包接上时零返工
* 只有「下游真的在读」的字段才有资格进 BUFF 词表，配了没人读的能力一律标为预留
*/

namespace Module.Fish.Model
{
    /// <summary>
    /// 单局下潜属性包，全部为只读，由背包聚合产出
    /// 实现侧约定：正向系数自行 clamp 到上限，越小越好的字段自行 clamp 到下限，
    /// 且不得为负，避免背包叠出负速度、负概率这类非法值
    /// </summary>
    public interface IFishingStats
    {
        /// <summary>可放线长度（米），钩子能到的最深处，是硬上限</summary>
        float LineLength { get; }

        /// <summary>收线速度（米/秒）</summary>
        float ReelSpeed { get; }

        /// <summary>下潜速度（米/秒），按住时的下沉快慢</summary>
        float DescendSpeed { get; }

        /// <summary>横向拖动限速（米/秒），钩子跟手的最大横速</summary>
        float LateralSpeed { get; }

        /// <summary>横向惯性收敛系数，越大越跟手、越小越飘</summary>
        float LateralDamping { get; }

        /// <summary>钩头碰撞半径（米），决定蹭鱼判定的宽度</summary>
        float HookRadius { get; }

        /// <summary>回收途中碰到河豚时钩上鱼的单条脱钩概率（0-1），越小越好</summary>
        float DetachChance { get; }

        /// <summary>碰到河豚后的缠绕僵直时长（秒），越小越好，实现侧下限 0.1</summary>
        float TangledDuration { get; }

        /// <summary>钩上可同时挂着的鱼条数上限，每条鱼一律占 1 个名额</summary>
        int MaxCarryCount { get; }

        /// <summary>稀有度权重放大系数，作用于加权随机表</summary>
        float LurePull { get; }

        /// <summary>卖鱼金币加成（0-1），实现侧锁上限 0.6</summary>
        float CoinBonus { get; }

        /// <summary>
        /// 张力上限，超过即断线
        /// 预留未接线：本版鱼线不会断，全工程无读取点，禁止配成商品 BUFF
        /// </summary>
        float TensionMax { get; }

        /// <summary>
        /// 断线前的缓冲窗口比例（0-1）
        /// 预留未接线：随断线玩法一起做，现阶段禁止配成商品 BUFF
        /// </summary>
        float TensionRelief { get; }

        /// <summary>
        /// 鱼显形半径（米），深水区关键
        /// 预留未接线：现有接触裁决走 HookRadius + 容差，显形裁剪尚未实现，禁止配成商品 BUFF
        /// </summary>
        float DetectRadius { get; }
    }
}
