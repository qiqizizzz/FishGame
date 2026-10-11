/*
* ┌──────────────────────────────────┐
* │  描    述: 音效与背景音乐的 Addressable 地址定义
* │  类    名: SFXDefines.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
*/

namespace Framework.QTower.Common.Defines
{
    public static class SFXDefines
    {
        // UI 通用
        public const string UI_CLICK = "SFX_UIClick";
        public const string UI_SWITCH = "SFX_UISwitch";

        // 水下判定
        public const string CATCH_FISH = "SFX_catchfish";

        // 河豚专音还没到位，先借咬钩音降调顶一下，换素材只改这一行地址
        public const string HAZARD = "SFX_catchfish";

        // 经济
        public const string COIN = "SFX_coin";
        public const string BUY = "SFX_buy";
        public const string SELL_OUT = "SFX_sellout";

        // 背包与暂存架拖拽
        public const string TAKE_ITEM = "SFX_takeItem";
        public const string TAKE_ITEM_DOWN = "SFX_takeItemDown";

        // 背景音乐
        public const string BGM_MAIN_MENU = "BGM_mainMenu";
        public const string BGM_SHOP = "BGM_shop";
    }
}
