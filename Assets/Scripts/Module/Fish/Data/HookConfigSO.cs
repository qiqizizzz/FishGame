/*
 * ┌──────────────────────────────────┐
 * │  描    述: 钩子配置，手感调参与玩家能力基线的唯一来源
 * │  类    名: HookConfigSO.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 * 能力基线段是 IFishingStats 的默认值来源：常量实现从这里取初值，
 * 背包层接上后由背包物品在其上叠加修改。除 IFishingStats 实现外，禁止直接读能力基线字段
 * 序列化字段用配置语义名（Meters / Ratio / Ceiling），对外属性用运行时语义名
 */

using UnityEngine;
using Utils.log;

namespace Module.Fish.Data
{
    [CreateAssetMenu(menuName = "FishGame/Config/Hook Config", fileName = "HookConfig")]
    public sealed class HookConfigSO : ScriptableObject
    {
        [Header("钩子手感")]
        [Tooltip("下潜速度（米/秒）")]
        [SerializeField] private float DescendSpeedMeters = 6f;
        [Tooltip("横向拖动限速（米/秒）")]
        [SerializeField] private float LateralSpeedMeters = 4f;
        [Tooltip("横向惯性收敛系数，越大越跟手")]
        [SerializeField] private float LateralDampingRate = 8f;
        [Tooltip("钩头碰撞半径（米）")]
        [SerializeField] private float HookRadiusMeters = 0.15f;
        [Tooltip("鱼线绘制宽度（米）")]
        [SerializeField] private float LineWidthMeters = 0.02f;

        [Header("能力基线（IFishingStats 的默认值）")]
        [Tooltip("线长上限（米），钩子能到的最深处")]
        [SerializeField] private float LineLengthMeters = 25f;
        [Tooltip("收线速度（米/秒）")]
        [SerializeField] private float ReelSpeedMeters = 4.5f;
        [Tooltip("张力上限，超过即断线")]
        [SerializeField] private float TensionCeiling = 100f;
        [Tooltip("断线前的缓冲窗口比例（0-1）")]
        [Range(0f, 1f)] [SerializeField] private float ReliefRatio;
        [Tooltip("钩上可同时挂着的鱼条数上限，每条鱼一律占 1 个名额")]
        [SerializeField] private int CarryCountCeiling = 10;
        [Tooltip("稀有度权重放大系数，作用于加权随机表")]
        [SerializeField] private float LureWeightScale = 1f;
        [Tooltip("鱼显形半径（米），深水区关键")]
        [SerializeField] private float DetectRadiusMeters = 0.8f;
        [Tooltip("卖鱼金币加成，上限锁 0.6")]
        [Range(0f, 0.6f)] [SerializeField] private float CoinBonusRatio;

        [Header("张力与脱钩")]
        [Tooltip("每 1 点张力负载折算的张力值")]
        [SerializeField] private float TensionPerLoad = 12f;
        [Tooltip("张力自然缓释速度（点/秒）")]
        [SerializeField] private float TensionReliefPerSecond = 5f;
        [Tooltip("回收途中受罚时钩上鱼单条脱钩概率")]
        [Range(0f, 1f)] [SerializeField] private float DetachChanceRatio = 0.35f;
        [Tooltip("碰到河豚后缠绕僵直时长（秒）")]
        [SerializeField] private float TangledDurationSeconds = 1.2f;

        public float DescendSpeed => DescendSpeedMeters;
        public float LateralSpeed => LateralSpeedMeters;
        public float LateralDamping => LateralDampingRate;
        public float HookRadius => HookRadiusMeters;
        public float LineWidth => LineWidthMeters;

        public float LineLength => LineLengthMeters;
        public float ReelSpeed => ReelSpeedMeters;
        public float TensionMax => TensionCeiling;
        public float TensionRelief => ReliefRatio;
        public int MaxCarryCount => CarryCountCeiling;
        public float LurePull => LureWeightScale;
        public float DetectRadius => DetectRadiusMeters;
        public float CoinBonus => CoinBonusRatio;

        public float TensionLoadUnit => TensionPerLoad;
        public float TensionDecayPerSecond => TensionReliefPerSecond;
        public float DetachChance => DetachChanceRatio;
        public float TangledDuration => TangledDurationSeconds;

        private void OnValidate()
        {
            if (DescendSpeedMeters <= 0f)
                QLog.Error($"[{nameof(HookConfigSO)}] 下潜速度必须大于 0");

            if (TensionCeiling <= 0f)
                QLog.Error($"[{nameof(HookConfigSO)}] 张力上限必须大于 0");

            if (CarryCountCeiling <= 0)
                QLog.Error($"[{nameof(HookConfigSO)}] 钩上条数上限必须大于 0");
        }
    }
}
