/*
* ┌──────────────────────────────────┐
* │  描    述: 一块布上的一条 BUFF，写死能力线、每格额度与本布在这条线上的封顶
* │  类    名: ClothBuffEntry.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 一块布可以挂好几条，珍罕布就是靠同时堆几条线才卖得贵
* 正负一律存带符号的值，每格额度与封顶同号，聚合侧按封顶符号决定夹上界还是下界
* 本类必须是引用类型：聚合时靠实例身份给同一块布散在包里的所有碎片归组封顶
* 校验放在 ClothCatalogSO 里统一做，这里只存不判
*/

using System;
using UnityEngine;

namespace Module.Backpack.Data
{
    /// <summary>
    /// 布料身上的一条加成配置，Inspector 里以数组元素呈现
    /// </summary>
    [Serializable]
    public sealed class ClothBuffEntry
    {
        // ==================== 字段[外部设置] ====================
        [Tooltip("这条 BUFF 堆的能力线")]
        [SerializeField] private BackpackStatMod ModType;

        [Tooltip("每占一格给多少，负向线填负数")]
        [SerializeField] private float PerCell;

        [Tooltip("这块布在所有碎片上最多叠到多少，负向线填负数，0 表示这条线不给加成")]
        [SerializeField] private float Cap;

        // ==================== 属性 ====================
        public BackpackStatMod Mod => ModType;
        public float PerCellValue => PerCell;
        public float CapValue => Cap;
    }
}
