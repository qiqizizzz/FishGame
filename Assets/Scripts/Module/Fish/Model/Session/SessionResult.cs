/*
* ┌──────────────────────────────────┐
* │  描    述: 一局结算结果，累计本局收益与最大下潜深度
* │  类    名: SessionResult.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 见 01 篇 3.5：单条收益在出水时就已算定，这里只做累加与展示快照，不再回查玩法对象
*/

using System.Collections.Generic;

namespace Module.Fish.Model
{
    public sealed class SessionResult
    {
        private readonly List<FishScoreItem> m_items = new();

        public SessionResult(int targetCatchCount)
        {
            TargetCatchCount = targetCatchCount;
        }

        /// <summary>本局带回水面的每一条鱼，按入钩顺序排列</summary>
        public IReadOnlyList<FishScoreItem> Items => m_items;

        public int TargetCatchCount { get; }

        public int TotalCoins { get; private set; }

        public int TotalCatch { get; private set; }

        /// <summary>本局最大下潜深度（米），对应结算页的「下潜深度」</summary>
        public float MaxDepth { get; private set; }

        public bool IsTargetReached => TotalCatch >= TargetCatchCount;

        public void Add(FishScoreItem item)
        {
            m_items.Add(item);
            TotalCoins += item.Coins;
        }

        /// <summary>封盘：写入最终捕获数与最大深度，之后只读</summary>
        public void Finish(int totalCatch, float maxDepth)
        {
            TotalCatch = totalCatch;
            MaxDepth = maxDepth;
        }
    }
}