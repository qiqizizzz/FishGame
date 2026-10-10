/*
* ┌──────────────────────────────────┐
* │  描    述: 合成规则总表，一条规则一行数组，判定与扣费侧只读这里
* │  类    名: CraftRuleSO.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 规则按存放物类别归一：一个类别只认第一条命中的规则，同类别挂两条会被 OnValidate 报出来
* 只有 MaxTier 大于 1 的定义才可能进合成，布料恒为单档，天然被这条筛掉，不必在代码里另写排除
* 表为空意味着合成整条链路不会响，配置阶段就该报错，不该等玩家点按钮才发现没反应
*/

using System.Collections.Generic;
using Module.Backpack.Data;
using UnityEngine;
using Utils.log;

namespace Module.Craft.Data
{
    [CreateAssetMenu(menuName = "FishGame/Config/Craft Rules", fileName = "CraftRules")]
    public sealed class CraftRuleSO : ScriptableObject
    {
        // ==================== 字段[外部设置] ====================
        [Header("规则")]
        [Tooltip("合成规则表，一条一行，本轮只放装备升档那一条")]
        [SerializeField] private List<CraftRule> RuleList = new();

        // ==================== 属性 ====================
        public IReadOnlyList<CraftRule> Rules => RuleList;

        // ==================== Public Function ====================
        /// <summary>
        /// 取这件存放物适用的合成规则，单档定义与没有规则覆盖的类别返回空
        /// </summary>
        /// <param name="def">存放物定义</param>
        public CraftRule ResolveRule(IBackpackDef def)
        {
            if (def == null || def.MaxTier <= 1)
                return null;

            for (int i = 0; i < RuleList.Count; i++)
            {
                CraftRule rule = RuleList[i];

                if (rule != null && rule.Kind == def.Kind)
                    return rule;
            }

            return null;
        }

        // ==================== Private Function ====================
        private void OnValidate()
        {
            if (RuleList.Count == 0)
                QLog.Error($"[{nameof(CraftRuleSO)}] 规则表是空的，合成整条链路不会响：{name}");

            for (int i = 0; i < RuleList.Count; i++)
            {
                CraftRule rule = RuleList[i];

                if (rule == null)
                {
                    QLog.Error($"[{nameof(CraftRuleSO)}] 规则表第 {i + 1} 项是空的：{name}");
                    continue;
                }

                if (rule.MaterialCount < 2)
                    QLog.Error($"[{nameof(CraftRuleSO)}] 「{rule.Label}」消耗件数至少要 2 件");

                if (rule.TierDelta < 1)
                    QLog.Error($"[{nameof(CraftRuleSO)}] 「{rule.Label}」产出档位差至少要升 1 档");

                for (int j = i + 1; j < RuleList.Count; j++)
                {
                    CraftRule other = RuleList[j];

                    if (other != null && other.Kind == rule.Kind)
                        QLog.Error($"[{nameof(CraftRuleSO)}] 类别 {rule.Kind} 挂了 {countSameKind(rule.Kind)} 条规则，只会认第一条，请删掉多余的");
                }
            }
        }

        // 同一类别挂了几条，报错时把条数一起报出来
        private int countSameKind(BackpackDefKind kind)
        {
            int count = 0;

            for (int i = 0; i < RuleList.Count; i++)
            {
                if (RuleList[i] != null && RuleList[i].Kind == kind)
                    count++;
            }

            return count;
        }
    }
}
