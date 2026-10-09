/*
* ┌──────────────────────────────────┐
* │  描    述: 商店服务，按权重抽货架、算售价、扣金币并把入手的东西交给背包
* │  类    名: ShopService.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 抽商品分三步：先抽类别，是装备再抽档位并从装备池抽款式，是布料则先按稀有度权重抽一块布再等概率剪一个异形
* 售价基准只有一个来源，装备取背包配置的档位价，布料按「单格基准价 × 占格数 × 稀有度系数」现算，两边都只再乘一次浮动
* 稀有度同时驱动两件事：越稀有的布抽中权重越低、价钱越高，两个数都写在总表的同一行里
* 回收价与上架价同一条公式，只差最后一次浮动与回收比例，两处不会算出两个数
* 扣钱与入手是分开的两步，入手失败原额退回，不会出现钱花了东西没影
* 刷新价钱只在服务内部按次数累进，重开一局换新实例即回到起始价
*/

using System;
using System.Collections.Generic;
using Module.Backpack.Data;
using Module.Backpack.Model;
using Module.Fish.Model;
using Module.Fish.Service;
using Module.Shop.Data;
using Module.Shop.Model;
using UnityEngine;
using Utils.log;

namespace Module.Shop.Service
{
    public sealed class ShopService
    {
        // ==================== 常量 ====================
        private const int FIRST_TIER = 1;
        private const int MIN_PRICE = 1;
        private const int DEDUPE_MAX_ATTEMPTS = 8;

        // ==================== 类型 ====================
        private enum GoodsCategory
        {
            Item,
            Cloth,
            BlankCloth
        }

        // ==================== 属性 ====================
        /// <summary>当前货架上的商品，长度等于配置的货架容量</summary>
        public IReadOnlyList<ShopStockEntry> Stock => m_stock;

        /// <summary>下一次刷新货架要花的金币</summary>
        public int RefreshCost { get; private set; }

        // ==================== 字段[私有] ====================
        private readonly ShopConfigSO m_shopConfig;
        private readonly BackpackConfigSO m_backpackConfig;
        private readonly ClothCatalogSO m_catalog;
        private readonly System.Random m_random;
        private readonly List<ShopStockEntry> m_stock = new();
        private readonly WeightedTable<GoodsCategory> m_categoryTable = new();
        private readonly WeightedTable<BackpackItemSO> m_itemTable = new();
        private readonly WeightedTable<int> m_buffClothTable = new();
        private readonly WeightedTable<int> m_blankClothTable = new();
        private readonly WeightedTable<int> m_tierTable = new();
        private readonly HashSet<string> m_takenKeys = new();
        private int m_refreshCount;

        // ==================== Constructor ====================
        /// <summary>
        /// 开一间店，立刻抽一次货架
        /// </summary>
        /// <param name="shopConfig">抽取权重与定价曲线来源</param>
        /// <param name="backpackConfig">装备档位价来源</param>
        /// <param name="seed">随机种子，同一种子必得同一货架</param>
        public ShopService(ShopConfigSO shopConfig, BackpackConfigSO backpackConfig, int seed)
        {
            m_shopConfig = shopConfig;
            m_backpackConfig = backpackConfig;
            m_catalog = shopConfig.Catalog;
            m_random = new System.Random(seed);
            RefreshCost = shopConfig.RefreshBaseCostValue;

            buildTables();
            RollStock();
        }

        // ==================== Public Function ====================
        /// <summary>
        /// 重抽一整面货架，已售标记与刷新次数一并复位
        /// </summary>
        public void RollStock()
        {
            m_stock.Clear();
            m_takenKeys.Clear();

            for (int i = 0; i < m_shopConfig.GoodsCount; i++)
                tryAppendEntry();
        }

        /// <summary>
        /// 花金币重开一面货架，钱不够时不动货架也不涨价
        /// </summary>
        /// <param name="run">本局运行态，扣钱走它</param>
        public bool TryRefresh(RunState run)
        {
            if (run == null)
                return false;

            if (!run.TrySpend(RefreshCost))
                return false;

            m_refreshCount++;
            RefreshCost = resolveRefreshCost();
            RollStock();

            return true;
        }

