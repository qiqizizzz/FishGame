/*
* ┌──────────────────────────────────┐
* │  描    述: 商店配置，货架容量、抽取权重、价格浮动与刷新涨价曲线
* │  类    名: ShopConfigSO.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 货架每回合重抽一次，抽什么由三层权重决定：先抽类别，装备再抽档位，布料则由总表按稀有度权重抽款式
* 板块这一整类不在这里摆清单，只挂一张布料总表，加一块布改一处就够
* 空白布单独一份权重，保证它不会被带加成的布挤出货架
* 货架分常开格与付费格两套权重：常开格便宜货多，付费格解锁之后才抽，高档装备与稀有的布在那一边密得多
* 最高档装备与最高稀有度的布不再第一回合就露面：常开格要等 TopTierFreeEntryRound、付费格等 TopTierPaidEntryRound 那两回合才放开
* 装备售价只认背包配置里的档位价，布料价由总表现算，这里只管浮动比例，避免两处定价打架
* 卖布只按基准价乘回收比例，随机浮动不参与，免得玩家靠一次好运低买高卖
* 合成规则表也挂在商店配置上，商店阶段是合成唯一的入口，别在场景里再散一份引用
*/

using System.Collections.Generic;
using Module.Backpack.Data;
using Module.Craft.Data;
using UnityEngine;
using Utils.log;

namespace Module.Shop.Data
{
    [CreateAssetMenu(menuName = "FishGame/Config/Shop Config", fileName = "ShopConfig")]
    public sealed class ShopConfigSO : ScriptableObject
    {
        // ==================== 常量 ====================
        public const int TIER_COUNT = BackpackConfigSO.TIER_COUNT;

        // ==================== 字段[外部设置] ====================
        [Header("货架")]
        [Tooltip("货架总格数，等于常开格加付费格，界面按这个数铺卡")]
        [SerializeField, Range(1, 12)] private int GoodsSlotCount = 10;

        [Header("类别权重")]
        [Tooltip("抽到装备的权重，与下面两项同量纲，填 0 等于下架该类别")]
        [SerializeField, Min(0f)] private float ItemCategoryWeight = 55f;
        [Tooltip("抽到带 BUFF 布料的权重")]
        [SerializeField, Min(0f)] private float ClothCategoryWeight = 35f;
        [Tooltip("抽到空白布的权重，空白布是腾格子的路，权重不要给太低")]
        [SerializeField, Min(0f)] private float BlankClothCategoryWeight = 10f;

        [Header("装备档位权重")]
        [Tooltip("抽装备时一二三级的权重，共 3 项")]
        [SerializeField] private List<float> TierWeightList = new() { 70f, 25f, 5f };

        [Header("付费槽位")]
        [Tooltip("开局就开着、不收钱的格子数，剩下的格子一律上锁，按从左到右的顺序逐格花金币开")]
        [SerializeField, Range(1, 12)] private int FreeSlotCount = 5;
        [Tooltip("每一把锁的解锁价，几项就是几个付费格，越靠后的格子越贵")]
        [SerializeField] private List<int> PaidSlotUnlockPriceList = new() { 50, 80, 110, 150, 200 };
        [Tooltip("付费格的保底：每一格至少出二级以上的装备或稀有以上的布，正常抽够次数还够不上就强制塞一件")]
        [SerializeField] private bool PaidSlotGuarantee = true;

        [Header("付费槽位抽取权重")]
        [Tooltip("付费格抽到装备的权重，与下面两项同量纲，填 0 等于下架该类别")]
        [SerializeField, Min(0f)] private float PaidItemCategoryWeight = 55f;
        [Tooltip("付费格抽到带 BUFF 布料的权重，比常开格给得高，稀有布主要指望花钱的格子出")]
        [SerializeField, Min(0f)] private float PaidClothCategoryWeight = 40f;
        [Tooltip("付费格抽到空白布的权重，空白布没有 BUFF，保底认的是带加成的布，这里压低")]
        [SerializeField, Min(0f)] private float PaidBlankClothCategoryWeight = 5f;
        [Tooltip("付费格抽装备时一二三级的权重，共 3 项，二三级比常开格重得多")]
        [SerializeField] private List<float> PaidTierWeightList = new() { 35f, 55f, 10f };
        [Tooltip("付费格抽布时各档稀有度的权重，项数与布料总表的稀有度档数一致，越界钳最后一项")]
        [SerializeField] private List<float> PaidClothRarityWeightList = new() { 25f, 35f, 28f, 12f };

