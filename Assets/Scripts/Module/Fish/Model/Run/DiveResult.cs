/*
* ┌──────────────────────────────────┐
* │  描    述: 单次出水的渔获结果，一次回收带上来的鱼与收益
* │  类    名: DiveResult.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 单条收益在出水那一刻算定，这里只做累加，不再回查任何运行时对象
* 空手而出同样产生一份零收益结果，下潜次数照样要扣
*/

using System.Collections.Generic;

namespace Module.Fish.Model
{
    public sealed class DiveResult
    {
        private readonly List<FishScoreItem> m_items = new();

        /// <summary>本次出水带回的每一条鱼，按入钩顺序排列</summary>
        public IReadOnlyList<FishScoreItem> Items => m_items;

        /// <summary>本次出水的渔获价值（金币）</summary>
        public int Coins { get; private set; }

        /// <summary>本次出水带回的鱼数</summary>
        public int FishCount => m_items.Count;

        public void Add(FishScoreItem item)
        {
            m_items.Add(item);
            Coins += item.Coins;
        }
    }
}
