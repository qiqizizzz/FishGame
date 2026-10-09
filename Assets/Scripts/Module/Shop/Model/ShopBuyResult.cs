/*
* ┌──────────────────────────────────┐
* │  描    述: 商店购买结果，界面据此决定是弹提示、变灰还是照常刷新列表
* │  类    名: ShopBuyResult.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
*/

namespace Module.Shop.Model
{
    public enum ShopBuyResult
    {
        /// <summary>买成了，钱已扣、货已入手</summary>
        Success = 0,

        /// <summary>这一件在之前的点击里已经被买走</summary>
        Sold = 1,

        /// <summary>金币不够，货架与价钱都不变</summary>
        NotEnoughCoins = 2,

        /// <summary>网格与暂存架都满了，钱已原额退回</summary>
        BackpackFull = 3,

        /// <summary>条目本身不可用，属于调用方传错</summary>
        NotFound = 4
    }
}
