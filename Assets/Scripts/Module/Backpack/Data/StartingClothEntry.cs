/*
* ┌──────────────────────────────────┐
* │  描    述: 开局铺在背包里的一块布，写清是哪块布的哪个异形摆在哪个锚点
* │  类    名: StartingClothEntry.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 布用标识引用布料总表，不在这里再存一份形状与颜色，免得两处对不上
* 锚点是异形包围盒左上角落在容器上的格，位序与形状位图一致
* 摆放合不合规不在本类判，铺不上由 RunBackpack 另找锚点并把配置写错这件事报出来
*/

using System;
using UnityEngine;

namespace Module.Backpack.Data
{
    /// <summary>
    /// 开局布料的一条配置，背包容器配置里以数组元素呈现
    /// </summary>
    [Serializable]
    public sealed class StartingClothEntry
    {
        // ==================== 字段[外部设置] ====================
        [Tooltip("取自布料总表的布标识，例如 cloth_blank")]
        [SerializeField] private string ClothId;

        [Tooltip("这块布异形列表里的下标，编辑器里用形状名选")]
        [SerializeField, Min(0)] private int ShapeSlot;

        [Tooltip("异形的朝向下标，0 是形状资产里存的原始朝向")]
        [SerializeField, Min(0)] private int OrientationSlot;

        [Tooltip("锚点列，异形包围盒左上角落在容器的哪一列")]
        [SerializeField, Min(0)] private int CellColumn;

        [Tooltip("锚点行，异形包围盒左上角落在容器的哪一行")]
        [SerializeField, Min(0)] private int CellRow;

        // ==================== 属性 ====================
        public string Id => ClothId;
        public int ShapeIndex => ShapeSlot;
        public int OrientationIndex => OrientationSlot;
        public int AnchorColumn => CellColumn;
        public int AnchorRow => CellRow;
    }
}