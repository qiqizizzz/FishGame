/*
* ┌──────────────────────────────────┐
* │  描    述: 一局背包的运行态，网格模型与暂存架的持有者，装备与板块共用一条入手链路
* │  类    名: RunBackpack.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 配置资产全程只读，可变状态只在这里，重开一局就是换一个实例
* 网格放不下的物品退回暂存架，两边都满才判定拿不下，开局不该卡在报错上
* 自动落位按行主序扫锚点，同一锚点先试初始朝向，能少转一次就少转一次
*/

using System.Collections.Generic;
using Module.Backpack.Data;
using UnityEngine;
using Utils.log;

namespace Module.Backpack.Model
{
    public sealed class RunBackpack
    {
        // ==================== 常量 ====================
        private const int FIRST_TIER = 1;

        // ==================== 属性 ====================
        public BackpackModel Model => m_model;
        public IReadOnlyList<BackpackItemInstance> Stash => m_stash;

        // ==================== 字段[私有] ====================
        private readonly BackpackModel m_model = new();
        private readonly BackpackConfigSO m_config;
        private readonly List<BackpackItemInstance> m_stash = new();

        // ==================== Public Function ====================
        /// <summary>
        /// 按容器配置开一个空背包，解锁停在开局阶段
        /// </summary>
        /// <param name="config">背包容器配置</param>
        public RunBackpack(BackpackConfigSO config)
        {
            m_config = config;
            m_model.Configure(config.Columns, config.Rows, config.UnlockMaskAt(0));
        }

        /// <summary>
        /// 把配置里写死的开局装备依次入手，一律按一级算
        /// </summary>
        public void SeedStartingItems()
        {
            IReadOnlyList<BackpackItemSO> starting = m_config.StartingItems;

            for (int i = 0; i < starting.Count; i++)
                TryAddItem(starting[i], FIRST_TIER);
        }

        /// <summary>
        /// 入手一件装备，先往网格上放，放不下退暂存架，两边都满才返回 false
        /// </summary>
        /// <param name="definition">装备定义</param>
        /// <param name="tier">档位，从 1 开始</param>
        public bool TryAddItem(IBackpackDef definition, int tier)
        {
            if (definition == null || definition.MaxTier == 0)
            {
                QLog.Error("存放物定义没有可用档位，无法入手");
                return false;
            }

            BackpackItemInstance instance = new()
            {
                Definition = definition,
                Tier = Mathf.Clamp(tier, FIRST_TIER, definition.MaxTier)
            };

            if (tryFindAnchor(instance, out int column, out int row))
            {
                instance.AnchorColumn = column;
                instance.AnchorRow = row;

                if (m_model.TryPlace(instance))
                    return true;
            }

            if (m_stash.Count >= m_config.StashCapacity)
            {
                QLog.Error($"网格与暂存架都已满，装不下：{definition.Label}");
                return false;
            }

            m_stash.Add(instance);
            return true;
        }

        /// <summary>
        /// 把暂存架里的一件取回网格，网格放不下就原样留在架上
        /// </summary>
        /// <param name="stashIndex">暂存架下标</param>
        public bool TryRecallFromStash(int stashIndex)
        {
            if (stashIndex < 0 || stashIndex >= m_stash.Count)
                return false;

            BackpackItemInstance instance = m_stash[stashIndex];

            if (!tryFindAnchor(instance, out int column, out int row))
                return false;

            instance.AnchorColumn = column;
            instance.AnchorRow = row;

            if (!m_model.TryPlace(instance))
                return false;

            m_stash.RemoveAt(stashIndex);
            return true;
        }
        // ==================== Private Function ====================
        // 全表扫锚点取第一个放得下的位置，顺手把朝向定下来，行主序让物品先贴左上角
        private bool tryFindAnchor(BackpackItemInstance instance, out int anchorColumn, out int anchorRow)
        {
            ShapeMask[] orientations = instance.ShapeDef.Orientations;

            for (int row = 0; row < m_model.Rows; row++)
            {
                for (int column = 0; column < m_model.Columns; column++)
                {
                    for (int i = 0; i < orientations.Length; i++)
                    {
                        if (!m_model.CanPlace(orientations[i], column, row))
                            continue;

                        instance.OrientationIndex = i;
                        anchorColumn = column;
                        anchorRow = row;
                        return true;
                    }
                }
            }

            anchorColumn = 0;
            anchorRow = 0;
            return false;
        }
    }
}
