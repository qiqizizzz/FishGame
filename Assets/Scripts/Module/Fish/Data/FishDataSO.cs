/*
 * ┌──────────────────────────────────┐
 * │  描    述: 鱼种档案，一个鱼种一份资产
 * │  类    名: FishDataSO.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 * 只存身份与覆写项，配色与倍率等档位数值一律回 RarityTableSO 查，不在这里冗余
 * 覆写类字段以「<=0 表示沿用档位基准」为约定，避免再引入 nullable 标记
 */

using Module.Fish.Type;
using UnityEngine;
using Utils.log;

namespace Module.Fish.Data
{
    [CreateAssetMenu(menuName = "FishGame/Config/Fish Data", fileName = "FishData")]
    public sealed class FishDataSO : ScriptableObject
    {
        [Header("标识")]
        [Tooltip("稳定标识，存档与图鉴索引使用，禁止使用中文名")]
        [SerializeField] private string Identifier;
        [SerializeField] private string Title;

        [Header("生成资源")]
        [Tooltip("Addressables key，与 Fish 组的 m_Address 完全一致")]
        [SerializeField] private string ResourceKey;

        [Header("档位与行为")]
        [SerializeField] private FishRarity ValueRarity;
        [SerializeField] private FishBehaviorType MovementBehavior;

        [Header("体型与容量")]
        [Tooltip("实测体长（米）")]
        [SerializeField] private float LengthMeters;
        [Tooltip("携带占用的容量点数")]
        [SerializeField] private int CapacityCost;

        [Header("覆写（<=0 表示沿用稀有度基准）")]
        [Tooltip("大于 0 时覆盖档位基准权重")]
        [SerializeField] private float WeightOverride;
        [Tooltip("MaxDepth 大于 0 时覆盖档位基准深度带")]
        [SerializeField] private DepthBand BandOverride;

        public string Id => Identifier;
        public string DisplayName => Title;
        public string Address => ResourceKey;
        public FishRarity Rarity => ValueRarity;
        public FishBehaviorType Behavior => MovementBehavior;
        public float BodyLength => LengthMeters;
        public int CarryCost => CapacityCost;
        public bool HasWeightOverride => WeightOverride > 0f;

        /// <summary>
        /// 解析生效深度带，鱼种覆写优先于档位基准
        /// </summary>
        public DepthBand ResolveDepthBand(RarityTableSO table)
        {
            if (BandOverride.IsValid)
                return BandOverride;

            RarityRow row = table == null ? null : table.Get(ValueRarity);

            if (row == null)
            {
                QLog.Error($"[{nameof(FishDataSO)}] {Identifier} 取不到档位 {ValueRarity} 的深度带");
                return default;
            }

            return row.DepthBand;
        }

        /// <summary>
        /// 解析生效抽取权重，Hazard 档基准权重为 0，由会话密度曲线接管
        /// </summary>
        public float ResolveSpawnWeight(RarityTableSO table)
        {
            if (WeightOverride > 0f)
                return WeightOverride;

            RarityRow row = table == null ? null : table.Get(ValueRarity);

            if (row == null)
            {
                QLog.Error($"[{nameof(FishDataSO)}] {Identifier} 取不到档位 {ValueRarity} 的基准权重");
                return 0f;
            }

            return row.SpawnWeight;
        }

        public float ResolveValueMultiplier(RarityTableSO table)
        {
            RarityRow row = table == null ? null : table.Get(ValueRarity);

            if (row == null)
            {
                QLog.Error($"[{nameof(FishDataSO)}] {Identifier} 取不到档位 {ValueRarity} 的价值倍率");
                return 0f;
            }

            return row.ValueMultiplier;
        }

        public int ResolveTensionLoad(RarityTableSO table)
        {
            RarityRow row = table == null ? null : table.Get(ValueRarity);

            if (row == null)
            {
                QLog.Error($"[{nameof(FishDataSO)}] {Identifier} 取不到档位 {ValueRarity} 的张力负载");
                return 0;
            }

            return row.TensionLoad;
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            // 新建未落盘的资产字段还是空的，跳过校验避免创建期误报
            if (!UnityEditor.AssetDatabase.Contains(this))
                return;
#endif

            if (string.IsNullOrEmpty(ResourceKey))
                QLog.Error($"[{nameof(FishDataSO)}] {name} 未填写 Addressables key");

            if (ValueRarity != FishRarity.Hazard && CapacityCost <= 0)
                QLog.Error($"[{nameof(FishDataSO)}] {Identifier} 可捕获鱼的容量点数必须大于 0");
        }
    }
}
