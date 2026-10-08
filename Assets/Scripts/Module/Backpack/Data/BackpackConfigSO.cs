/*
* ┌──────────────────────────────────┐
* │  描    述: 背包容器配置，网格尺寸、逐级解锁掩码、档位售价与暂存架容量
* │  类    名: BackpackConfigSO.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 解锁掩码逐级包含：后一级必须盖住前一级所有已解锁格，否则已放好的物品会凭空悬空
* 掩码位序与形状位图一致，位序 = 行 × 8 + 列，由网格绘制器点选而非手填
* 暂存架按件计不参与网格，所以它的格数与容器尺寸无关，只限件数
* 开局装备只列定义不带档位，新背包一律从一级起步
*/

using System.Collections.Generic;
using UnityEngine;
using Utils.log;

namespace Module.Backpack.Data
{
    [CreateAssetMenu(menuName = "FishGame/Config/Backpack Config", fileName = "BackpackConfig")]
    public sealed class BackpackConfigSO : ScriptableObject
    {
        // ==================== 常量 ====================
        public const int TIER_COUNT = 3;

        // ==================== 字段[外部设置] ====================
        [Header("容器网格")]
        [Tooltip("容器列数，上限受位图每行余量约束")]
        [SerializeField, Range(2, Model.ShapeMask.MAX_GRID_SIDE)] private int GridColumns = 5;
        [Tooltip("容器行数，上限受 long 符号位约束")]
        [SerializeField, Range(2, Model.ShapeMask.MAX_GRID_SIDE)] private int GridRows = 5;

        [Header("逐级解锁")]
        [Tooltip("索引 0 是开局已解锁的格，之后每买一块扩容板推进一级，用网格绘制器点选")]
        [SerializeField] private List<long> UnlockMaskList = new();
        [Tooltip("长度比解锁级数少 1，索引 i 是从第 i+1 级买到第 i+2 级的价钱")]
        [SerializeField] private List<int> ExpansionCostList = new();

        [Header("商品定价")]
        [Tooltip("一/二/三级装备售价，共 3 项")]
        [SerializeField] private List<int> TierPriceList = new() { 80, 200, 560 };

        [Header("暂存架")]
        [Tooltip("暂存架可放的件数，按件计不参与网格")]
        [SerializeField, Range(1, 12)] private int StashCapacityLimit = 5;

        [Header("开局装备")]
        [Tooltip("新背包开局就带着的装备，一律按一级放入，网格放不下自动退到暂存架")]
        [SerializeField] private List<BackpackItemSO> StartingItemList = new();

        // ==================== 属性 ====================
        public int Columns => GridColumns;
        public int Rows => GridRows;
        public int UnlockStageCount => UnlockMaskList.Count;
        public int StashCapacity => StashCapacityLimit;
        public IReadOnlyList<BackpackItemSO> StartingItems => StartingItemList;

        // ==================== Public Function ====================
        /// <summary>
        /// 取指定解锁阶段的掩码，阶段越界时钳到最后一阶段
        /// </summary>
        /// <param name="stage">解锁阶段，从 0 开始</param>
        public long UnlockMaskAt(int stage)
        {
            if (UnlockMaskList.Count == 0)
                return 0;

            return UnlockMaskList[Mathf.Clamp(stage, 0, UnlockMaskList.Count - 1)];
        }

        /// <summary>
        /// 取从当前阶段买下一级扩容板的价钱，已到顶时返回 0
        /// </summary>
        /// <param name="stage">当前解锁阶段，从 0 开始</param>
        public int ExpansionCostAt(int stage)
        {
            if (stage < 0 || stage >= ExpansionCostList.Count)
                return 0;

            return ExpansionCostList[stage];
        }

        /// <summary>
        /// 取指定档位的售价，档位越界时钳到 1 到 3
        /// </summary>
        /// <param name="tier">档位，从 1 开始</param>
        public int TierPrice(int tier)
        {
            int index = Mathf.Clamp(tier, 1, TIER_COUNT) - 1;
            return index < TierPriceList.Count ? TierPriceList[index] : 0;
        }

        // ==================== Private Function ====================
        private void OnValidate()
        {
            if (UnlockMaskList.Count == 0)
            {
                QLog.Error("背包配置没有任何解锁阶段，容器无格可用");
                return;
            }

            for (int stage = 0; stage < UnlockMaskList.Count; stage++)
            {
                string label = $"第 {stage + 1} 阶段解锁掩码";

                if (UnlockMaskList[stage] == 0)
                    QLog.Error($"{label} 为空");

                if ((ExpansionCostList.Count != UnlockMaskList.Count - 1) && stage == 0)
                    QLog.Error($"扩容板数量与解锁阶段不匹配：解锁 {UnlockMaskList.Count} 阶段，扩容板 {ExpansionCostList.Count} 块");

                validateMaskBounds(UnlockMaskList[stage], label);

                if (stage > 0 && (UnlockMaskList[stage - 1] & ~UnlockMaskList[stage]) != 0)
                    QLog.Error($"{label} 没有包含上一阶段的全部格，已放好的物品会悬空");
            }

            if (TierPriceList.Count != TIER_COUNT)
                QLog.Error($"售价必须配满 {TIER_COUNT} 档：当前 {TierPriceList.Count} 项");

            for (int i = 0; i < StartingItemList.Count; i++)
            {
                if (StartingItemList[i] == null)
                    QLog.Error($"开局装备第 {i + 1} 项为空引用，容器不会为它留位");
            }
        }

        // 掩码里不允许出现落在容器外的位，否则形状永远放不下且报错信息毫无参考价值
        private void validateMaskBounds(long mask, string label)
        {
            for (int bit = 0; bit < Model.ShapeMask.MAX_GRID_SIDE * Model.ShapeMask.ROW_STRIDE; bit++)
            {
                if ((mask & (1L << bit)) == 0)
                    continue;

                int column = bit % Model.ShapeMask.ROW_STRIDE;
                int row = bit / Model.ShapeMask.ROW_STRIDE;

                if (column >= GridColumns || row >= GridRows)
                {
                    QLog.Error($"{label} 含容器外的格：列 {column} 行 {row}，容器 {GridColumns}×{GridRows}");
                    return;
                }
            }
        }
    }
}
