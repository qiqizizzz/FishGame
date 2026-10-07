/*
* ┌──────────────────────────────────┐
* │  描    述: 一局游戏的状态容器，回合推进、下潜计数、金币银行与回合目标
* │  类    名: RunState.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 回合以固定下潜次数推进，次数用尽才裁决达标与否，中途达标不提前收尾
* 金币跨回合保留，回合目标每回合重算：超额渔获只进银行，不垫高下一回合目标
*/

using System.Collections.Generic;
using UnityEngine;

namespace Module.Fish.Model
{
    public sealed class RunState
    {
        private const int FIRST_ROUND = 1;

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
        private readonly int m_baseRoundTarget;
        private readonly float m_roundTargetGrowth;

        // ==================== Public Function ====================
        /// <summary>
        /// 开一局：定下每回合下潜次数与回合目标的增长曲线
        /// </summary>
        /// <param name="divesPerRound">每回合固定下潜次数</param>
        /// <param name="baseRoundTarget">第一回合的目标渔获价值（金币）</param>
        /// <param name="roundTargetGrowth">目标价值逐回合增长倍率，1.8 即每回合是上回合的 1.8 倍</param>
        public RunState(int divesPerRound, int baseRoundTarget, float roundTargetGrowth)
        {
            DivesPerRound = Mathf.Max(1, divesPerRound);
            m_baseRoundTarget = baseRoundTarget;
            m_roundTargetGrowth = roundTargetGrowth;

            Round = FIRST_ROUND;
            RoundTarget = resolveRoundTarget(FIRST_ROUND);
        }

        /// <summary>
        /// 提交一次出水的渔获，价值同时计入本回合目标进度与金币银行
        /// </summary>
        /// <param name="dive">本次出水结果，空手而出传零收益结果</param>
        /// <param name="hookMaxDepth">本次出水中钩子到达过的最大深度（米）</param>
        public void SubmitDive(DiveResult dive, float hookMaxDepth)
        {
            if (dive == null)
                return;

            m_roundItems.AddRange(dive.Items);
            RoundValue += dive.Coins;
            Coins += dive.Coins;
            TotalCatch += dive.FishCount;
            MaxDepth = Mathf.Max(MaxDepth, hookMaxDepth);
            DivesUsed++;
        }

        /// <summary>
        /// 进入下一回合：本回合目标进度与渔获明细清零，金币银行与整局统计保留
        /// </summary>
        public void AdvanceRound()
        {
            Round++;
            DivesUsed = 0;
            RoundValue = 0;
            RoundTarget = resolveRoundTarget(Round);
            m_roundItems.Clear();
        }

        /// <summary>封盘：本局结束，之后只剩读取</summary>
        public void EndRun()
        {
            IsRunOver = true;
        }

        // ==================== Private Function ====================
        // 第一回合取基准值，之后按倍率等比增长
        private int resolveRoundTarget(int round)
        {
            return Mathf.RoundToInt(m_baseRoundTarget * Mathf.Pow(m_roundTargetGrowth, round - FIRST_ROUND));
        }
    }
}
