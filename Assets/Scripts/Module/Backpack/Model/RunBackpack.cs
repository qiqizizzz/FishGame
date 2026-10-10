/*
* ┌──────────────────────────────────┐
* │  描    述: 一局背包的运行态，网格模型与暂存架的持有者，布料板块与装备共用一条入手链路
* │  类    名: RunBackpack.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 配置资产全程只读，可变状态只在这里，重开一局就是换一个实例
* 布负责铺出格子，装备负责压住格子，入手时按定义的 Kind 分流，两条链路各自找锚点
* 网格放不下的东西退回暂存架，两边都满才判定拿不下，开局不该卡在报错上
* 暂存中的板块既不铺格也不给 BUFF，所以拆布挤下来的装备和布本身都往架上放
* 挪布转布可能让装备失去底子，模型只挑出悬空件，接不住谁由这里决定
* 容量不够时模型整次不动，所以暂存架满等于这次挪不动，玩家看到的状态不会出现半成品
* 自动落位按行主序扫锚点，同一锚点先试初始朝向，能少转一次就少转一次
* 商店买入按玩家拖出来的落点入手，铺不开就整笔作废，不替玩家另找位置也不自动进架
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

        /// <summary>暂存架还剩几个位置，板块变更能不能挤装备下来全看这个数</summary>
        public int StashRemaining => Mathf.Max(0, m_config.StashCapacity - m_stash.Count);

        // ==================== 字段[私有] ====================
        private readonly BackpackModel m_model = new();
        private readonly BackpackConfigSO m_config;
        private readonly List<BackpackItemInstance> m_stash = new();
        private readonly List<BackpackItemInstance> m_orphans = new();

        // ==================== Public Function ====================
        /// <summary>
        /// 按容器配置开一个空背包，此时一格都不可用，格子全靠布铺出来
        /// </summary>
        /// <param name="config">背包容器配置</param>
        public RunBackpack(BackpackConfigSO config)
        {
            m_config = config;
            m_model.Configure(config.Columns, config.Rows);
        }

        /// <summary>
        /// 把配置里写死的开局布料依次铺上网格，最早的格子就是这么拼出来的
        /// </summary>
        public void SeedStartingCloths()
        {
            ClothCatalogSO catalog = m_config.ClothCatalogRef;

            if (catalog == null)
            {
                QLog.Error($"背包容器配置没有引用布料总表，铺不出开局格子：{m_config.name}");
                return;
            }

            IReadOnlyList<StartingClothEntry> cloths = m_config.StartingCloths;

            for (int i = 0; i < cloths.Count; i++)
            {
                StartingClothEntry entry = cloths[i];

                if (entry == null)
                {
                    QLog.Error($"开局布料第 {i + 1} 项是空的：{m_config.name}");
                    continue;
                }

                ClothPieceDef piece = catalog.ResolveByClothId(entry.Id, entry.ShapeIndex);

                if (piece == null)
                    continue;

                BackpackItemInstance plate = new()
                {
                    Definition = piece,
                    Tier = FIRST_TIER,
                    OrientationIndex = entry.OrientationIndex,
                    AnchorColumn = entry.AnchorColumn,
                    AnchorRow = entry.AnchorRow
                };

                if (m_model.TryPlacePlate(plate))
                    continue;

                if (tryFindPlateAnchor(plate, out int column, out int row))
                {
                    plate.AnchorColumn = column;
                    plate.AnchorRow = row;

                    if (m_model.TryPlacePlate(plate))
                    {
                        QLog.Error($"开局布料写的位置摆不下，已自动挪到列 {column} 行 {row}：{entry.Id}");
                        continue;
                    }
                }

                QLog.Error($"开局布料在这面容器里放不下，开局格子会缺：{entry.Id} 共 {piece.Shape.CellCount} 格");
            }
        }

        /// <summary>
        /// 把配置里写死的开局装备依次入手，一律按一级算，得先有布铺出的格才压得住
        /// </summary>
        public void SeedStartingItems()
        {
            IReadOnlyList<BackpackItemSO> starting = m_config.StartingItems;

            for (int i = 0; i < starting.Count; i++)
                TryAddItem(starting[i], FIRST_TIER);
        }

        /// <summary>
        /// 入手一件存放物，布走板块层装备走物品层，放不下退暂存架，两边都满才返回 false
        /// </summary>
        /// <param name="definition">存放物定义</param>
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

            bool isPlate = definition.Kind == BackpackDefKind.Cloth;

            bool found = isPlate
                ? tryFindPlateAnchor(instance, out int column, out int row)
                : tryFindAnchor(instance, out column, out row);

            if (found)
            {
                instance.AnchorColumn = column;
                instance.AnchorRow = row;

                bool placed = isPlate ? m_model.TryPlacePlate(instance) : m_model.TryPlace(instance);

                if (placed)
                    return true;
            }

            return pushToStash(instance);
        }

        /// <summary>
        /// 按玩家拖出来的落点入手一件存放物，铺不开就整笔作废，既不另找位置也不退进暂存架
        /// </summary>
        /// <param name="definition">存放物定义</param>
        /// <param name="tier">档位，从 1 开始</param>
        /// <param name="anchorColumn">落点包围盒左上角列</param>
        /// <param name="anchorRow">落点包围盒左上角行</param>
        public bool TryAddItemAt(IBackpackDef definition, int tier, int anchorColumn, int anchorRow)
        {
            if (definition == null || definition.MaxTier == 0)
            {
                QLog.Error("存放物定义没有可用档位，无法入手");
                return false;
            }

            BackpackItemInstance instance = new()
            {
                Definition = definition,
                Tier = Mathf.Clamp(tier, FIRST_TIER, definition.MaxTier),
                AnchorColumn = anchorColumn,
                AnchorRow = anchorRow
            };

            return definition.Kind == BackpackDefKind.Cloth
                ? m_model.TryPlacePlate(instance)
                : m_model.TryPlace(instance);
        }

        /// <summary>
        /// 把一件存放物直接买进暂存架，架子满了安静返回 false，不替玩家挑别的落点
        /// </summary>
        /// <param name="definition">存放物定义</param>
        /// <param name="tier">档位，从 1 开始</param>
        public bool TryAddItemToStash(IBackpackDef definition, int tier)
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

            return tryPushToStash(instance);
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
            bool isPlate = instance.Definition.Kind == BackpackDefKind.Cloth;

            bool found = isPlate
                ? tryFindPlateAnchor(instance, out int column, out int row)
                : tryFindAnchor(instance, out column, out row);

            if (!found)
                return false;

            instance.AnchorColumn = column;
            instance.AnchorRow = row;

            bool placed = isPlate ? m_model.TryPlacePlate(instance) : m_model.TryPlace(instance);

            if (!placed)
                return false;

            m_stash.RemoveAt(stashIndex);
            return true;
        }

        /// <summary>
        /// 把暂存架里的一件按指定锚点摆回网格，摆不下就原样留在架上
        /// </summary>
        /// <param name="stashIndex">暂存架下标</param>
        /// <param name="anchorColumn">落点包围盒左上角列</param>
        /// <param name="anchorRow">落点包围盒左上角行</param>
        public bool TryRecallFromStashAt(int stashIndex, int anchorColumn, int anchorRow)
        {
            if (stashIndex < 0 || stashIndex >= m_stash.Count)
                return false;

            BackpackItemInstance instance = m_stash[stashIndex];
            bool isPlate = instance.Definition.Kind == BackpackDefKind.Cloth;

            // 摆不成只是留了个没落地的锚点，两条放置入口都会自己重算，不必回滚
            instance.AnchorColumn = anchorColumn;
            instance.AnchorRow = anchorRow;

            bool placed = isPlate ? m_model.TryPlacePlate(instance) : m_model.TryPlace(instance);

            if (!placed)
                return false;

            m_stash.RemoveAt(stashIndex);
            return true;
        }

        /// <summary>
        /// 把网格上的一件装备收进暂存架，装备不托着别的东西，摘走不会有谁悬空
        /// </summary>
        /// <param name="item">待收装备</param>
        public bool TryRemoveItemToStash(BackpackItemInstance item)
        {
            if (item == null)
                return false;

            if (StashRemaining < 1)
            {
                QLog.Error($"暂存架已满，收不下：{item.Definition.Label}");
                return false;
            }

            if (!m_model.Remove(item))
                return false;

            m_stash.Add(item);
            return true;
        }

        /// <summary>
        /// 把一件装备从网格摘走并直接丢弃，出售走这条，装备不进暂存架
        /// </summary>
        /// <param name="item">待摘装备</param>
        public bool TryRemoveItem(BackpackItemInstance item)
        {
            return item != null && m_model.Remove(item);
        }

        /// <summary>
        /// 把一块布挪到新的锚点，失去底子的装备自动退到暂存架
        /// </summary>
        /// <param name="plate">待移板块</param>
        /// <param name="anchorColumn">新包围盒左上角列</param>
        /// <param name="anchorRow">新包围盒左上角行</param>
        public bool TryMovePlate(BackpackItemInstance plate, int anchorColumn, int anchorRow)
        {
            m_orphans.Clear();

            if (!m_model.TryMovePlate(plate, anchorColumn, anchorRow, StashRemaining, m_orphans))
                return false;

            acceptOrphans();
            return true;
        }

        /// <summary>
        /// 原地转一档布的朝向，压着的装备跟着让位，退到暂存架
        /// </summary>
        /// <param name="plate">待转板块</param>
        public bool TryRotatePlate(BackpackItemInstance plate)
        {
            m_orphans.Clear();

            if (!m_model.TryRotatePlate(plate, StashRemaining, m_orphans))
                return false;

            acceptOrphans();
            return true;
        }

        /// <summary>
        /// 把一块布从网格拆进暂存架，拆完这块布自己也占架上的一格
        /// </summary>
        /// <param name="plate">待拆板块</param>
        public bool TryRemovePlateToStash(BackpackItemInstance plate)
        {
            if (plate == null)
                return false;

            // 这块布本身也要占一格，所以它能挤下来的装备只剩一格余量
            if (StashRemaining < 1)
            {
                QLog.Error($"暂存架已满，拆不下这块布：{plate.Definition.Label}");
                return false;
            }

            m_orphans.Clear();

            if (!m_model.TryRemovePlate(plate, StashRemaining - 1, m_orphans))
                return false;

            acceptOrphans();
            m_stash.Add(plate);
            return true;
        }

        /// <summary>
        /// 把一块布从网格拆走并直接丢弃，出售走这条，布不进暂存架
        /// </summary>
        /// <param name="plate">待拆板块</param>
        public bool TryRemovePlate(BackpackItemInstance plate)
        {
            if (plate == null)
                return false;

            m_orphans.Clear();

            if (!m_model.TryRemovePlate(plate, StashRemaining, m_orphans))
                return false;

            acceptOrphans();
            return true;
        }

        // ==================== Private Function ====================
        // 模型预演过的悬空件一次性收进暂存架，容量在调用前就报给了模型，这里只会收下
        private void acceptOrphans()
        {
            for (int i = 0; i < m_orphans.Count; i++)
                m_stash.Add(m_orphans[i]);

            m_orphans.Clear();
        }

        // 开局与自动落位那条链路兜不住位置，架子真满了要报出来
        private bool pushToStash(BackpackItemInstance instance)
        {
            if (!tryPushToStash(instance))
            {
                QLog.Error($"网格与暂存架都已满，装不下：{instance.Definition.Label}");
                return false;
            }

            return true;
        }

        // 商店那条链路由调用方自己报「架子没空位」，这里只回成不成，不替它喊
        private bool tryPushToStash(BackpackItemInstance instance)
        {
            if (m_stash.Count >= m_config.StashCapacity)
                return false;

            m_stash.Add(instance);
            return true;
        }

        // 板块层找锚点，判定只看会不会压到别的布，装备在上头不算冲突
        private bool tryFindPlateAnchor(BackpackItemInstance instance, out int anchorColumn, out int anchorRow)
        {
            ShapeMask[] orientations = instance.ShapeDef.Orientations;

            for (int row = 0; row < m_model.Rows; row++)
            {
                for (int column = 0; column < m_model.Columns; column++)
                {
                    for (int i = 0; i < orientations.Length; i++)
                    {
                        if (!m_model.CanPlacePlate(orientations[i], column, row))
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

        // 物品层找锚点，格得是布铺出来的且没被别的装备压着
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