        [Header("最高档进场回合")]
        [Tooltip("常开格最早在第几回合刷出最高档装备与最高稀有度的布，未到回合前一律压到次高档")]
        [SerializeField, Min(1)] private int TopTierFreeEntryRound = 5;
        [Tooltip("付费格最早在第几回合刷出最高档，比常开格早一回合，留一点花钱开格子的意义")]
        [SerializeField, Min(1)] private int TopTierPaidEntryRound = 4;

        [Header("价格浮动")]
        [Tooltip("在基准价上下的浮动比例，0.1 即最高低一成、最高高一成")]
        [Range(0f, 0.5f)] [SerializeField] private float PriceJitterRatio = 0.1f;
        [Tooltip("卖掉一块布能回收基准买价的几成，0.5 即半价，填 0 等于不认出售")]
        [SerializeField, Range(0f, 1f)] private float ClothSellRatio = 0.5f;

        [Header("刷新涨价")]
        [Tooltip("第一次刷新货架的价钱")]
        [SerializeField, Min(0)] private int RefreshBaseCost = 10;
        [Tooltip("每刷一次价钱乘上的倍率")]
        [SerializeField, Min(1f)] private float RefreshCostGrowth = 1.6f;
        [Tooltip("刷新价钱的上限，越界后一直按这个数收")]
        [SerializeField, Min(0)] private int RefreshCostCeiling = 60;

        [Header("奖池")]
        [Tooltip("可上架的装备定义")]
        [SerializeField] private List<BackpackItemSO> ItemPool = new();
        [Tooltip("布料总表，板块类商品全部由它按稀有度随机刷出")]
        [SerializeField] private ClothCatalogSO ClothCatalog;

        [Header("合成")]
        [Tooltip("合成规则总表，商店阶段的合成按钮只认这一张表")]
        [SerializeField] private CraftRuleSO CraftRuleList;

        // ==================== 属性 ====================
        /// <summary>货架总格数，等于常开格加付费格</summary>
        public int GoodsCount => GoodsSlotCount;
        public float ItemWeight => ItemCategoryWeight;
        public float ClothWeight => ClothCategoryWeight;
        public float BlankClothWeight => BlankClothCategoryWeight;
        /// <summary>开局白送的格子数，剩下的格子一律上锁</summary>
        public int FreeSlots => FreeSlotCount;
        /// <summary>花钱才能开的格子数，就是解锁价表里那几档</summary>
        public int PaidSlots => PaidSlotUnlockPriceList.Count;
        /// <summary>付费格要不要保底，关掉之后花钱开的格子也可能抽到低档货</summary>
        public bool PaidGuaranteeEnabled => PaidSlotGuarantee;
        public float PaidItemWeight => PaidItemCategoryWeight;
        public float PaidClothWeight => PaidClothCategoryWeight;
        public float PaidBlankClothWeight => PaidBlankClothCategoryWeight;
        public float JitterRatio => PriceJitterRatio;
        public float SellRatio => ClothSellRatio;
        public int RefreshBaseCostValue => RefreshBaseCost;
        public float RefreshGrowth => RefreshCostGrowth;
        public int RefreshCostCap => RefreshCostCeiling;
        public IReadOnlyList<BackpackItemSO> Items => ItemPool;
        public ClothCatalogSO Catalog => ClothCatalog;
        public CraftRuleSO CraftRules => CraftRuleList;
        /// <summary>常开格放开最高档装备与最高稀有度布的最早回合</summary>
        public int TopTierFreeEntryRoundValue => TopTierFreeEntryRound;
        /// <summary>付费格放开最高档的最早回合</summary>
        public int TopTierPaidEntryRoundValue => TopTierPaidEntryRound;

        // ==================== Public Function ====================
        /// <summary>
        /// 取指定档位的抽取权重，档位越界时钳到有效区间
        /// </summary>
        /// <param name="tier">档位，从 1 开始</param>
        public float TierWeightAt(int tier)
        {
            if (TierWeightList.Count == 0)
                return 0f;

            int index = Mathf.Clamp(tier, 1, TierWeightList.Count) - 1;
            return TierWeightList[index];
        }

