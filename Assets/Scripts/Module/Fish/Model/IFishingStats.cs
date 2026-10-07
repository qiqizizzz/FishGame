/*
* ┌──────────────────────────────────┐
* │  描    述: 下潜属性包接口，下潜层与背包层之间的唯一接口
* │  类    名: IFishingStats.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 背包物品聚合出这个只读属性包，下潜层禁止读任何背包概念
* 接口先立起来是为了防止下潜层散落硬编码深度与容量常量，背包接上时零返工
*/

namespace Module.Fish.Model
{
    /// <summary>
    /// 单局下潜属性包，全部为只读，由背包聚合产出
    /// </summary>
    public interface IFishingStats
    {
        /// <summary>可放线长度（米），钩子能到的最深处，是硬上限</summary>
        float LineLength { get; }

        /// <summary>收线速度（米/秒）</summary>
        float ReelSpeed { get; }

        /// <summary>张力上限，超过即断线</summary>
        float TensionMax { get; }

        /// <summary>断线前的缓冲窗口比例（0-1）</summary>
        float TensionRelief { get; }

        /// <summary>钩上可同时挂着的鱼条数上限，每条鱼一律占 1 个名额</summary>
        int MaxCarryCount { get; }

        /// <summary>稀有度权重放大系数，作用于加权随机表</summary>
        float LurePull { get; }

        /// <summary>鱼显形半径（米），深水区关键</summary>
        float DetectRadius { get; }

        /// <summary>卖鱼金币加成（0-1），实现侧锁上限 0.6</summary>
        float CoinBonus { get; }
    }
}