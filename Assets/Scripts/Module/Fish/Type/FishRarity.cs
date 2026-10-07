/*
 * ┌──────────────────────────────────┐
 * │  描    述: 鱼的价值档位枚举，颜色即信息的分档依据
 * │  类    名: FishRarity.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 * 与道具品质枚举是两套语义，禁止复用同一个枚举，靠映射表关联
 */

namespace Module.Fish.Type
{
    /// <summary>
    /// 鱼的价值档位，取值顺序即价值递增顺序
    /// </summary>
    public enum FishRarity
    {
        /// <summary>常见，灰白无边缘光，浅层成群</summary>
        Common = 0,

        /// <summary>少见，绿色淡边缘光</summary>
        Uncommon = 1,

        /// <summary>稀有，亮蓝边缘光</summary>
        Rare = 2,

        /// <summary>史诗，亮紫边缘光</summary>
        Epic = 3,

        /// <summary>传说，暖金边缘光加呼吸脉冲</summary>
        Legendary = 4,

        /// <summary>障碍（河豚），不参与色阶与加权抽取，碰即受罚</summary>
        Hazard = 5
    }
}
