/*
* ┌──────────────────────────────────┐
* │  描    述: 常量属性包实现，P0 阶段直接回读钓竿配置基准值
* │  类    名: DefaultFishingStats.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 策略模式的最小实现：P1 背包接上后换成聚合实现，下潜层一行不改
* 数值全部来自 HookConfigSO，代码里不出现任何玩法常量
*/

using Module.Fish.Data;

namespace Module.Fish.Model
{
    /// <summary>
    /// 未叠加任何背包加成时的基准属性包
    /// </summary>
    public sealed class DefaultFishingStats : IFishingStats
    {
        private readonly HookConfigSO m_config;

        public DefaultFishingStats(HookConfigSO config)
        {
            m_config = config;
        }

        public float LineLength => m_config.LineLength;
        public int CarryCapacity => m_config.CarryCapacity;
        public float ReelSpeed => m_config.ReelSpeed;
        public float TensionMax => m_config.TensionMax;
        public float TensionRelief => m_config.TensionRelief;
        public int HookCount => m_config.HookCount;
        public float LurePull => m_config.LurePull;
        public float DetectRadius => m_config.DetectRadius;
        public float CoinBonus => m_config.CoinBonus;
    }
}