/*
* ┌──────────────────────────────────┐
* │  描    述: 合成服务，按规则表在背包与暂存架里配对素材并执行一笔合成
* │  类    名: CraftService.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 只读规则表与实例状态，不持有背包也不持有金币，谁调用谁把容器传进来
* 配对口径全在 CraftRule 里，这里不加任何品质链与档位的硬编码
* 单次扫描不级联：四件一级只会规划出两组二级，不会顺手把两组再合成三级
* 留哪件有优先级：在网格上的优先留下当落点基准，其次占格多的，再按锚点靠上靠左
* 素材可以一边在网格一边在暂存架，配对只看定义与档位，不看它此刻躺在哪
* 一笔执行的落位与回滚全在 RunBackpack.TryMergeItems 里，这里只管扣钱与退钱
*/

using System.Collections.Generic;
using Module.Backpack.Data;
using Module.Backpack.Model;
using Module.Craft.Data;
using Module.Craft.Model;
using Module.Fish.Model;
using Utils.log;

namespace Module.Craft.Service
{
    public sealed class CraftService
    {
        // ==================== 字段[私有] ====================
        private readonly CraftRuleSO m_rules;
        private readonly HashSet<int> m_used = new();
        private readonly List<BackpackItemInstance> m_materialScratch = new(1);

        // ==================== Public Function ====================
        /// <summary>
        /// 挂上规则表开一个合成服务，表为空时本服务永远配不出组
        /// </summary>
        /// <param name="rules">合成规则总表</param>
        public CraftService(CraftRuleSO rules)
        {
            m_rules = rules;

            if (m_rules == null)
                QLog.Error("没有挂上合成规则表，合成按钮不会有任何产出");
        }

        /// <summary>
        /// 这件定义有没有适用的合成规则，界面拿它决定要不要画可合成的提示
        /// </summary>
        /// <param name="definition">存放物定义</param>
        public bool IsCraftable(IBackpackDef definition) => ResolveRule(definition) != null;

        /// <summary>
        /// 取这件定义合成一次要付的手续费，没有规则时为 0
        /// </summary>
        /// <param name="definition">存放物定义</param>
        public int CoinCostOf(IBackpackDef definition)
        {
            CraftRule rule = ResolveRule(definition);
            return rule == null ? 0 : rule.Cost;
        }

        /// <summary>
        /// 数一数当前能凑出几组合成，只要个数时用这条，省掉建配对对象的开销
        /// </summary>
        /// <param name="pool">候选池，约定前 gridItemCount 件在网格上，其余在暂存架</param>
        /// <param name="gridItemCount">网格里有多少件，排在池子最前面</param>
        public int CountMerges(IReadOnlyList<BackpackItemInstance> pool, int gridItemCount)
            => PlanMerges(pool, gridItemCount, null);

        /// <summary>
        /// 规划本轮可执行的合成配对，结果追加进 result 并返回组数，单次扫描不级联
        /// </summary>
        /// <param name="pool">候选池，约定前 gridItemCount 件在网格上，其余在暂存架</param>
        /// <param name="gridItemCount">网格里有多少件，排在池子最前面</param>
        /// <param name="result">要追加配对的列表，允许为空表示只要个数</param>
        public int PlanMerges(IReadOnlyList<BackpackItemInstance> pool, int gridItemCount, List<CraftPair> result)
        {
            if (pool == null || pool.Count < 2)
                return 0;

            m_used.Clear();
            int count = 0;

            for (int i = 0; i < pool.Count; i++)
            {
                if (m_used.Contains(i))
                    continue;

                BackpackItemInstance self = pool[i];
                CraftRule rule = self == null ? null : ResolveRule(self.Definition);

                if (rule == null)
                    continue;

                int outputTier = self.Tier + rule.TierDelta;

                if (outputTier > self.Definition.MaxTier)
                    continue;

                int best = findMaterial(pool, i, self, rule);

                if (best < 0)
                    continue;

                // 同档时谁留下谁被吃只影响落点基准，档位不同不会影响配对成立
                int keptIndex = preferKept(pool, gridItemCount, i, best) ? i : best;
                int materialIndex = keptIndex == i ? best : i;

                m_used.Add(keptIndex);
                m_used.Add(materialIndex);

                if (result != null)
                {
                    result.Add(new CraftPair
                    {
                        Kept = pool[keptIndex],
                        Material = pool[materialIndex],
                        OutputTier = outputTier,
                        CoinCost = rule.Cost,
                        RuleLabel = rule.Label
                    });
                }

                count++;
            }

            return count;
        }

        /// <summary>
        /// 执行一笔合成，先扣手续费再落位，落不成把钱退回去
        /// </summary>
        /// <param name="pair">已规划好的配对</param>
        /// <param name="backpack">当前这局的背包</param>
        /// <param name="run">当前这局的游戏状态，扣费与退费都记在它身上</param>
        public bool TryCraft(CraftPair pair, RunBackpack backpack, RunState run)
        {
            if (pair?.Kept == null || pair.Material == null || backpack == null)
                return false;

            if (pair.CoinCost > 0)
            {
                if (run == null || !run.TrySpend(pair.CoinCost))
                {
                    QLog.Info($"合成「{pair.Kept.Definition.Label}」要 {pair.CoinCost} 金币，钱不够");
                    return false;
                }
            }

            m_materialScratch.Clear();
            m_materialScratch.Add(pair.Material);

            if (backpack.TryMergeItems(pair.Kept, m_materialScratch, pair.OutputTier))
                return true;

            if (pair.CoinCost > 0 && run != null)
                run.AddCoins(pair.CoinCost);

            return false;
        }

        // ==================== Private Function ====================
        // 规则表里找这件定义适用的那条，单档定义一律不给规则
        private CraftRule ResolveRule(IBackpackDef definition)
        {
            if (m_rules == null || definition == null)
                return null;

            return m_rules.ResolveRule(definition);
        }

        // 从 i 之后找第一件还没被占用又能配成同款的素材，先配到谁就用谁
        private int findMaterial(IReadOnlyList<BackpackItemInstance> pool, int selfIndex,
            BackpackItemInstance self, CraftRule rule)
        {
            for (int j = selfIndex + 1; j < pool.Count; j++)
            {
                if (m_used.Contains(j))
                    continue;

                BackpackItemInstance other = pool[j];

                if (other?.Definition == null)
                    continue;

                if (rule.IsPair(self.Definition, self.Tier, other.Definition, other.Tier))
                    return j;
            }

            return -1;
        }

        // 谁留下当落点基准：先看在不在网格上，再看占格多不多，最后按锚点靠上靠左
        private bool preferKept(IReadOnlyList<BackpackItemInstance> pool, int gridItemCount, int a, int b)
        {
            BackpackItemInstance first = pool[a];
            BackpackItemInstance second = pool[b];
            bool firstOnGrid = a < gridItemCount;
            bool secondOnGrid = b < gridItemCount;

            if (firstOnGrid != secondOnGrid)
                return firstOnGrid;

            if (first.Cells != second.Cells)
                return first.Cells > second.Cells;

            if (first.AnchorRow != second.AnchorRow)
                return first.AnchorRow < second.AnchorRow;

            return first.AnchorColumn <= second.AnchorColumn;
        }
    }
}
