/*
* ┌──────────────────────────────────┐
* │  描    述: 网格形状位图，旋转与放置判定的最小数据单元
* │  类    名: ShapeMask.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 位图按每行 8 位排布，位序 = 行 × 8 + 列，列数上限 7 时列方向越界也只落在本行空位上
* 这是刻意留的余量：整张位图一次左移即可完成平移，不必逐行修正跨行串位
* 行上限同为 7，最高位 6 × 8 + 6 = 54，仍在 long 的符号位之内，位移不会丢高位
* 结构体不可变，旋转与平移一律返回新实例，朝向列表由形状资产算一次后缓存
*/

using System.Collections.Generic;
using UnityEngine;

namespace Module.Backpack.Model
{
    public readonly struct ShapeMask
    {
        // ==================== 常量与静态字段 ====================
        // 每行占用的位数，比列数上限再多 1 位，保证列方向平移绝不串到下一行
        public const int ROW_STRIDE = 8;

        // 容器行列上限，同时受列余量与 long 符号位约束
        public const int MAX_GRID_SIDE = 7;

        // 扫描到 63 位为止，符号位不参与解析
        private const int SCAN_BIT_LIMIT = 63;

        // 四连通邻接偏移，顺序为上右下左
        private static readonly Vector2Int[] m_neighborOffsets =
        {
            new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0)
        };

        // ==================== 属性 ====================
        public readonly long Bits;
        public readonly int CellCount;
        public readonly int Width;
        public readonly int Height;

        // ==================== 构造 ====================
        private ShapeMask(long bits, int cellCount, int width, int height)
        {
            Bits = bits;
            CellCount = cellCount;
            Width = width;
            Height = height;
        }

        // ==================== Public Function ====================
        /// <summary>
        /// 从位图解析形状并归一化到左上角，位图为空时返回 false
        /// </summary>
        /// <param name="bits">原始位图</param>
        /// <param name="mask">解析结果</param>
        public static bool TryCreate(long bits, out ShapeMask mask)
        {
            mask = default;

            if (bits == 0)
                return false;

            int minColumn = int.MaxValue;
            int maxColumn = int.MinValue;
            int minRow = int.MaxValue;
            int maxRow = int.MinValue;
            int cellCount = 0;

            for (int bit = 0; bit < SCAN_BIT_LIMIT; bit++)
            {
                if ((bits & (1L << bit)) == 0)
                    continue;

                int column = bit % ROW_STRIDE;
                int row = bit / ROW_STRIDE;

                minColumn = Mathf.Min(minColumn, column);
                maxColumn = Mathf.Max(maxColumn, column);
                minRow = Mathf.Min(minRow, row);
                maxRow = Mathf.Max(maxRow, row);
                cellCount++;
            }

            // 高位脏数据：扫到有效位以外的位被忽略，此处 cellCount 必大于 0
            long normalized = 0;

            for (int bit = 0; bit < SCAN_BIT_LIMIT; bit++)
            {
                if ((bits & (1L << bit)) == 0)
                    continue;

                int column = bit % ROW_STRIDE - minColumn;
                int row = bit / ROW_STRIDE - minRow;
                normalized |= 1L << (row * ROW_STRIDE + column);
            }

            mask = new ShapeMask(normalized, cellCount, maxColumn - minColumn + 1, maxRow - minRow + 1);
            return true;
        }

        /// <summary>
        /// 展开归一化坐标，左上角为原点，坐标为（列, 行）
        /// </summary>
        public IEnumerable<Vector2Int> Cells()
        {
            for (int bit = 0; bit < SCAN_BIT_LIMIT; bit++)
            {
                if ((Bits & (1L << bit)) == 0)
                    continue;

                yield return new Vector2Int(bit % ROW_STRIDE, bit / ROW_STRIDE);
            }
        }

        /// <summary>
        /// 顺时针旋转 90 度，返回归一化后的新形状
        /// </summary>
        public ShapeMask Rotated()
        {
            long rotated = 0;

            foreach (Vector2Int cell in Cells())
            {
                int column = Height - 1 - cell.y;
                int row = cell.x;
                rotated |= 1L << (row * ROW_STRIDE + column);
            }

            return TryCreate(rotated, out ShapeMask mask) ? mask : this;
        }

        /// <summary>
        /// 平移到容器格上，包围盒元数据保持不变
        /// </summary>
        /// <param name="columnOffset">列方向位移</param>
        /// <param name="rowOffset">行方向位移</param>
        public ShapeMask Shifted(int columnOffset, int rowOffset)
        {
            long shifted = Bits << (rowOffset * ROW_STRIDE + columnOffset);
            return new ShapeMask(shifted, CellCount, Width, Height);
        }

        /// <summary>
        /// 四连通检查，不连通的形状无法一次摆放，必须在配置期就拦下
        /// </summary>
        public bool IsConnected()
        {
            List<int> pending = new();

            foreach (Vector2Int cell in Cells())
                pending.Add(cell.y * ROW_STRIDE + cell.x);

            if (pending.Count == 0)
                return false;

            HashSet<int> visited = new() { pending[0] };
            Stack<int> frontier = new();
            frontier.Push(pending[0]);

            while (frontier.Count > 0)
            {
                int current = frontier.Pop();
                int column = current % ROW_STRIDE;
                int row = current / ROW_STRIDE;

                for (int i = 0; i < m_neighborOffsets.Length; i++)
                {
                    int neighborColumn = column + m_neighborOffsets[i].x;
                    int neighborRow = row + m_neighborOffsets[i].y;

                    if (neighborColumn < 0 || neighborRow < 0)
                        continue;

                    int neighborBit = neighborRow * ROW_STRIDE + neighborColumn;

                    if (!pending.Contains(neighborBit) || !visited.Add(neighborBit))
                        continue;

                    frontier.Push(neighborBit);
                }
            }

            return visited.Count == pending.Count;
        }

        /// <summary>
        /// 生成去重后的朝向列表，方块类形状只有 1 个朝向，界面据此决定是否给旋转按钮
        /// </summary>
        /// <param name="baseMask">基准形状</param>
        public static ShapeMask[] BuildOrientations(ShapeMask baseMask)
        {
            List<ShapeMask> orientations = new();
            HashSet<long> seen = new();
            ShapeMask current = baseMask;

            for (int step = 0; step < 4; step++)
            {
                if (seen.Add(current.Bits))
                    orientations.Add(current);

                current = current.Rotated();
            }

            return orientations.ToArray();
        }
    }
}
