/*
 * ┌──────────────────────────────────┐
 * │  描    述: 单局会话配置，井道几何、回合规则与鱼池
 * │  类    名: FishingSessionConfigSO.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 * 回合目标看累计渔获价值，不看条数，钩满一串杂鱼也顶不上几条高价值鱼
 * 井道总深度是世界几何边界，与 IFishingStats.LineLength 是两回事：
 * 钩子触底深度取二者较小值，线不够长而下方仍有鱼正是核心挫败感来源
 * 收益公式：单条收益 = 鱼种标价（FishDataSO）× DepthBonus × ComboMultiplier
 * 渔获价值一分为二：全额推进回合目标，按 CoinPayoutRatio 打折后才进金币银行，商店只花银行里的钱
 * 回合目标写成逐回合手填的表，不用等比公式：玩家产出靠买装备线性成长，公式涨得比玩家快就会撞死墙
 */

using System.Collections.Generic;
using UnityEngine;
using Utils.log;

namespace Module.Fish.Data
{
    [CreateAssetMenu(menuName = "FishGame/Config/Fishing Session Config", fileName = "FishingSessionConfig")]
    public sealed class FishingSessionConfigSO : ScriptableObject
    {
        [Header("井道几何")]
        [Tooltip("井道总深度（米），钩子可下潜的世界边界")]
        [SerializeField] private float ShaftDepthMeters = 100f;
        [Tooltip("井道可视半宽（米），横向拖动的物理边界")]
        [SerializeField] private float ShaftHalfWidthMeters = 3f;

        [Header("回合规则")]
        [Tooltip("每回合固定的下潜次数，次数用尽即裁决本回合是否达标")]
        [SerializeField, Min(1)] private int RoundDivesCount = 3;
        [Tooltip("逐回合的渔获价值目标，超出表尾一律按最后一档，每档都能单独松紧")]
        [SerializeField, Min(1)] private List<int> RoundTargetValues = new() { 90, 125, 170, 230, 305, 400, 520, 680 };
        [Tooltip("开局白送的金币，保证第一回合的商店不是空转。达标看渔获价值不看金币，给多少都不会让第一回合变简单")]
        [SerializeField, Min(0)] private int InitialCoins = 100;

        [Header("鱼池与密度")]
        [Tooltip("本档可用鱼种，Hazard 也放这里，由生成服务按档位分流")]
        [SerializeField] private List<FishDataSO> FishPool = new();
        [Tooltip("横轴为归一化深度(0-1)，纵轴为密度倍率")]
        [SerializeField] private AnimationCurve DensityCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.4f);
        [Tooltip("单个深度切片的基准鱼数，实际数量还要乘密度曲线")]
        [SerializeField, Min(0f)] private float BaseFishCountPerSlice = 2f;
        [Tooltip("每次生成判定为河豚的概率，仅在鱼池含 Hazard 档时生效")]
        [SerializeField, Range(0f, 0.5f)] private float HazardSpawnChance = 0.15f;

        [Header("收益公式")]
        [Tooltip("深度加成的参考深度（米），钩到这一深度时加成吃满。必须落在玩家真能潜到的深度上，填井道总深度会让加成永远没肉")]
        [SerializeField, Min(1f)] private float BonusDepthReferenceMeters = 40f;
        [Tooltip("DepthBonus = 1 + 归一化深度 × 本系数")]
        [SerializeField] private float DepthBonusFactor = 1f;
        [Tooltip("同一次回收内每多捕一条的连击增量")]
        [SerializeField] private float ComboStepPerCatch = 0.15f;
        [Tooltip("渔获价值折成可消费金币的比例：达标看全额渔获价值，钱包只收这一折，避免一趟鱼把商店买空")]
        [SerializeField, Range(0.1f, 1f)] private float FishPayoutRatio = 0.5f;

        public float ShaftDepth => ShaftDepthMeters;
        public float ShaftHalfWidth => ShaftHalfWidthMeters;
        public int DivesPerRound => RoundDivesCount;
        public IReadOnlyList<int> RoundTargets => RoundTargetValues;
        public int StartingCoins => InitialCoins;
        public IReadOnlyList<FishDataSO> Fishes => FishPool;
        public AnimationCurve Density => DensityCurve;
        public float BaseFishPerSlice => BaseFishCountPerSlice;
        public float HazardChance => HazardSpawnChance;
        public float BonusDepthReference => BonusDepthReferenceMeters;
        public float DepthBonusScale => DepthBonusFactor;
        public float ComboStep => ComboStepPerCatch;
        public float CoinPayoutRatio => FishPayoutRatio;

        /// <summary>
        /// 按深度采样密度倍率，深度按井道总深度归一化后取值
        /// </summary>
        public float SampleDensity(float depth)
        {
            if (DensityCurve == null || ShaftDepthMeters <= 0f)
                return 0f;

            return DensityCurve.Evaluate(Mathf.Clamp01(depth / ShaftDepthMeters));
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            // 新建未落盘的资产鱼池还是空的，跳过校验避免创建期误报
            if (!UnityEditor.AssetDatabase.Contains(this))
                return;
#endif

            if (FishPool == null || FishPool.Count == 0)
            {
                QLog.Error($"[{nameof(FishingSessionConfigSO)}] {name} 鱼池为空，本局不会生成任何鱼");
                return;
            }

            if (RoundTargetValues == null || RoundTargetValues.Count == 0)
                QLog.Error($"[{nameof(FishingSessionConfigSO)}] {name} 未填回合目标表，运行时会退回兜底目标");

            for (int i = FishPool.Count - 1; i >= 0; i--)
            {
                if (FishPool[i] != null)
                    continue;

                QLog.Error($"[{nameof(FishingSessionConfigSO)}] {name} 鱼池第 {i} 项为空引用");
                FishPool.RemoveAt(i);
            }
        }
    }
}
