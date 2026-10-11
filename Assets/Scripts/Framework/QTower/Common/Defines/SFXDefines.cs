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

        // 钓钩入水，素材原名 large-underwater-explosion，已并入 SFX_ 命名
        public const string DIVE = "SFX_dive";

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

        // 水底循环，钓钩入水后接管，出水或离开商店即淡停；同一首还在响时重复 PlayBGM 不会顶回开头
        public const string BGM_UNDERWATER_LOOP = "BGM_underwaterLoop";
        public const string BGM_SHOP = "BGM_shop";
    }
}
