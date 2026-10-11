/*
* ┌──────────────────────────────────┐
* │  描    述: 货架上的一件商品条目，定死款式、档位与售价，卖出一件划掉一件
* │  类    名: ShopStockEntry.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 条目只描述「有什么、多少钱、卖没卖出去」，扣钱与入手全部在 ShopService 里
* 定义走 IBackpackDef，装备与布料板块在同一条链路上流通
* 显示名不放在这里，界面上的叫法由 UI 层统一拼，模型不掺和文案
* IsPremium 只记这件是花钱开的格子里抽出来的，界面靠它染卡底，玩法上一条都不变
*/

using Module.Backpack.Data;

namespace Module.Shop.Model
{
    public sealed class ShopStockEntry
    {
        // ==================== 属性 ====================
        /// <summary>本件商品的定义，装备或布料板块</summary>
        public IBackpackDef Definition { get; }

        /// <summary>本件商品的档位，布料板块恒为 1</summary>
        public int Tier { get; }

        /// <summary>本件商品的售价，抽上架时就算死，之后不随金币变动</summary>
        public int Price { get; }

        /// <summary>是否已被买走，买走后条目留在货架上占位，只是不可再交互</summary>
        public bool IsSold { get; private set; }

        /// <summary>是否出自花钱解锁的格子，只决定界面怎么画，与价钱和玩法都无关</summary>
        public bool IsPremium { get; }

        // ==================== Constructor ====================
        public ShopStockEntry(IBackpackDef definition, int tier, int price, bool isPremium = false)
        {
            Definition = definition;
            Tier = tier;
            Price = price;
            IsPremium = isPremium;
        }

        // ==================== Public Function ====================
        /// <summary>
        /// 标记为已售出，货架重抽时由整面重铺自然复位
        /// </summary>
        public void MarkSold()
        {
            IsSold = true;
        }
    }
}
