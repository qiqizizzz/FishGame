/*
* ┌──────────────────────────────────┐
* │  描    述: 板块纸页定义，一张纸只管一条能力线
* │  类    名: BuffPageSO.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 一板块 = 一纸页 = 一条线，不做复合线，玩家看颜色就知道这张纸在往哪条线上堆
* 每占一格给一点小额常驻加成，纸页自带一个板块封顶，防住「铺满整张网只吃一条线」的无脑堆叠
* 正负一律存带符号的值：负向线每格值与封顶都是负数，聚合侧按封顶符号选 min 或 max，不再单列方向
* 空白纸页 Mod 为 None、每格值为 0，只用来填缝，必须进奖池且权重不低
*
* 已被 ClothCatalogSO 取代：纸页与阵营的字段全并进了布料总表的每一行，
* 本类只是遗留容器，右键新建入口已撤掉，也不再进商店奖池，要清理旧资产时说一声即可
*/

using UnityEngine;
using Utils.log;

namespace Module.Backpack.Data
{
    public sealed class BuffPageSO : ScriptableObject
    {
        // ==================== 字段[外部设置] ====================
        [Tooltip("程序内唯一标识，例如 page_spool")]
        [SerializeField] private string PageId;
        [Tooltip("界面显示的纸名，例如 线轴纸")]
        [SerializeField] private string PageLabel;
        [Tooltip("所属阵营，决定纸页底色")]
        [SerializeField] private BuffFactionSO FactionAsset;
        [Tooltip("这张纸堆的能力线，空白纸页选 None")]
        [SerializeField] private BackpackStatMod ModType;
        [Tooltip("每占一格给多少，负向线填负数")]
        [SerializeField] private float PerCell;
        [Tooltip("这张纸在所有格子上最多叠到多少，负向线填负数，0 表示这张纸不给加成")]
        [SerializeField] private float BonusCap;

        // ==================== 属性 ====================
        public string Id => PageId;
        public string Label => PageLabel;
        public BuffFactionSO Faction => FactionAsset;
        public BackpackStatMod Mod => ModType;
        public float PerCellValue => PerCell;
        public float PageBonusCap => BonusCap;

        // ==================== Public Function ====================
        /// <summary>
        /// 按占格数算这块纸的单块加成量，聚合时还要再受纸页封顶夹一次
        /// </summary>
        /// <param name="cells">板块占用的格子数</param>
        public float ValueForCells(int cells)
        {
            return PerCell * cells;
        }

        // ==================== Private Function ====================
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(PageId))
                QLog.Error($"[{nameof(BuffPageSO)}] 纸页缺少标识：{name}");

            if (FactionAsset == null)
                QLog.Error($"[{nameof(BuffPageSO)}] 纸页未指定阵营，界面无从取色：{PageId}");

            if (ModType == BackpackStatMod.None && PerCell != 0f)
                QLog.Error($"[{nameof(BuffPageSO)}] 空白纸页不该带加成：{PageId}");
        }
    }
}
