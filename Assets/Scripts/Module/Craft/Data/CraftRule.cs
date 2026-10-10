/*
* ┌──────────────────────────────────┐
* │  描    述: 一条合成规则的数据，管配对口径、消耗件数、产出档位差与手续费
* │  类    名: CraftRule.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 规则一律写在 CraftRuleSO 的数组里，判定侧只读这里，不在代码里硬编码品质链
* 产物的形状与数值不由本条规则决定，恒按定义的档位表取新档，所以这里没有形状模式字段
* CoinCost 是额外手续费，本轮定的是不收，素材本身就是成本，填 0 时链路直接跳过扣钱
*/

using System;
using Module.Backpack.Data;
using UnityEngine;

namespace Module.Craft.Data
{
    [Serializable]
    public sealed class CraftRule
    {
        // ==================== 字段[外部设置] ====================
        [Header("标识")]
        [Tooltip("规则名，只给编辑器与日志看，例如「装备升档」")]
        [SerializeField] private string RuleLabel = "新规则";

        [Header("配对")]
        [Tooltip("这条规则管哪一类存放物，装备与布料各一条")]
        [SerializeField] private BackpackDefKind InputKind = BackpackDefKind.Item;
        [Tooltip("同款判定口径：同 Id 同档位，还是只看同档位")]
        [SerializeField] private CraftMatchMode MatchMode = CraftMatchMode.SameIdAndTier;
        [Tooltip("吃掉几件产出 1 件，本轮定的是 2 合 1")]
        [SerializeField, Range(2, 5)] private int InputCount = 2;

        [Header("产出")]
        [Tooltip("产物档位 = 素材档位 + 本值，越过定义的最高档就不成对")]
        [SerializeField, Range(1, 2)] private int OutputTierDelta = 1;

        [Header("手续费")]
        [Tooltip("合成额外收的金币，0 表示不收，素材本身就是成本")]
        [SerializeField, Min(0)] private int CoinCost = 0;

        // ==================== 属性 ====================
        public string Label => RuleLabel;
        public BackpackDefKind Kind => InputKind;
        public CraftMatchMode Match => MatchMode;
        public int MaterialCount => InputCount < 2 ? 2 : InputCount;
        public int TierDelta => OutputTierDelta < 1 ? 1 : OutputTierDelta;
        public int Cost => CoinCost < 0 ? 0 : CoinCost;

        // ==================== Public Function ====================
        /// <summary>
        /// 按本条规则的配对口径判定这两件能不能凑成一组素材
        /// </summary>
        /// <param name="self">已选定的那件定义</param>
        /// <param name="selfTier">已选定那件的档位</param>
        /// <param name="other">待判定的另一件定义</param>
        /// <param name="otherTier">另一件的档位</param>
        public bool IsPair(IBackpackDef self, int selfTier, IBackpackDef other, int otherTier)
        {
            if (self == null || other == null || selfTier != otherTier)
                return false;

            return Match == CraftMatchMode.SameIdAndTier
                ? self.Id == other.Id
                : self.Kind == InputKind;
        }
    }
}
