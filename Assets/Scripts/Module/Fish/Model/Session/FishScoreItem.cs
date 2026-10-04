/*
* ┌──────────────────────────────────┐
* │  描    述: 单条鱼的结算明细，出水时算好收益并交给结算面板
* │  类    名: FishScoreItem.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 纯数据快照：结算面板在鱼实例回池之后还要展示，所以这里不持有任何运行时引用
*/

using Module.Fish.Type;
using UnityEngine;

namespace Module.Fish.Model
{
    public sealed class FishScoreItem
    {
        public string Id;
        public string DisplayName;
        public FishRarity Rarity;
        public int Stars;
        public int Coins;
        public float CatchDepth;
        public Color PrimaryColor;
    }
}