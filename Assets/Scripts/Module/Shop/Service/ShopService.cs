/*
* ┌──────────────────────────────────┐
* │  描    述: 商店服务，按权重抽货架、算售价、扣金币并把入手的东西交给背包
* │  类    名: ShopService.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 抽商品分三步：先抽类别，是装备再抽档位并从装备池抽款式，是布料则先按稀有度权重抽一块布再等概率剪一个异形
* 售价基准只有一个来源，装备优先取自己身上配的档位价、没配才回落背包配置的档位价，布料按「单格基准价 × 占格数 × 稀有度系数」现算
* 装备自带价是为了让「占 2 格」和「占 6 格」不再同价：格子是本玩法的核心资源，它不进价格就逼不出取舍
* 两边都只再乘一次浮动，回收价走同一条基准公式，只差一次回收比例，两处不会算出两个数
* 稀有度同时驱动两件事：越稀有的布抽中权重越低、价钱越高，两个数都写在总表的同一行里
* 付费格不是货架多摆一格，是换一套权重再抽一遍：常开格与付费格各建一套类别、档位、稀有度表，池子空了各删各的
* 解锁的格子当场就抽，抽够次数还够不上保底就硬塞一件；解锁数只在重开一局时清零，跨回合重抽不会把开过的格子关回去
* 扣钱与入手是分开的两步，入手失败原额退回，不会出现钱花了东西没影
* 买入落点由玩家在界面上拖出来定，铺不开与架子满都算没成交，钱原额退回
* 刷新价钱只在服务内部按次数累进，重开一局换新实例与新开一回合都把次数与价钱打回起始价
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

        // 付费格的保底线：装备从这一档起、布料从这一档稀有度起才算得上高级货
        private const int GUARANTEE_MIN_TIER = 2;
        private const int GUARANTEE_MIN_RARITY = 2;

        // ==================== 类型 ====================
        private enum GoodsCategory
        {
            Item,
            Cloth,
            BlankCloth
        }

        // ==================== 属性 ====================
        /// <summary>当前货架上的商品：前面是常开格，后面跟着已解锁的付费格，长度随解锁数往上涨</summary>
        public IReadOnlyList<ShopStockEntry> Stock => m_stock;

        /// <summary>开局白送的格子数</summary>
        public int FreeSlotCount => m_shopConfig.FreeSlots;

        /// <summary>花钱才能开的格子总数</summary>
        public int PaidSlotTotal => m_shopConfig.PaidSlots;

        /// <summary>已经解锁了几格付费货架</summary>
        public int UnlockedPaidCount => m_unlockedPaidCount;

        /// <summary>解锁下一格要花的金币，付费格全开完了返回 0</summary>
        public int NextUnlockPrice
        {
            get
            {
                if (m_unlockedPaidCount >= m_shopConfig.PaidSlots)
                    return 0;

                return m_shopConfig.PaidUnlockPriceAt(m_unlockedPaidCount);
            }
        }

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

        // 付费格自己一套表，与常开格同构；保底那两张只装够得上高级的东西
        private readonly WeightedTable<GoodsCategory> m_premiumCategoryTable = new();
        private readonly WeightedTable<int> m_premiumTierTable = new();
        private readonly WeightedTable<int> m_premiumBuffClothTable = new();
        private readonly WeightedTable<int> m_premiumBlankClothTable = new();
        private readonly WeightedTable<int> m_guaranteeTierTable = new();
        private readonly WeightedTable<int> m_guaranteeClothTable = new();

        private readonly HashSet<string> m_takenKeys = new();
        private int m_refreshCount;
        private int m_unlockedPaidCount;

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
        /// 重抽一整面货架，只换货不动刷新次数与价钱，复位那两样归 ResetForNewRound
        /// 常开格与已解锁的付费格一起重抽，各走各的权重，已花的解锁钱不退
        /// </summary>
        public void RollStock()
        {
            m_stock.Clear();
            m_takenKeys.Clear();

            for (int i = 0; i < m_shopConfig.FreeSlots; i++)
                tryAppendEntry(false);

            // 已经花过钱开的格子跟着一起重抽，上一轮开过的那几格不会在新回合被关掉
            for (int i = 0; i < m_unlockedPaidCount; i++)
                tryAppendEntry(true);
        }

        /// <summary>
        /// 开一个新回合：刷新次数打回零、价钱回到起始档、整面货架重抽一遍
        /// 上一轮卖空的格子不该跟着进这一轮，所以已售条目随库存一起重建
        /// </summary>
        public void ResetForNewRound()
        {
            m_refreshCount = 0;
            RefreshCost = m_shopConfig.RefreshBaseCostValue;
            RollStock();
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
        /// 花金币解锁下一格付费货架，钱不够就一格也不开，开成的那一格当场抽一件高级货上架
        /// 格子按从左到右的顺序开，前一格没开就轮不到后面的格子
        /// </summary>
        /// <param name="run">本局运行态，扣钱走它</param>
        public bool TryUnlockSlot(RunState run)
        {
            if (run == null)
                return false;

            int price = NextUnlockPrice;

            if (price <= 0)
                return false;

            if (!run.TrySpend(price))
                return false;

            m_unlockedPaidCount++;
            tryAppendEntry(true);

            return true;
        }

        /// <summary>
        /// 按玩家拖出来的落点买走货架上的一件，铺不开就整笔作废原额退回
        /// </summary>
        /// <param name="entry">要买的商品条目</param>
        /// <param name="run">本局运行态，扣钱走它</param>
        /// <param name="backpack">本局背包，入手走它</param>
        /// <param name="anchorColumn">落点包围盒左上角列</param>
        /// <param name="anchorRow">落点包围盒左上角行</param>
        public ShopBuyResult TryBuyAt(ShopStockEntry entry, RunState run, RunBackpack backpack, int anchorColumn,
            int anchorRow)
        {
            return tryComplete(entry, run, backpack,
                () => backpack.TryAddItemAt(entry.Definition, entry.Tier, anchorColumn, anchorRow));
        }

        /// <summary>
        /// 把货架上的一件直接买进暂存架，架子没空位就整笔作废原额退回
        /// </summary>
        /// <param name="entry">要买的商品条目</param>
        /// <param name="run">本局运行态，扣钱走它</param>
        /// <param name="backpack">本局背包，入手走它</param>
        public ShopBuyResult TryBuyToStash(ShopStockEntry entry, RunState run, RunBackpack backpack)
        {
            return tryComplete(entry, run, backpack,
                () => backpack.TryAddItemToStash(entry.Definition, entry.Tier));
        }

        /// <summary>
        /// 折算一件存放物的回收价，装备与布料都按上架基准价乘回收比例，随机浮动不参与
        /// </summary>
        /// <param name="definition">要卖的存放物定义</param>
        /// <param name="tier">要卖的那件是几档，布料恒为 1，只有装备那一支会读它</param>
        public int SellPriceOf(IBackpackDef definition, int tier)
        {
            if (definition is ClothPieceDef piece)
            {
                // 只挂装备池的配置可以不填总表，这时货架上根本不会有布，这条链路也就问不到这里
                if (piece.Cloth == null || piece.Shape == null || m_catalog == null)
                    return 0;

                return toSellPrice(clothBasePriceOf(piece));
            }

            if (definition is BackpackItemSO item)
                return toSellPrice(itemBasePriceOf(item, tier));

            return 0;
        }

        // ==================== Private Function ====================
        // 扣钱、入手、标记售出三步走，place 只负责「按玩家给的落点入不入库」，落点之外的账都记在这里
        private ShopBuyResult tryComplete(ShopStockEntry entry, RunState run, RunBackpack backpack, Func<bool> place)
        {
            if (entry == null || entry.Definition == null)
                return ShopBuyResult.NotFound;

            if (entry.IsSold)
                return ShopBuyResult.Sold;

            if (run == null || backpack == null)
                return ShopBuyResult.NotFound;

            if (!run.TrySpend(entry.Price))
                return ShopBuyResult.NotEnoughCoins;

            if (!place())
            {
                run.AddCoins(entry.Price);
                return ShopBuyResult.NoRoom;
            }

            entry.MarkSold();
            return ShopBuyResult.Success;
        }
        // 奖池只在开店时摊平成权重表，之后每次抽取都只查表，总表改动要重开一局才生效
        // 装备池常开格与付费格共用，真正分开的是档位、稀有度与类别这三套权重
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
            {
                m_tierTable.Add(tier, m_shopConfig.TierWeightAt(tier));
                m_premiumTierTable.Add(tier, m_shopConfig.PaidTierWeightAt(tier));
            }

            // 两套类别表各删各的空池，常开格抽不出布不该连累付费格
            pruneEmptyCategories(m_categoryTable, m_buffClothTable, m_blankClothTable,
                m_shopConfig.ItemWeight, m_shopConfig.ClothWeight, m_shopConfig.BlankClothWeight);
            pruneEmptyCategories(m_premiumCategoryTable, m_premiumBuffClothTable, m_premiumBlankClothTable,
                m_shopConfig.PaidItemWeight, m_shopConfig.PaidClothWeight, m_shopConfig.PaidBlankClothWeight);

            if (m_categoryTable.Count == 0)
                QLog.Error($"[{nameof(ShopService)}] 装备池与布料总表都抽不出东西，货架会全程空着");

            if (m_shopConfig.PaidSlots > 0 && m_premiumCategoryTable.Count == 0)
                QLog.Error($"[{nameof(ShopService)}] 付费格的三个类别权重或奖池是空的，解锁出来的格子只能空着");

            buildGuaranteeTables();
        }

        // 一块布进哪个池只看它挂没挂 BUFF，权重取它那一档稀有度的抽中权重
        // 同一块布在常开格与付费格两个池里存的是同一个下标，只是两边取的权重不一样
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
                float premiumWeight = m_shopConfig.PaidClothRarityWeightAt(cloth.RarityIndex);

                if (cloth.IsBlank)
                {
                    m_blankClothTable.Add(i, weight);
                    m_premiumBlankClothTable.Add(i, premiumWeight);
                }
                else
                {
                    m_buffClothTable.Add(i, weight);
                    m_premiumBuffClothTable.Add(i, premiumWeight);
                }
            }
        }

        // 池子空了的类别不该继续占权重，每次抽到它都只能白跑一趟；装备池是两套格子共用的，只查一次
        private void pruneEmptyCategories(WeightedTable<GoodsCategory> categoryTable,
            WeightedTable<int> buffClothTable, WeightedTable<int> blankClothTable,
            float itemWeight, float clothWeight, float blankWeight)
        {
            categoryTable.Clear();

            if (m_itemTable.Count > 0 && itemWeight > 0f)
                categoryTable.Add(GoodsCategory.Item, itemWeight);

            if (buffClothTable.Count > 0 && clothWeight > 0f)
                categoryTable.Add(GoodsCategory.Cloth, clothWeight);

            if (blankClothTable.Count > 0 && blankWeight > 0f)
                categoryTable.Add(GoodsCategory.BlankCloth, blankWeight);
        }

        // 保底池只装够得上「高级」的那些：装备从保底档位起，布料从保底稀有度起且不要空白布
        // 权重沿用付费格那一套，保底抽出来的东西和正常抽出来的手感不该是两个价
        private void buildGuaranteeTables()
        {
            for (int tier = GUARANTEE_MIN_TIER; tier <= ShopConfigSO.TIER_COUNT; tier++)
                m_guaranteeTierTable.Add(tier, m_shopConfig.PaidTierWeightAt(tier));

            if (m_catalog == null)
                return;

            int clothCount = m_catalog.ClothCount;

            for (int i = 0; i < clothCount; i++)
            {
                ClothEntry cloth = m_catalog.ClothAt(i);

                if (cloth == null || cloth.ShapeCount == 0 || cloth.IsBlank)
                    continue;

                if (cloth.RarityIndex < GUARANTEE_MIN_RARITY)
                    continue;

                m_guaranteeClothTable.Add(i, m_shopConfig.PaidClothRarityWeightAt(cloth.RarityIndex));
            }
        }

        // 先抽类别再抽款式，同一面货架尽量不撞同款，撞够次数就作罢，别让空池子卡死上架
        // 付费格多一道门槛：够不上高级的抽法当作没抽到，一直重抽，八次还不合格就走保底硬塞一件
        private void tryAppendEntry(bool premium)
        {
            WeightedTable<GoodsCategory> categoryTable = premium ? m_premiumCategoryTable : m_categoryTable;

            for (int attempt = 0; attempt < DEDUPE_MAX_ATTEMPTS; attempt++)
            {
                if (!categoryTable.TrySample(nextRoll01(), out GoodsCategory category))
                    break;

                ShopStockEntry entry = category switch
                {
                    GoodsCategory.Item => rollItem(premium),
                    GoodsCategory.Cloth => rollCloth(premium ? m_premiumBuffClothTable : m_buffClothTable, premium),
                    _ => rollCloth(premium ? m_premiumBlankClothTable : m_blankClothTable, premium)
                };

                if (entry == null)
                    continue;

                if (premium && m_shopConfig.PaidGuaranteeEnabled && !qualifiesAsPremium(entry))
                    continue;

                if (attempt + 1 < DEDUPE_MAX_ATTEMPTS && m_takenKeys.Contains(buildKey(entry)))
                    continue;

                m_takenKeys.Add(buildKey(entry));
                m_stock.Add(entry);
                return;
            }

            // 走到这里说明这一格要么抽不出合格货、要么池子干脆是空的，花钱开的格子不能就这么空着
            if (premium && m_shopConfig.PaidGuaranteeEnabled)
                appendGuaranteeEntry();
        }

        // 布料看稀有度、装备看档位，两条线各走各的，一条都够不上就不该摆在花钱的格子上
        private bool qualifiesAsPremium(ShopStockEntry entry)
        {
            if (entry.Definition is ClothPieceDef piece)
                return piece.Cloth != null && !piece.Cloth.IsBlank
                       && piece.Cloth.RarityIndex >= GUARANTEE_MIN_RARITY;

            return entry.Tier >= GUARANTEE_MIN_TIER;
        }

        // 保底硬塞一件上架，保底池自己也空了就只能warning，别为了塞货把上架这条路卡死
        private void appendGuaranteeEntry()
        {
            ShopStockEntry entry = forcePremiumEntry();

            if (entry == null)
            {
                QLog.Warning($"[{nameof(ShopService)}] 付费格的保底池是空的，这一格只能空着");
                return;
            }

            m_takenKeys.Add(buildKey(entry));
            m_stock.Add(entry);
        }

        // 保底走哪一支照付费格的类别权重决定，那支空了就换另一支，两支都空才认输
        private ShopStockEntry forcePremiumEntry()
        {
            if (!m_premiumCategoryTable.TrySample(nextRoll01(), out GoodsCategory category))
                category = GoodsCategory.Item;

            // 空白布不带 BUFF，在保底这里不算高级货，抽到它那一支就当走带加成的布
            if (category == GoodsCategory.BlankCloth)
                category = GoodsCategory.Cloth;

            if (category == GoodsCategory.Item)
            {
                ShopStockEntry item = rollItemFrom(m_guaranteeTierTable, true);
                return item != null ? item : rollCloth(m_guaranteeClothTable, true);
            }

            ShopStockEntry cloth = rollCloth(m_guaranteeClothTable, true);
            return cloth != null ? cloth : rollItemFrom(m_guaranteeTierTable, true);
        }

        private ShopStockEntry rollItem(bool premium)
        {
            return rollItemFrom(premium ? m_premiumTierTable : m_tierTable, premium);
        }

        // 装备的款式池两套格子共用，只有档位权重分开，定价那条链路两边完全一致
        private ShopStockEntry rollItemFrom(WeightedTable<int> tierTable, bool premium)
        {
            if (!m_itemTable.TrySample(nextRoll01(), out BackpackItemSO item))
                return null;

            if (!tierTable.TrySample(nextRoll01(), out int tier))
                tier = FIRST_TIER;

            int basePrice = item.TierPriceAt(tier);

            if (basePrice <= 0)
                basePrice = m_backpackConfig.TierPrice(tier);

            return new ShopStockEntry(item, tier, applyJitter(basePrice), premium);
        }

        // 抽布只看稀有度权重，抽到之后剪成哪种异形是等概率，异形本身不参与稀有度
        // 传哪张表就是哪套格子的池子，两张表存的都是同一个下标
        private ShopStockEntry rollCloth(WeightedTable<int> table, bool premium)
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

            return new ShopStockEntry(piece, piece.MaxTier, applyJitter(clothBasePriceOf(piece)), premium);
        }

        // 布料的基准价：单格价 × 占格数 × 稀有度系数，上架与回收共用这一条，改价只改总表那一行
        private int clothBasePriceOf(ClothPieceDef piece)
        {
            return Mathf.RoundToInt(piece.Cloth.CellBasePriceValue * piece.Shape.CellCount
                                     * m_catalog.PriceScaleOf(piece.Cloth.RarityIndex));
        }

        // 装备的基准价：自带档位价优先，没配才回落全局档位定价，与上架那一条口径完全一致
        private int itemBasePriceOf(BackpackItemSO item, int tier)
        {
            int ownPrice = item.TierPriceAt(tier);
            return ownPrice > 0 ? ownPrice : m_backpackConfig.TierPrice(tier);
        }

        // 回收价只在基准价上乘一次比例，不重复算稀有度与占格，避免出现第二个价源
        private int toSellPrice(int basePrice)
        {
            if (basePrice <= 0)
                return 0;

            return Mathf.Max(MIN_PRICE, Mathf.RoundToInt(basePrice * m_shopConfig.SellRatio));
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
