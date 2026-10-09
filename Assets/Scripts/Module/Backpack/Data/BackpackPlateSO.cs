/*
* ┌──────────────────────────────────┐
* │  描    述: 板块纸页藏品定义，一张纸配一种异形，单档不进升档体系
* │  类    名: BackpackPlateSO.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 板块和装备走同一条摆放链路：档位永远是一级，形状直接给，加成按占格数现算
* 能力线、每格值与板块封顶一律问纸页要，同一张纸换异形只是占地不同，不改变它在堆哪条线
* 价钱自成一项，不蹭装备的档位价表
*
* 已被 ClothCatalogSO 取代：本类连同 Factions/Pages/Plates 三批资产一起作为遗留容器保留，
* 不再实现 IBackpackDef，也不再进商店奖池，要清理旧资产时说一声即可
*/

using UnityEngine;
using Utils.log;

namespace Module.Backpack.Data
{
    public sealed class BackpackPlateSO : ScriptableObject
    {
        // ==================== 常量 ====================
        private const int PLATE_TIER = 1;

        // ==================== 字段[外部设置] ====================
        [Tooltip("程序内唯一标识，例如 plate_spool_corner")]
        [SerializeField] private string PlateId;
        [Tooltip("界面显示名，例如 线轴纸·三格拐角")]
        [SerializeField] private string PlateLabel;
        [Tooltip("图标，素材未就位时留空，界面按名称显示")]
        [SerializeField] private Sprite PlateIcon;
        [Tooltip("所属纸页，决定能力线、每格值、纸页封顶与底色")]
        [SerializeField] private BuffPageSO PageAsset;
        [Tooltip("占格异形，取自形状模板库")]
        [SerializeField] private GridShapeSO ShapeAsset;
        [Tooltip("商店基准售价（金币）")]
        [SerializeField, Min(0)] private int Price = 40;

        // ==================== 属性 ====================
        public string Id => PlateId;
        public string Label => PlateLabel;
        public Sprite Icon => PlateIcon;
        public BackpackDefKind Kind => BackpackDefKind.Cloth;
        public BuffPageSO Page => PageAsset;
        public BuffFactionSO Faction => PageAsset != null ? PageAsset.Faction : null;
        public BackpackStatMod Mod => PageAsset != null ? PageAsset.Mod : BackpackStatMod.None;
        public GridShapeSO Shape => ShapeAsset;
        public int BasePrice => Price;
        public int MaxTier => PLATE_TIER;

        // ==================== Public Function ====================
        /// <summary>
        /// 板块不分档，任何档位都返回同一个异形
        /// </summary>
        /// <param name="tier">档位，从 1 开始</param>
        public GridShapeSO ShapeAt(int tier)
        {
            return ShapeAsset;
        }

        /// <summary>
        /// 加成量 = 纸页每格值 × 占格数，占得多的纸页天然比单格贵
        /// </summary>
        /// <param name="tier">档位，从 1 开始</param>
        public float ValueAt(int tier)
        {
            if (PageAsset == null || ShapeAsset == null)
                return 0f;

            return PageAsset.ValueForCells(ShapeAsset.CellCount);
        }

        // ==================== Private Function ====================
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(PlateId))
                QLog.Error($"[{nameof(BackpackPlateSO)}] 板块缺少标识：{name}");

            if (PageAsset == null)
                QLog.Error($"[{nameof(BackpackPlateSO)}] 板块未指定纸页：{PlateId}");

            if (ShapeAsset == null)
                QLog.Error($"[{nameof(BackpackPlateSO)}] 板块未指定异形：{PlateId}");
        }
    }
}
