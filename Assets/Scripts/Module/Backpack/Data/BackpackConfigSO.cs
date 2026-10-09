/*
* ┌──────────────────────────────────┐
* │  描    述: 背包容器配置，网格尺寸、开局布料、档位售价与暂存架容量
* │  类    名: BackpackConfigSO.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 可用格的全部来源是布料板块，配置里不再存在「先解锁一批格子」这种独立状态
* 开局那几格本身就由两三块布拼出来，和后期买布铺格走的是同一条链路
* 本表只引用布料总表，绝不复制一份布的数据，商店抽的布与开局铺的布永远是同一张表
* OnValidate 只判本资产内部看得见的内容，跨资产取数据一律留到编辑器工具与运行期
* 掩码位序与形状位图一致，位序 = 行 × 8 + 列，由网格绘制器点选而非手填
* 暂存架按件计不参与网格，所以它的件数上限与容器尺寸无关
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
        [SerializeField, Range(2, Model.ShapeMask.MAX_GRID_SIDE)] private int GridColumns = 6;
        [Tooltip("容器行数，上限受 long 符号位约束")]
        [SerializeField, Range(2, Model.ShapeMask.MAX_GRID_SIDE)] private int GridRows = 5;

        [Header("布料来源")]
        [Tooltip("开局布与商店布共用的一张布料总表，这里只引用不复制")]
        [SerializeField] private ClothCatalogSO ClothCatalog;

        [Header("开局布料")]
        [Tooltip("新背包开局就铺上的布，最初的格子全部由它们拼出来")]
        [SerializeField] private List<StartingClothEntry> StartingClothList = new();

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
        public int StashCapacity => StashCapacityLimit;
        public ClothCatalogSO ClothCatalogRef => ClothCatalog;
        public int StartingClothCount => StartingClothList.Count;
        public IReadOnlyList<StartingClothEntry> StartingCloths => StartingClothList;
        public IReadOnlyList<BackpackItemSO> StartingItems => StartingItemList;

        // ==================== Public Function ====================
        /// <summary>
        /// 取指定档位的售价，档位越界时钳到 1 到 3
        /// </summary>
        /// <param name="tier">档位，从 1 开始</param>
        public int TierPrice(int tier)
        {
            int index = Mathf.Clamp(tier, 1, TIER_COUNT) - 1;
            return index < TierPriceList.Count ? TierPriceList[index] : 0;
        }

        /// <summary>
        /// 把开局布料摊成一张可用格掩码，编辑器预览与烘焙工具都读这个结果
        /// </summary>
        public long BuildStartingMask()
        {
            long mask = 0L;

            if (ClothCatalog == null)
            {
                QLog.Error($"[{nameof(BackpackConfigSO)}] 没有引用布料总表，铺不出开局格子：{name}");
                return mask;
            }

            for (int i = 0; i < StartingClothList.Count; i++)
            {
                if (tryResolveStartingBits(StartingClothList[i], i, out long bits))
                    mask |= bits;
            }

            return mask;
        }

        /// <summary>
        /// 取开局某块布摊在容器上的位图，配置写错时返回 false 并当场报错
        /// </summary>
        /// <param name="index">开局布料列表里的下标</param>
        /// <param name="bits">该块布铺出来的格</param>
        public bool TryBuildStartingBits(int index, out long bits)
        {
            bits = 0L;

            if (ClothCatalog == null)
            {
                QLog.Error($"[{nameof(BackpackConfigSO)}] 没有引用布料总表，铺不出开局格子：{name}");
                return false;
            }

            if (index < 0 || index >= StartingClothList.Count)
                return false;

            return tryResolveStartingBits(StartingClothList[index], index, out bits);
        }

        /// <summary>
        /// 按参数算一块布摊在容器上的位图，编辑器读的是还没写回的序列化值，只能走这条
        /// </summary>
        /// <param name="clothId">布标识</param>
        /// <param name="shapeIndex">该块布异形列表里的下标</param>
        /// <param name="orientationIndex">异形的朝向下标</param>
        /// <param name="anchorColumn">锚点列</param>
        /// <param name="anchorRow">锚点行</param>
        /// <param name="bits">算出来的格</param>
        public bool TryBuildBits(string clothId, int shapeIndex, int orientationIndex, int anchorColumn, int anchorRow, out long bits)
        {
            bits = 0L;

            if (ClothCatalog == null)
            {
                QLog.Error($"[{nameof(BackpackConfigSO)}] 没有引用布料总表，铺不出格子：{name}");
                return false;
            }

            ClothPieceDef piece = ClothCatalog.ResolveByClothId(clothId, shapeIndex);

            if (piece == null)
                return false;

            Model.ShapeMask mask = piece.Shape.OrientationAt(orientationIndex);

            if (mask.CellCount == 0)
            {
                QLog.Error($"[{nameof(BackpackConfigSO)}] 布料的异形是空的：{clothId} 第 {shapeIndex} 个形状");
                return false;
            }

            bits = mask.Shifted(anchorColumn, anchorRow).Bits;
            return true;
        }

        // ==================== Private Function ====================
        private void OnValidate()
        {
            if (StartingClothList.Count == 0)
                QLog.Error($"[{nameof(BackpackConfigSO)}] 没有配任何开局布料，容器一格都不可用：{name}");

            if (ClothCatalog == null)
                QLog.Error($"[{nameof(BackpackConfigSO)}] 没有引用布料总表，开局布与商店布都取不到数据：{name}");

            if (TierPriceList.Count != TIER_COUNT)
                QLog.Error($"[{nameof(BackpackConfigSO)}] 售价必须配满 {TIER_COUNT} 档：当前 {TierPriceList.Count} 项");

            for (int i = 0; i < StartingClothList.Count; i++)
            {
                StartingClothEntry entry = StartingClothList[i];

                if (entry == null)
                    QLog.Error($"[{nameof(BackpackConfigSO)}] 开局布料第 {i + 1} 项是空的：{name}");
            }

            for (int i = 0; i < StartingItemList.Count; i++)
            {
                if (StartingItemList[i] == null)
                    QLog.Error($"[{nameof(BackpackConfigSO)}] 开局装备第 {i + 1} 项为空引用，容器不会为它留位");
            }
        }

        // 单条开局布换算成位图，报错原因由 TryBuildBits 里那句「总表里没有这块布」给出
        private bool tryResolveStartingBits(StartingClothEntry entry, int index, out long bits)
        {
            bits = 0L;

            if (entry == null)
                return false;

            if (string.IsNullOrEmpty(entry.Id))
            {
                QLog.Error($"[{nameof(BackpackConfigSO)}] 开局布料第 {index + 1} 项没填布标识：{name}");
                return false;
            }

            return TryBuildBits(entry.Id, entry.ShapeIndex, entry.OrientationIndex,
                entry.AnchorColumn, entry.AnchorRow, out bits);
        }
    }
}