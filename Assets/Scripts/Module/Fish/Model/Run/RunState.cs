/*
* ┌──────────────────────────────────┐
* │  描    述: 一局游戏的状态容器，回合推进、下潜计数、金币银行与回合目标
* │  类    名: RunState.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 回合以固定下潜次数推进，次数用尽才裁决达标与否，中途达标不提前收尾
* 金币跨回合保留，回合目标每回合重算：超额渔获只进银行，不垫高下一回合目标
* 渔获价值与金币是两套数：全额计入回合目标推进，只有打折后的那一份进金币银行，商店花的是银行里的钱
* 回合目标按配置给的表逐回合直取，越界取最后一档，不再用等倍率公式，避免需求涨得比玩家成长快
*/

using System.Collections.Generic;
using UnityEngine;

namespace Module.Fish.Model
{
    public sealed class RunState
    {
        private const int FIRST_ROUND = 1;
        private const int DEFAULT_ROUND_TARGET = 120;

        // ==================== 属性 ====================
        /// <summary>当前回合数，从 1 开始</summary>
        public int Round { get; private set; }

        /// <summary>每回合固定的下潜次数</summary>
        public int DivesPerRound { get; }

        /// <summary>本回合已经烧掉的下潜次数</summary>
        public int DivesUsed { get; private set; }

        /// <summary>本回合还剩几次下潜</summary>
        public int DivesRemaining => DivesPerRound - DivesUsed;

        /// <summary>本回合次数是否已用尽，用尽即触发回合裁决</summary>
        public bool IsRoundOver => DivesUsed >= DivesPerRound;

        /// <summary>本回合目标渔获价值（金币）</summary>
        public int RoundTarget { get; private set; }

        /// <summary>本回合累计渔获价值（金币），进下一回合时清零</summary>
        public int RoundValue { get; private set; }

        /// <summary>本回合渔获折算后实际入账的金币，明细面板用它解释为什么入账少于渔获价值</summary>
        public int RoundCoins { get; private set; }

        /// <summary>本回合渔获是否达标，达标才准进下一回合</summary>
        public bool IsTargetReached => RoundValue >= RoundTarget;

        /// <summary>本回合带回水面的每一条鱼，按入钩顺序排列</summary>
        public IReadOnlyList<FishScoreItem> RoundItems => m_roundItems;

        /// <summary>金币银行，跨回合保留，不会随回合切换清零</summary>
        public int Coins { get; private set; }

        /// <summary>整局累计带回水面的鱼数</summary>
        public int TotalCatch { get; private set; }

        /// <summary>整局最大下潜深度（米）</summary>
        public float MaxDepth { get; private set; }

        /// <summary>本局是否已结束，回合不达标即结束</summary>
        public bool IsRunOver { get; private set; }

        // ==================== 字段[私有] ====================
        private readonly List<FishScoreItem> m_roundItems = new();
        private readonly IReadOnlyList<int> m_roundTargets;
        private readonly float m_coinPayoutRatio;

        // ==================== Public Function ====================
        /// <summary>
        /// 开一局：定下每回合下潜次数、逐回合的渔获价值目标表与金币折算比例
        /// </summary>
        /// <param name="divesPerRound">每回合固定下潜次数</param>
        /// <param name="roundTargets">逐回合目标渔获价值，越界取最后一档，空表退回兜底值</param>
        /// <param name="startingCoins">开局白送的金币，让第一回合的商店买得起东西</param>
        /// <param name="coinPayoutRatio">渔获价值折进金币银行的比例，1 即渔获全额变现</param>
        public RunState(int divesPerRound, IReadOnlyList<int> roundTargets, int startingCoins, float coinPayoutRatio)
        {
            DivesPerRound = Mathf.Max(1, divesPerRound);
            m_roundTargets = roundTargets;
            m_coinPayoutRatio = Mathf.Clamp01(coinPayoutRatio);

            Coins = Mathf.Max(0, startingCoins);
            Round = FIRST_ROUND;
            RoundTarget = resolveRoundTarget(FIRST_ROUND);
        }

        /// <summary>
        /// 提交一次出水的渔获：全额推进本回合目标，只有折算后的那一份进金币银行
        /// </summary>
        /// <param name="dive">本次出水结果，空手而出传零收益结果</param>
        /// <param name="hookMaxDepth">本次出水中钩子到达过的最大深度（米）</param>
        public void SubmitDive(DiveResult dive, float hookMaxDepth)
        {
            if (dive == null)
                return;

            int paidCoins = Mathf.RoundToInt(dive.Coins * m_coinPayoutRatio);

            m_roundItems.AddRange(dive.Items);
            RoundValue += dive.Coins;
            RoundCoins += paidCoins;
            Coins += paidCoins;
            TotalCatch += dive.FishCount;
            MaxDepth = Mathf.Max(MaxDepth, hookMaxDepth);
            DivesUsed++;
        }

        /// <summary>
        /// 花一笔金币，本局已结束或钱不够都不扣，交给调用方决定怎么提示
        /// </summary>
        /// <param name="cost">要花的金币，传负数按不花处理</param>
        public bool TrySpend(int cost)
        {
            if (IsRunOver)
                return false;

            if (cost > Coins)
                return false;

            Coins -= cost;
            return true;
        }

        /// <summary>
        /// 补一笔金币回银行，只给「钱扣了但东西没到手」这种回滚用
        /// </summary>
        /// <param name="amount">要补的金币，非正数直接忽略</param>
        public void AddCoins(int amount)
        {
            if (amount <= 0)
                return;

            Coins += amount;
        }
        /// <summary>
        /// 进入下一回合：本回合目标进度与渔获明细清零，金币银行与整局统计保留
        /// </summary>
        public void AdvanceRound()
        {
            Round++;
            DivesUsed = 0;
            RoundValue = 0;
            RoundCoins = 0;
            RoundTarget = resolveRoundTarget(Round);
            m_roundItems.Clear();
        }

        /// <summary>封盘：本局结束，之后只剩读取</summary>
        public void EndRun()
        {
            IsRunOver = true;
        }

        // ==================== Private Function ====================
        // 目标按回合序号直取配置表，越界取最后一档；表是空的就退回兜底值，不至于把 0 当目标白送玩家过
        private int resolveRoundTarget(int round)
        {
            if (m_roundTargets == null || m_roundTargets.Count == 0)
                return DEFAULT_ROUND_TARGET;

            return m_roundTargets[Mathf.Clamp(round - FIRST_ROUND, 0, m_roundTargets.Count - 1)];
        }
    }
}