        /// <summary>
        /// 取第几把锁的解锁价，付费格下标从 0 开始，越界时钳到最后一档，一个付费格都没配就返回 0
        /// </summary>
        /// <param name="paidIndex">付费格下标，从 0 开始</param>
        public int PaidUnlockPriceAt(int paidIndex)
        {
            if (PaidSlotUnlockPriceList.Count == 0)
                return 0;

            int index = Mathf.Clamp(paidIndex, 0, PaidSlotUnlockPriceList.Count - 1);
            return PaidSlotUnlockPriceList[index];
        }

        /// <summary>
        /// 取付费格抽装备时某档位的权重，档位越界时钳到有效区间
        /// </summary>
        /// <param name="tier">档位，从 1 开始</param>
        public float PaidTierWeightAt(int tier)
        {
            if (PaidTierWeightList.Count == 0)
                return 0f;

            int index = Mathf.Clamp(tier, 1, PaidTierWeightList.Count) - 1;
            return PaidTierWeightList[index];
        }

        /// <summary>
        /// 取付费格抽布时某档稀有度的权重，下标从 0 开始，越界时钳到最后一档
        /// </summary>
        /// <param name="rarIndex">稀有度下标，从 0 开始</param>
        public float PaidClothRarityWeightAt(int rarIndex)
        {
            if (PaidClothRarityWeightList.Count == 0)
                return 0f;

            int index = Mathf.Clamp(rarIndex, 0, PaidClothRarityWeightList.Count - 1);
            return PaidClothRarityWeightList[index];
        }

        // ==================== Private Function ====================
        private void OnValidate()
        {
            if (ItemPool.Count == 0 && Catalog == null)
                QLog.Error($"[{nameof(ShopConfigSO)}] 装备池与布料总表都没有，货架会整回合空着");

            if (Catalog != null && Catalog.ClothCount == 0)
                QLog.Error($"[{nameof(ShopConfigSO)}] 挂上的布料总表是空的，板块这一类抽不出东西：{Catalog.name}");

            if (TierWeightList.Count != TIER_COUNT && TierWeightList.Count > 0)
                QLog.Error($"[{nameof(ShopConfigSO)}] 档位权重应该有 {TIER_COUNT} 项，当前 {TierWeightList.Count} 项");

            // 常开格与付费格加起来必须正好铺满货架，不然会有格子既不上锁也不上架
            if (FreeSlotCount + PaidSlotUnlockPriceList.Count != GoodsSlotCount)
                QLog.Error($"[{nameof(ShopConfigSO)}] 常开格 {FreeSlotCount} 加付费格 {PaidSlotUnlockPriceList.Count} 不等于货架总格数 {GoodsSlotCount}");

            if (PaidTierWeightList.Count > 0 && PaidTierWeightList.Count != TIER_COUNT)
                QLog.Error($"[{nameof(ShopConfigSO)}] 付费格档位权重应该有 {TIER_COUNT} 项，当前 {PaidTierWeightList.Count} 项");

            if (PaidSlotUnlockPriceList.Count > 0 && PaidClothRarityWeightList.Count == 0)
                QLog.Error($"[{nameof(ShopConfigSO)}] 有付费格却没配付费格的稀有度权重，高级货架抽不出布");

            if (PaidSlotUnlockPriceList.Count > 0
                && Mathf.Abs(PaidItemCategoryWeight + PaidClothCategoryWeight + PaidBlankClothCategoryWeight) < float.Epsilon)
                QLog.Error($"[{nameof(ShopConfigSO)}] 有付费格但三个付费类别权重全为 0，花钱开的格子会空着");

            if (Mathf.Abs(ItemCategoryWeight + ClothCategoryWeight + BlankClothCategoryWeight) < float.Epsilon)
                QLog.Error($"[{nameof(ShopConfigSO)}] 三个类别权重全为 0，抽不出任何商品");

            if (CraftRuleList == null)
                QLog.Error($"[{nameof(ShopConfigSO)}] 没有挂合成规则表，商店阶段的合成按钮点不动");
        }
    }
}
