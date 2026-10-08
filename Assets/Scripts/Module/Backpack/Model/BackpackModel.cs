/*
* ┌──────────────────────────────────┐
* │  描    述: 背包网格模型，放置、移动、旋转、移除的唯一裁决者
* │  类    名: BackpackModel.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 纯 C# 不挂 MonoBehaviour，界面与玩法都走这里问合法性，杜绝各处自己算位图
* 占用位图与物品列表互为冗余，每次改动都以列表为准整表重算，不手工加减位
* 判定一律先摘掉被移动物品自己的占用位，否则原地旋转永远和自己冲突，格子里转不动
* 摘的必须是旋转前的位，改完朝向再取会算成新形状的位置，老格子挡不住新格子
* 平移前显式查包围盒是否越界，位图左移会丢高位，只靠位与会被误判成可放置
*/

using System;
using System.Collections.Generic;
using UnityEngine;
using Utils.log;

namespace Module.Backpack.Model
{
    public sealed class BackpackModel
    {
        // ==================== 字段[私有] ====================
        private int m_columns;
        private int m_rows;
        private long m_unlockMask;
        private long m_occupiedMask;
        private readonly List<BackpackItemInstance> m_items = new();

        // ==================== 事件 ====================
        public event Action OnChanged;

        // ==================== 属性 ====================
        public int Columns => m_columns;
        public int Rows => m_rows;
        public long UnlockMask => m_unlockMask;
        public long OccupiedMask => m_occupiedMask;
        public IReadOnlyList<BackpackItemInstance> Items => m_items;
        public int ItemCount => m_items.Count;
        public int UsedCells => countBits(m_occupiedMask);
        public int UnlockedCells => countBits(m_unlockMask);
        public int FreeCells => UnlockedCells - UsedCells;

        // ==================== Public Function ====================
        /// <summary>
        /// 配置容器尺寸与解锁掩码，同时清空已有物品，用于开新局
        /// </summary>
        /// <param name="columns">容器列数</param>
        /// <param name="rows">容器行数</param>
        /// <param name="unlockMask">开局解锁掩码</param>
        public void Configure(int columns, int rows, long unlockMask)
        {
            m_items.Clear();
            m_occupiedMask = 0;
            m_columns = Mathf.Max(1, columns);
            m_rows = Mathf.Max(1, rows);
            m_unlockMask = unlockMask;
            notifyChanged();
        }

        /// <summary>
        /// 推进解锁阶段，新掩码必须盖住旧掩码和全部已放物品，否则拒绝
        /// </summary>
        /// <param name="unlockMask">目标解锁掩码</param>
        public bool TrySetUnlockMask(long unlockMask)
        {
            if ((m_unlockMask & ~unlockMask) != 0)
            {
                QLog.Error($"解锁掩码收缩，会关掉已解锁的格：旧 {m_unlockMask} 新 {unlockMask}");
                return false;
            }

            if ((m_occupiedMask & ~unlockMask) != 0)
            {
                QLog.Error("解锁掩码不覆盖已放好的物品，拒绝变更");
                return false;
            }

            m_unlockMask = unlockMask;
            notifyChanged();
            return true;
        }

        /// <summary>
        /// 指定物品是否占据某个格子，界面命中拾取用
        /// </summary>
        /// <param name="item">待查物品</param>
        /// <param name="column">容器列</param>
        /// <param name="row">容器行</param>
        public bool ItemCovers(BackpackItemInstance item, int column, int row)
        {
            if (item == null)
                return false;

            return (bitsOf(item) & bitAt(column, row)) != 0;
        }

        /// <summary>
        /// 取某个格子上的物品，格子为空或未解锁时返回 null
        /// </summary>
        /// <param name="column">容器列</param>
        /// <param name="row">容器行</param>
        public BackpackItemInstance FindItemAt(int column, int row)
        {
            long bit = bitAt(column, row);

            if ((m_occupiedMask & bit) == 0)
                return null;

            for (int i = 0; i < m_items.Count; i++)
            {
                if ((bitsOf(m_items[i]) & bit) != 0)
                    return m_items[i];
            }

            return null;
        }

