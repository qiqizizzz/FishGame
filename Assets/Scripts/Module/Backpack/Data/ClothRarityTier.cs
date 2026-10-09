/*
* ┌──────────────────────────────────┐
* │  描    述: 布料稀有度档，一档同时决定抽中权重与价格系数
* │  类    名: ClothRarityTier.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 越稀有的布权重越低、价钱越高，两个数写在一起，调节奏时只看一行就够
* 档数不固定，总表里想加第四档第五档都行，布料按下标引用
* 权重与系数都是相对量纲，档内所有布料共用，单块布不再各自填
*/

using System;
using UnityEngine;

namespace Module.Backpack.Data
{
    /// <summary>
    /// 一档稀有度，供布料总表的数组元素引用
    /// </summary>
    [Serializable]
    public sealed class ClothRarityTier
    {
        // ==================== 字段[外部设置] ====================
        [Tooltip("程序内唯一标识，例如 common")]
        [SerializeField] private string TierId;

        [Tooltip("界面显示的档名，例如 常见")]
        [SerializeField] private string TierLabel;

        [Tooltip("抽布时的权重，同档的布共用这个数，越大越容易刷出来")]
        [SerializeField, Min(0f)] private float DrawWeight = 62f;

        [Tooltip("售价乘数，1 表示按基准价卖，越稀有填得越大")]
        [SerializeField, Min(0f)] private float PriceScale = 1f;

        // ==================== 属性 ====================
        public string Id => TierId;
        public string Label => TierLabel;
        public float DrawWeightValue => DrawWeight;
        public float PriceScaleValue => PriceScale;
    }
}