        /// <summary>
        /// 买走货架上的一件，扣钱、入手、标记售出，任何一步失败都原额退回
        /// </summary>
        /// <param name="entry">要买的商品条目</param>
        /// <param name="run">本局运行态，扣钱走它</param>
        /// <param name="backpack">本局背包，入手走它</param>
        public ShopBuyResult TryBuy(ShopStockEntry entry, RunState run, RunBackpack backpack)
        {
            if (entry == null || entry.Definition == null)
                return ShopBuyResult.NotFound;

            if (entry.IsSold)
                return ShopBuyResult.Sold;

            if (run == null || backpack == null)
                return ShopBuyResult.NotFound;

            if (!run.TrySpend(entry.Price))
                return ShopBuyResult.NotEnoughCoins;

            if (!backpack.TryAddItem(entry.Definition, entry.Tier))
            {
                run.AddCoins(entry.Price);
                return ShopBuyResult.BackpackFull;
            }

            entry.MarkSold();
            return ShopBuyResult.Success;
        }

        /// <summary>
        /// 折算一块布的回收价，按上架的基准价乘回收比例，随机浮动不参与
        /// </summary>
        /// <param name="definition">要卖的存放物定义，只有布料板块算得出价钱</param>
        public int SellPriceOf(IBackpackDef definition)
        {
            if (definition is not ClothPieceDef piece || piece.Cloth == null || piece.Shape == null)
                return 0;

            // 只挂装备池的配置可以不填总表，这时货架上根本不会有布，这条链路也就问不到这里
            if (m_catalog == null)
                return 0;

            int basePrice = Mathf.RoundToInt(piece.Cloth.CellBasePriceValue * piece.Shape.CellCount
                                            * m_catalog.PriceScaleOf(piece.Cloth.RarityIndex));

            return Mathf.Max(MIN_PRICE, Mathf.RoundToInt(basePrice * m_shopConfig.SellRatio));
        }

        // ==================== Private Function ====================
        // 奖池只在开店时摊平成权重表，之后每次抽取都只查表，总表改动要重开一局才生效
        private void buildTables()
        {
            IReadOnlyList<BackpackItemSO> items = m_shopConfig.Items;

            for (int i = 0; i < items.Count; i++)
            {
                BackpackItemSO item = items[i];

                if (item == null)
                    continue;

                m_itemTable.Add(item, 1f);
            }

            buildClothTables();

            for (int tier = FIRST_TIER; tier <= ShopConfigSO.TIER_COUNT; tier++)
                m_tierTable.Add(tier, m_shopConfig.TierWeightAt(tier));

            m_categoryTable.Add(GoodsCategory.Item, m_shopConfig.ItemWeight);
            m_categoryTable.Add(GoodsCategory.Cloth, m_shopConfig.ClothWeight);
            m_categoryTable.Add(GoodsCategory.BlankCloth, m_shopConfig.BlankClothWeight);

            if (m_itemTable.Count == 0)
                dropCategory(GoodsCategory.Item);

            if (m_buffClothTable.Count == 0)
                dropCategory(GoodsCategory.Cloth);

            if (m_blankClothTable.Count == 0)
                dropCategory(GoodsCategory.BlankCloth);

            if (m_categoryTable.Count == 0)
                QLog.Error($"[{nameof(ShopService)}] 装备池与布料总表都抽不出东西，货架会全程空着");
        }

        // 一块布进哪个池只看它挂没挂 BUFF，权重取它那一档稀有度的抽中权重
        private void buildClothTables()
        {
            if (m_catalog == null)
                return;

            int clothCount = m_catalog.ClothCount;

            for (int i = 0; i < clothCount; i++)
            {
                ClothEntry cloth = m_catalog.ClothAt(i);

                if (cloth == null || cloth.ShapeCount == 0)
                    continue;

                float weight = m_catalog.DrawWeightOf(cloth.RarityIndex);

                if (cloth.IsBlank)
                    m_blankClothTable.Add(i, weight);
                else
                    m_buffClothTable.Add(i, weight);
            }
        }

        // 池子空了的类别不该继续占权重，否则每次抽到它都只能白跑一趟
        private void dropCategory(GoodsCategory category)
        {
            float weight = category switch
            {
                GoodsCategory.Item => m_shopConfig.ItemWeight,
                GoodsCategory.Cloth => m_shopConfig.ClothWeight,
                _ => m_shopConfig.BlankClothWeight
            };

            if (weight <= 0f)
                return;

            rebuildCategoryTableWithout(category);
        }