        /// <summary>
        /// 只问合法性不改动状态，拖拽过程中每帧高亮都靠它
        /// </summary>
        /// <param name="shape">待放形状</param>
        /// <param name="anchorColumn">包围盒左上角列</param>
        /// <param name="anchorRow">包围盒左上角行</param>
        /// <param name="ignore">需要摘掉自身占用的物品，原地移动时传它</param>
        public bool CanPlace(ShapeMask shape, int anchorColumn, int anchorRow, BackpackItemInstance ignore = null)
        {
            return canPlace(shape, anchorColumn, anchorRow, ignore == null ? 0L : bitsOf(ignore));
        }

        /// <summary>
        /// 放入一件新物品，锚点由调用方给定，失败不产生任何副作用
        /// </summary>
        /// <param name="item">待放实例</param>
        public bool TryPlace(BackpackItemInstance item)
        {
            if (item == null || item.Definition == null)
                return false;

            if (m_items.Contains(item) || item.Shape.CellCount == 0)
                return false;

            if (!canPlace(item.Shape, item.AnchorColumn, item.AnchorRow, 0L))
                return false;

            m_items.Add(item);
            rebuildOccupiedMask();
            notifyChanged();
            return true;
        }

        /// <summary>
        /// 把已放物品移到新锚点，朝向不变
        /// </summary>
        /// <param name="item">待移物品</param>
        /// <param name="anchorColumn">新包围盒左上角列</param>
        /// <param name="anchorRow">新包围盒左上角行</param>
        public bool TryMove(BackpackItemInstance item, int anchorColumn, int anchorRow)
        {
            if (item == null || !m_items.Contains(item))
                return false;

            if (!canPlace(item.Shape, anchorColumn, anchorRow, bitsOf(item)))
                return false;

            item.AnchorColumn = anchorColumn;
            item.AnchorRow = anchorRow;
            rebuildOccupiedMask();
            notifyChanged();
            return true;
        }

        /// <summary>
        /// 原地转一档朝向；原锚点放不下就朝容器内收，再不行就近找空位，全找不到才失败并还原
        /// </summary>
        /// <param name="item">待转物品</param>
        public bool TryRotate(BackpackItemInstance item)
        {
            if (item == null || !m_items.Contains(item))
                return false;

            if (!item.CanRotate)
                return false;

            int keepIndex = item.OrientationIndex;
            int keepColumn = item.AnchorColumn;
            int keepRow = item.AnchorRow;
            ShapeMask oldShape = item.Shape;
            long selfBits = bitsOf(item);

            item.AdvanceOrientation();
            ShapeMask newShape = item.Shape;

            int column = Mathf.Min(keepColumn, m_columns - newShape.Width);
            int row = Mathf.Min(keepRow, m_rows - newShape.Height);

            if (canPlace(newShape, column, row, selfBits))
            {
                applyAnchor(item, column, row);
                return true;
            }

            if (tryNearbyAnchor(newShape, oldShape, keepColumn, keepRow, selfBits, out int bestColumn, out int bestRow))
            {
                applyAnchor(item, bestColumn, bestRow);
                return true;
            }

            item.OrientationIndex = keepIndex;
            item.AnchorColumn = keepColumn;
            item.AnchorRow = keepRow;
            return false;
        }

        /// <summary>
        /// 从网格取走一件物品，成功时返回 true
        /// </summary>
        /// <param name="item">待移除实例</param>
        public bool Remove(BackpackItemInstance item)
        {
            if (!m_items.Remove(item))
                return false;

            rebuildOccupiedMask();
            notifyChanged();
            return true;
        }

        /// <summary>
        /// 清空整张网格，物品列表与占用位一起归零
        /// </summary>
        public void ClearItems()
        {
            if (m_items.Count == 0 && m_occupiedMask == 0)
                return;

            m_items.Clear();
            m_occupiedMask = 0;
            notifyChanged();
        }

