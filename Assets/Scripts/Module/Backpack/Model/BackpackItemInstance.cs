/*
* ┌──────────────────────────────────┐
* │  描    述: 背包内的一件装备实例，记录档位、朝向与摆放锚点
* │  类    名: BackpackItemInstance.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 只存最小状态：形状一律由「定义 + 档位」现查，避免副本数据和资产对不上
* 朝向索引由 GridShapeSO 的朝向列表回绕，旋转只会推进索引，不会改形状资产
* 锚点是包围盒左上角落在容器上的格，形状本身已归一化到左上角，两者相加即世界格
*/

using System.Collections.Generic;
using Module.Backpack.Data;
using UnityEngine;

namespace Module.Backpack.Model
{
    public sealed class BackpackItemInstance
    {
        // ==================== 公共字段 ====================
        public BackpackItemSO Definition;
        public int Tier = 1;
        public int OrientationIndex;
        public int AnchorColumn;
        public int AnchorRow;

        // ==================== 属性 ====================
        public GridShapeSO ShapeDef => Definition.ShapeAt(Tier);
        public ShapeMask Shape => ShapeDef.OrientationAt(OrientationIndex);
        public BackpackStatMod Mod => Definition.Mod;
        public int Cells => Shape.CellCount;
        public bool CanRotate => ShapeDef.OrientationCount > 1;

        // ==================== Public Function ====================
        /// <summary>
        /// 取当前位置下每个格子在容器中的世界坐标，界面画方块用
        /// </summary>
        public void FillWorldCells(List<Vector2Int> result)
        {
            result.Clear();

            foreach (Vector2Int cell in Shape.Cells())
                result.Add(new Vector2Int(cell.x + AnchorColumn, cell.y + AnchorRow));
        }

        /// <summary>
        /// 推进到下一个朝向，只改索引不动形状，是否放得下由 BackpackModel 判定
        /// </summary>
        public void AdvanceOrientation()
        {
            int count = ShapeDef.OrientationCount;
            OrientationIndex = count > 0 ? (OrientationIndex + 1) % count : 0;
        }

        /// <summary>
        /// 换档，档位被钳到定义的合法区间，朝向索引一并复位避免越界
        /// </summary>
        /// <param name="tier">目标档位，从 1 开始</param>
        public void SetTier(int tier)
        {
            int clamped = Mathf.Clamp(tier, 1, Definition.MaxTier);

            if (clamped == Tier)
                return;

            Tier = clamped;
            OrientationIndex = 0;
        }
    }
}