        private void rebuildCategoryTableWithout(GoodsCategory excluded)
        {
            m_categoryTable.Clear();

            if (excluded != GoodsCategory.Item && m_itemTable.Count > 0)
                m_categoryTable.Add(GoodsCategory.Item, m_shopConfig.ItemWeight);

            if (excluded != GoodsCategory.Cloth && m_buffClothTable.Count > 0)
                m_categoryTable.Add(GoodsCategory.Cloth, m_shopConfig.ClothWeight);

            if (excluded != GoodsCategory.BlankCloth && m_blankClothTable.Count > 0)
                m_categoryTable.Add(GoodsCategory.BlankCloth, m_shopConfig.BlankClothWeight);
        }

        // 先抽类别再抽款式，同一面货架尽量不撞同款，撞够次数就作罢，别让空池子卡死上架
        private void tryAppendEntry()
        {
            for (int attempt = 0; attempt < DEDUPE_MAX_ATTEMPTS; attempt++)
            {
                if (!m_categoryTable.TrySample(nextRoll01(), out GoodsCategory category))
                    return;

                ShopStockEntry entry = category switch
                {
                    GoodsCategory.Item => rollItem(),
                    GoodsCategory.Cloth => rollCloth(m_buffClothTable),
                    _ => rollCloth(m_blankClothTable)
                };

                if (entry == null)
                    continue;

                if (attempt + 1 < DEDUPE_MAX_ATTEMPTS && m_takenKeys.Contains(buildKey(entry)))
                    continue;

                m_takenKeys.Add(buildKey(entry));
                m_stock.Add(entry);
                return;
            }
        }

        private ShopStockEntry rollItem()
        {
            if (!m_itemTable.TrySample(nextRoll01(), out BackpackItemSO item))
                return null;

            if (!m_tierTable.TrySample(nextRoll01(), out int tier))
                tier = FIRST_TIER;

            return new ShopStockEntry(item, tier, applyJitter(m_backpackConfig.TierPrice(tier)));
        }

        // 抽布只看稀有度权重，抽到之后剪成哪种异形是等概率，异形本身不参与稀有度
        private ShopStockEntry rollCloth(WeightedTable<int> table)
        {
            if (!table.TrySample(nextRoll01(), out int clothIndex))
                return null;

            ClothEntry cloth = m_catalog.ClothAt(clothIndex);

            if (cloth == null || cloth.ShapeCount == 0)
                return null;

            int shapeCount = cloth.ShapeCount;
            int shapeIndex = Mathf.Clamp((int) (nextRoll01() * shapeCount), 0, shapeCount - 1);

            ClothPieceDef piece = m_catalog.Resolve(clothIndex, shapeIndex);

            if (piece == null)
                return null;

            int basePrice = Mathf.RoundToInt(cloth.CellBasePriceValue * piece.Shape.CellCount *
                                             m_catalog.PriceScaleOf(cloth.RarityIndex));

            return new ShopStockEntry(piece, piece.MaxTier, applyJitter(basePrice));
        }

        private static string buildKey(ShopStockEntry entry)
        {
            return $"{entry.Definition.Id}#{entry.Tier}";
        }

        // 浮动只在基准价上乘一次，装备与布料共用同一个比例，两边不会出现两套手感
        private int applyJitter(int basePrice)
        {
            if (basePrice <= 0)
                return MIN_PRICE;

            float factor = 1f + (nextRoll01() * 2f - 1f) * m_shopConfig.JitterRatio;
            return Mathf.Max(MIN_PRICE, Mathf.RoundToInt(basePrice * factor));
        }

        private int resolveRefreshCost()
        {
            float grown = m_shopConfig.RefreshBaseCostValue *
                          Mathf.Pow(m_shopConfig.RefreshGrowth, m_refreshCount);

            return Mathf.Min(m_shopConfig.RefreshCostCap, Mathf.RoundToInt(grown));
        }

        private float nextRoll01()
        {
            return (float) m_random.NextDouble();
        }
    }
}
