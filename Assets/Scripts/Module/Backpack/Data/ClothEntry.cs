/*
* ┌──────────────────────────────────┐
* │  描    述: 一块布的配置，颜色、稀有度、BUFF 数组与可刷的异形全在这里
* │  类    名: ClothEntry.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 一条布就是一个颜色，颜色只用来让玩家认出这块布在往哪几条线上堆，不再挂阵营资产
* BUFF 用数组存，常见布一条，珍罕布三条，一块布能挂几条完全看配置
* 形状用数组存，同一块布可以刷出好几种异形，抽中哪种在抽货架时现定
* BUFF 的计价方式由 ClothBuffEntry.Mode 决定：按格累加走 PerCell，整块定额走 Flat
* 价钱只填单格基准价，最终价由总表按「基准 × 占格数 × 稀有度系数」现算，省得一块形状改尺寸就得重定价
* 空白布就是 BuffList 留空的那一块，它只占格不给加成，是玩家腾格子的唯一出路
*/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Module.Backpack.Data
{
    /// <summary>
    /// 一块布的配置条目，布料总表里的数组元素
    /// </summary>
    [Serializable]
    public sealed class ClothEntry
    {
        // ==================== 字段[外部设置] ====================
        [Tooltip("程序内唯一标识，例如 cloth_dive")]
        [SerializeField] private string ClothId;

        [Tooltip("界面显示的布名，例如 深潜布")]
        [SerializeField] private string ClothLabel;

        [Tooltip("布自己的底色，背包块与商品卡都吃这个色，留空按白处理")]
        [SerializeField] private Color ClothColor = new Color32(0xED, 0xE0, 0xC8, 0xFF);

        [Tooltip("取自总表稀有度档数组的下标，0 常见 1 少见 2 稀有 3 珍罕")]
        [SerializeField, Min(0)] private int RarityTierIndex;

        [Tooltip("占一格的基准价，最终价 = 基准价 × 占格数 × 稀有度系数 × 浮动")]
        [SerializeField, Min(0)] private int CellBasePrice = 14;

        [Tooltip("这块布挂的 BUFF，留空即空白布，只占格不给加成")]
        [SerializeField] private List<ClothBuffEntry> BuffList = new();

        [Tooltip("这块布能刷出的异形，取自形状模板库，抽货架时等概率挑一个")]
        [SerializeField] private List<GridShapeSO> ShapeList = new();

        // ==================== 属性 ====================
        public string Id => ClothId;
        public string Label => ClothLabel;
        public Color Color => ClothColor;
        public int RarityIndex => RarityTierIndex;
        public int CellBasePriceValue => CellBasePrice;
        public IReadOnlyList<ClothBuffEntry> Buffs => BuffList;
        public IReadOnlyList<GridShapeSO> Shapes => ShapeList;
        public int BuffCount => BuffList.Count;
        public int ShapeCount => ShapeList.Count;

        // 没挂 BUFF 就是空白布，商店按这个把它分进空白池，保证它不会被带加成的布挤走
        public bool IsBlank => BuffList.Count == 0;

        // ==================== Public Function ====================
        /// <summary>
        /// 取这块布在指定形状下的加成量，按这条 BUFF 的计价方式分流
        /// </summary>
        /// <param name="buff">本布的一条 BUFF</param>
        /// <param name="cells">异形占用的格子数</param>
        public float ValueFor(ClothBuffEntry buff, int cells)
        {
            if (buff == null)
                return 0f;

            return buff.ModeValue == ClothBuffMode.Flat
                ? buff.FlatValueOf
                : buff.PerCellValue * cells;
        }
    }
}
