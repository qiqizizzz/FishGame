/*
* ┌──────────────────────────────────┐
* │  描    述: 合成素材的配对口径，决定哪两件算同款可以合
* │  类    名: CraftMatchMode.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* SameIdAndTier 认「同一个 Id 加同一个档位」，装备升档走这条
* SameRarityTier 只认「同存放物类别加同一个档位」，不要求同 Id
* 后者是给将来的布料提稀留的口径：两块同稀有度的布不必同色同形也能合
*/

namespace Module.Craft.Data
{
    /// <summary>
    /// 合成素材的配对口径
    /// </summary>
    public enum CraftMatchMode
    {
        /// <summary>同 Id 且同档位才算同款，装备升档用这条</summary>
        SameIdAndTier = 0,

        /// <summary>同存放物类别且同档位即算同款，不看 Id，布料提稀用这条</summary>
        SameRarityTier = 1
    }
}
