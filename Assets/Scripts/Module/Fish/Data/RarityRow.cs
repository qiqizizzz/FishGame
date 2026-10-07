/*
 * ┌──────────────────────────────────┐
 * │  描    述: 稀有度档位行，RarityTableSO 中的一条记录
 * │  类    名: RarityRow.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 * 纯数据记录，字段直接暴露给 Inspector 编辑；运行时只读，禁止写入
 */

using System;
using Module.Fish.Type;
using UnityEngine;

namespace Module.Fish.Data
{
    [Serializable]
    public sealed class RarityRow
    {
        [Header("档位与配色")]
        public FishRarity Rarity = FishRarity.Common;
        [Tooltip("鱼身主色")]
        public Color PrimaryColor = Color.white;
        [Tooltip("fresnel 边缘光颜色，Common 与 Hazard 为无色")]
        public Color RimColor = Color.white;
        [Range(0f, 4f)] public float RimIntensity;
        [Tooltip("传说档开启呼吸式脉冲，配合提示音")]
        public bool RimPulse;

        [Header("结算展示")]
        [Range(0, 5)] public int StarCount;

        [Header("生成与负载")]
        [Tooltip("加权随机表的基准权重，Hazard 填 0，其密度由会话配置接管")]
        public float SpawnWeight;
        public DepthBand DepthBand;
        [Tooltip("挂上钩子占用的张力负载档位，Hazard 为 0（它不进携带结算）")]
        public int TensionLoad;
    }
}
