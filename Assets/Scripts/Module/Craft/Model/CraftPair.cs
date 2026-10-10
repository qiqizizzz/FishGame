/*
* ┌──────────────────────────────────┐
* │  描    述: 一组已配好对的合成素材，记录谁留下、谁被吃掉、产出什么档位
* │  类    名: CraftPair.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 由 CraftService 规划出来，界面只管照着执行，不自己拼配对关系
* Kept 是留下来的那件，产物直接长在它身上，档位、落点与动画起点都以它为准
* 本轮口径是 2 合 1，被吃掉的那件恒为一个；将来改成 3 合 1 时这里换成列表
*/

using Module.Backpack.Model;

namespace Module.Craft.Model
{
    public sealed class CraftPair
    {
        // ==================== 公共字段 ====================
        public BackpackItemInstance Kept;
        public BackpackItemInstance Material;
        public int OutputTier;
        public int CoinCost;
        public string RuleLabel;
    }
}
