/*
* ┌──────────────────────────────────┐
* │  描    述: 商店配置，货架容量、抽取权重、价格浮动与刷新涨价曲线
* │  类    名: ShopConfigSO.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 货架每回合重抽一次，抽什么由三层权重决定：先抽类别，装备再抽档位，布料则由总表按稀有度权重抽款式
* 板块这一整类不在这里摆清单，只挂一张布料总表，加一块布改一处就够
* 空白布单独一份权重，保证它不会被带加成的布挤出货架
* 装备售价只认背包配置里的档位价，布料价由总表现算，这里只管浮动比例，避免两处定价打架
* 卖布只按基准价乘回收比例，随机浮动不参与，免得玩家靠一次好运低买高卖
*/

using System.Collections.Generic;
using Module.Backpack.Data;
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
        [Tooltip("每回合上架的商品件数")]
        [SerializeField, Range(1, 12)] private int GoodsSlotCount = 4;

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

        // ==================== 属性 ====================
        public int GoodsCount => GoodsSlotCount;
        public float ItemWeight => ItemCategoryWeight;
        public float ClothWeight => ClothCategoryWeight;
        public float BlankClothWeight => BlankClothCategoryWeight;
        public float JitterRatio => PriceJitterRatio;
        public float SellRatio => ClothSellRatio;
        public int RefreshBaseCostValue => RefreshBaseCost;
        public float RefreshGrowth => RefreshCostGrowth;
        public int RefreshCostCap => RefreshCostCeiling;
        public IReadOnlyList<BackpackItemSO> Items => ItemPool;
        public ClothCatalogSO Catalog => ClothCatalog;

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

        // ==================== Private Function ====================
        private void OnValidate()
        {
            if (ItemPool.Count == 0 && Catalog == null)
                QLog.Error($"[{nameof(ShopConfigSO)}] 装备池与布料总表都没有，货架会整回合空着");

            if (Catalog != null && Catalog.ClothCount == 0)
                QLog.Error($"[{nameof(ShopConfigSO)}] 挂上的布料总表是空的，板块这一类抽不出东西：{Catalog.name}");

            if (TierWeightList.Count != TIER_COUNT && TierWeightList.Count > 0)
                QLog.Error($"[{nameof(ShopConfigSO)}] 档位权重应该有 {TIER_COUNT} 项，当前 {TierWeightList.Count} 项");

            if (Mathf.Abs(ItemCategoryWeight + ClothCategoryWeight + BlankClothCategoryWeight) < float.Epsilon)
                QLog.Error($"[{nameof(ShopConfigSO)}] 三个类别权重全为 0，抽不出任何商品");
        }
    }
}