        /// <summary>
        /// 求某格在解锁掩码内是否可用，界面画锁格用
        /// </summary>
        /// <param name="column">容器列</param>
        /// <param name="row">容器行</param>
        public bool IsCellUnlocked(int column, int row)
        {
            return (m_unlockMask & bitAt(column, row)) != 0;
        }

        /// <summary>
        /// 求某格是否已解锁且空闲，暂存架的物件能否落回网格靠它
        /// </summary>
        /// <param name="column">容器列</param>
        /// <param name="row">容器行</param>
        public bool IsCellFree(int column, int row)
        {
            long bit = bitAt(column, row);
            return (m_unlockMask & bit) != 0 && (m_occupiedMask & bit) == 0;
        }

        // ==================== Private Function ====================
        // 位图层面的合法性：越界、越锁、越占三道关，excludeBits 是必须摘掉的自身旧占用
        private bool canPlace(ShapeMask shape, int anchorColumn, int anchorRow, long excludeBits)
        {
            if (shape.CellCount == 0)
                return false;

            if (!isInsideBoundingBox(shape, anchorColumn, anchorRow))
                return false;

            long mask = shape.Shifted(anchorColumn, anchorRow).Bits;

            if ((mask & ~m_unlockMask) != 0)
                return false;

            return (mask & (m_occupiedMask & ~excludeBits)) == 0;
        }

        private void applyAnchor(BackpackItemInstance item, int column, int row)
        {
            item.AnchorColumn = column;
            item.AnchorRow = row;
            rebuildOccupiedMask();
            notifyChanged();
        }

        // 全表扫锚点，取中心离原位最近的合法落点，5×5 容器最多 25 次判定
        private bool tryNearbyAnchor(ShapeMask newShape, ShapeMask oldShape, int oldColumn, int oldRow,
            long excludeBits, out int bestColumn, out int bestRow)
        {
            float bestScore = float.MaxValue;
            bestColumn = 0;
            bestRow = 0;
            float oldCenterX = oldColumn + oldShape.Width * 0.5f;
            float oldCenterY = oldRow + oldShape.Height * 0.5f;
            bool found = false;

            for (int row = 0; row <= m_rows - newShape.Height; row++)
            {
                for (int column = 0; column <= m_columns - newShape.Width; column++)
                {
                    if (!canPlace(newShape, column, row, excludeBits))
                        continue;

                    float deltaX = column + newShape.Width * 0.5f - oldCenterX;
                    float deltaY = row + newShape.Height * 0.5f - oldCenterY;
                    float score = deltaX * deltaX + deltaY * deltaY;

                    if (score >= bestScore)
                        continue;

                    bestScore = score;
                    bestColumn = column;
                    bestRow = row;
                    found = true;
                }
            }

            return found;
        }

        // 包围盒越界检查，位图平移丢高位之前必须先过这一关
        private bool isInsideBoundingBox(ShapeMask shape, int anchorColumn, int anchorRow)
        {
            return anchorColumn >= 0 && anchorRow >= 0
                && anchorColumn + shape.Width <= m_columns
                && anchorRow + shape.Height <= m_rows;
        }

        private long bitsOf(BackpackItemInstance item)
        {
            if (!isInsideBoundingBox(item.Shape, item.AnchorColumn, item.AnchorRow))
                return 0;

            return item.Shape.Shifted(item.AnchorColumn, item.AnchorRow).Bits;
        }

        private void rebuildOccupiedMask()
        {
            m_occupiedMask = 0;

            for (int i = 0; i < m_items.Count; i++)
                m_occupiedMask |= bitsOf(m_items[i]);
        }

        private void notifyChanged()
        {
            OnChanged?.Invoke();
        }

        private static long bitAt(int column, int row)
        {
            if (column < 0 || row < 0 || column >= ShapeMask.MAX_GRID_SIDE || row >= ShapeMask.MAX_GRID_SIDE)
                return 0;

            return 1L << (row * ShapeMask.ROW_STRIDE + column);
        }

        private static int countBits(long bits)
        {
            int count = 0;

            while (bits != 0)
            {
                bits &= bits - 1;
                count++;
            }

            return count;
        }
    }
}
