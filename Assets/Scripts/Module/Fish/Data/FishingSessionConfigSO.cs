/*
 * ┌──────────────────────────────────┐
 * │  描    述: 单局会话配置，井道几何、结束条件与鱼池
 * │  类    名: FishingSessionConfigSO.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 * 井道总深度是世界几何边界，与 IFishingStats.LineLength 是两回事：
 * 钩子触底深度取二者较小值，线不够长而下方仍有鱼正是核心挫败感来源
 * 收益公式：单条收益 = RewardBase × 稀有度倍率 × DepthBonus × ComboMultiplier
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

        [Header("结束条件")]
        [Tooltip("目标捕获数，达到即结束本局")]
        [SerializeField] private int TargetCount = 10;
        [Tooltip("卷线器容错次数，归零后再次受罚即结束")]
        [SerializeField] private int ReelToleranceCount = 3;

        [Header("鱼池与密度")]
        [Tooltip("本档可用鱼种，Hazard 也放这里，由生成服务按档位分流")]
        [SerializeField] private List<FishDataSO> FishPool = new();
        [Tooltip("横轴为归一化深度(0-1)，纵轴为密度倍率")]
        [SerializeField] private AnimationCurve DensityCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.4f);

        [Header("收益公式")]
        [Tooltip("单条收益基准值，乘稀有度倍率后得到 BaseValue(rarity)")]
        [SerializeField] private float RewardBaseValue = 10f;
        [Tooltip("DepthBonus = 1 + 归一化深度 × 本系数")]
        [SerializeField] private float DepthBonusFactor = 1f;
        [Tooltip("同一次回收内每多捕一条的连击增量")]
        [SerializeField] private float ComboStepPerCatch = 0.15f;

        public float ShaftDepth => ShaftDepthMeters;
        public float ShaftHalfWidth => ShaftHalfWidthMeters;
        public int TargetCatchCount => TargetCount;
        public int ReelCount => ReelToleranceCount;
        public IReadOnlyList<FishDataSO> Fishes => FishPool;
        public AnimationCurve Density => DensityCurve;
        public float RewardBase => RewardBaseValue;
        public float DepthBonusScale => DepthBonusFactor;
        public float ComboStep => ComboStepPerCatch;

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
