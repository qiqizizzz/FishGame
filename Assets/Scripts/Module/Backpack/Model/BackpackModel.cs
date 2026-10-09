/*
* ┌──────────────────────────────────┐
* │  描    述: 背包网格模型，布料板块与装备两层位图的唯一裁决者
* │  类    名: BackpackModel.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 纯 C# 不挂 MonoBehaviour，界面与玩法都走这里问合法性，杜绝各处自己算位图
* 两层：板块层铺出可用格，装备层只能压在板块上，可用格的全部来源就是板块的并集
* 板块之间不许重叠，装备之间不许重叠，装备压板块是本设计唯一合法的叠层
* 两份位图与两份列表互为冗余，每次改动都以列表为准整表重算，不手工加减位
* 判定一律先摘掉被移动物件自己的占用位，否则原地旋转永远和自己冲突，格子里转不动
* 摘的必须是旋转前的位，改完朝向再取会算成新形状的位置，老格子挡不住新格子
* 平移前显式查包围盒是否越界，位图左移会丢高位，只靠位与会被误判成可放置
* 板块拆走或挪走会叫压在上面的装备悬空，先预演再挑出悬空件，接不下就整次作废不脏状态
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
        private long m_plateMask;
        private long m_occupiedMask;
        private readonly List<BackpackItemInstance> m_plates = new();
        private readonly List<BackpackItemInstance> m_items = new();

        // 板块变更时先在这里预演一遍会被迫下架的装备，容量不够就整次作废
        private readonly List<BackpackItemInstance> m_pending = new();

        // ==================== 事件 ====================
        public event Action OnChanged;

        // ==================== 属性 ====================
        public int Columns => m_columns;
        public int Rows => m_rows;

        // 可用格掩码等价于板块并集，留着这个名字是给界面与旧调用方的
        public long UnlockMask => m_plateMask;
        public long PlateMask => m_plateMask;
        public long OccupiedMask => m_occupiedMask;
        public IReadOnlyList<BackpackItemInstance> Items => m_items;
        public IReadOnlyList<BackpackItemInstance> Plates => m_plates;
        public int ItemCount => m_items.Count;
        public int PlateCount => m_plates.Count;
        public int UsedCells => countBits(m_occupiedMask);
        public int UnlockedCells => countBits(m_plateMask);
        public int FreeCells => UnlockedCells - UsedCells;

        // ==================== Public Function ====================
        /// <summary>
        /// 配置容器尺寸并清空两层，开一局走这里，可用格要靠铺板块长出来
        /// </summary>
        /// <param name="columns">容器列数</param>
        /// <param name="rows">容器行数</param>
        public void Configure(int columns, int rows)
        {
            m_plates.Clear();
            m_items.Clear();
            m_plateMask = 0;
            m_occupiedMask = 0;
            m_columns = Mathf.Max(1, columns);
            m_rows = Mathf.Max(1, rows);
            notifyChanged();
        }

        /// <summary>
        /// 指定装备是否占据某个格子，界面命中拾取用
        /// </summary>
        /// <param name="item">待查装备</param>
        /// <param name="column">容器列</param>
        /// <param name="row">容器行</param>
        public bool ItemCovers(BackpackItemInstance item, int column, int row)
        {
            if (item == null)
                return false;

            return (BitsOf(item) & bitAt(column, row)) != 0;
        }

        /// <summary>
        /// 指定板块是否铺到某个格子，界面给布料染色用
        /// </summary>
        /// <param name="plate">待查板块</param>
        /// <param name="column">容器列</param>
        /// <param name="row">容器行</param>
        public bool PlateCovers(BackpackItemInstance plate, int column, int row)
        {
            if (plate == null)
                return false;

            return (BitsOf(plate) & bitAt(column, row)) != 0;
        }

        /// <summary>
        /// 取某个格子上的装备，这一格没被装备压住时返回 null，与有没有板块无关
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
                if ((BitsOf(m_items[i]) & bit) != 0)
                    return m_items[i];
            }

            return null;
        }

        /// <summary>
        /// 取铺在某个格子上的板块，这一格没底时返回 null
        /// </summary>
        /// <param name="column">容器列</param>
        /// <param name="row">容器行</param>
        public BackpackItemInstance FindPlateAt(int column, int row)
        {
            long bit = bitAt(column, row);

            if ((m_plateMask & bit) == 0)
                return null;

            for (int i = 0; i < m_plates.Count; i++)
            {
                if ((BitsOf(m_plates[i]) & bit) != 0)
                    return m_plates[i];
            }

            return null;
        }

        /// <summary>
        /// 取一件存放物当前占用的位图，包围盒越界时返回 0，配置错误由调用方判定
        /// </summary>
        /// <param name="instance">待查实例</param>
        public long BitsOf(BackpackItemInstance instance)
        {
            if (instance == null)
                return 0;

            return shiftedBits(instance.Shape, instance.AnchorColumn, instance.AnchorRow);
        }

        /// <summary>
        /// 只问装备放得下不放下，拖拽过程中每帧高亮都靠它
        /// </summary>
        /// <param name="shape">待放形状</param>
        /// <param name="anchorColumn">包围盒左上角列</param>
        /// <param name="anchorRow">包围盒左上角行</param>
        /// <param name="ignore">需要摘掉自身占用的装备，原地移动时传它</param>
        public bool CanPlace(ShapeMask shape, int anchorColumn, int anchorRow, BackpackItemInstance ignore = null)
        {
            return canPlaceItem(shape, anchorColumn, anchorRow, ignore == null ? 0L : BitsOf(ignore));
        }

        /// <summary>
        /// 只问板块铺不铺得下，只看别的板块，装备压在上头不算冲突
        /// </summary>
        /// <param name="shape">待放形状</param>
        /// <param name="anchorColumn">包围盒左上角列</param>
        /// <param name="anchorRow">包围盒左上角行</param>
        /// <param name="ignore">需要摘掉自身占用的板块，原地移动时传它</param>
        public bool CanPlacePlate(ShapeMask shape, int anchorColumn, int anchorRow, BackpackItemInstance ignore = null)
        {
            return canPlacePlate(shape, anchorColumn, anchorRow, ignore == null ? 0L : BitsOf(ignore));
        }

        /// <summary>
        /// 放入一件新装备，锚点由调用方给定，失败不产生任何副作用
        /// </summary>
        /// <param name="item">待放实例</param>
        public bool TryPlace(BackpackItemInstance item)
        {
            if (item == null || item.Definition == null)
            {
                QLog.Error("待放装备或它的定义为空，网格不收");
                return false;
            }

            if (m_items.Contains(item) || item.Shape.CellCount == 0)
                return false;

            if (!canPlaceItem(item.Shape, item.AnchorColumn, item.AnchorRow, 0L))
                return false;

            m_items.Add(item);
            rebuildOccupiedMask();
            notifyChanged();
            return true;
        }

        /// <summary>
        /// 铺上一块新板块，它只会往外添可用格，不可能叫已有的装备悬空
        /// </summary>
        /// <param name="plate">待放板块</param>
        public bool TryPlacePlate(BackpackItemInstance plate)
        {
            if (plate == null || plate.Definition == null)
            {
                QLog.Error("待铺板块或它的定义为空，网格不收");
                return false;
            }

            if (m_plates.Contains(plate) || plate.Shape.CellCount == 0)
                return false;

            if (!canPlacePlate(plate.Shape, plate.AnchorColumn, plate.AnchorRow, 0L))
                return false;

            m_plates.Add(plate);
            rebuildPlateMask();
            notifyChanged();
            return true;
        }

        /// <summary>
        /// 把已放装备移到新锚点，朝向不变
        /// </summary>
        /// <param name="item">待移装备</param>
        /// <param name="anchorColumn">新包围盒左上角列</param>
        /// <param name="anchorRow">新包围盒左上角行</param>
        public bool TryMove(BackpackItemInstance item, int anchorColumn, int anchorRow)
        {
            if (item == null || !m_items.Contains(item))
                return false;

            if (!canPlaceItem(item.Shape, anchorColumn, anchorRow, BitsOf(item)))
                return false;

            item.AnchorColumn = anchorColumn;
            item.AnchorRow = anchorRow;
            rebuildOccupiedMask();
            notifyChanged();
            return true;
        }

        /// <summary>
        /// 原地转一档装备朝向；原锚点放不下就朝容器内收，再不行就近找空位，全找不到才失败并还原
        /// </summary>
        /// <param name="item">待转装备</param>
        public bool TryRotate(BackpackItemInstance item)
        {
            if (item == null || !m_items.Contains(item))
                return false;

            int keepIndex = item.OrientationIndex;
            int keepColumn = item.AnchorColumn;
            int keepRow = item.AnchorRow;

            if (!rotate(item, false, 0, null))
            {
                item.OrientationIndex = keepIndex;
                item.AnchorColumn = keepColumn;
                item.AnchorRow = keepRow;
                return false;
            }

            return true;
        }

        /// <summary>
        /// 挪一块布到新锚点，失去底子的装备一律追加进 orphans
        /// </summary>
        /// <param name="plate">待移板块</param>
        /// <param name="anchorColumn">新包围盒左上角列</param>
        /// <param name="anchorRow">新包围盒左上角行</param>
        /// <param name="maxOrphans">调用方能接下的悬空件数，不够就整次拒绝</param>
        /// <param name="orphans">接住被迫下架的装备，可为空</param>
        public bool TryMovePlate(BackpackItemInstance plate, int anchorColumn, int anchorRow,
            int maxOrphans, List<BackpackItemInstance> orphans)
        {
            if (plate == null || !m_plates.Contains(plate))
                return false;

            long selfBits = BitsOf(plate);

            if (!canPlacePlate(plate.Shape, anchorColumn, anchorRow, selfBits))
                return false;

            long candidate = (m_plateMask & ~selfBits) | shiftedBits(plate.Shape, anchorColumn, anchorRow);

            if (!planPlateChange(candidate, maxOrphans))
                return false;

            plate.AnchorColumn = anchorColumn;
            plate.AnchorRow = anchorRow;
            commitPlateChange(orphans);
            return true;
        }

        /// <summary>
        /// 原地转一档板块朝向，转完失去底子的装备同样走 orphans
        /// </summary>
        /// <param name="plate">待转板块</param>
        /// <param name="maxOrphans">调用方能接下的悬空件数，不够就整次拒绝</param>
        /// <param name="orphans">接住被迫下架的装备，可为空</param>
        public bool TryRotatePlate(BackpackItemInstance plate, int maxOrphans, List<BackpackItemInstance> orphans)
        {
            if (plate == null || !m_plates.Contains(plate))
                return false;

            int keepIndex = plate.OrientationIndex;
            int keepColumn = plate.AnchorColumn;
            int keepRow = plate.AnchorRow;

            if (!rotate(plate, true, maxOrphans, orphans))
            {
                plate.OrientationIndex = keepIndex;
                plate.AnchorColumn = keepColumn;
                plate.AnchorRow = keepRow;
                return false;
            }

            return true;
        }

        /// <summary>
        /// 从网格拆走一块布，失去底子的装备追加进 orphans；调用方接不下就整次不动
        /// </summary>
        /// <param name="plate">待拆板块</param>
        /// <param name="maxOrphans">调用方能接下的悬空件数，不够就整次拒绝</param>
        /// <param name="orphans">接住被迫下架的装备，可为空</param>
        public bool TryRemovePlate(BackpackItemInstance plate, int maxOrphans, List<BackpackItemInstance> orphans)
        {
            if (plate == null || !m_plates.Contains(plate))
                return false;

            long candidate = m_plateMask & ~BitsOf(plate);

            if (!planPlateChange(candidate, maxOrphans))
                return false;

            m_plates.Remove(plate);
            commitPlateChange(orphans);
            return true;
        }

        /// <summary>
        /// 从网格取走一件装备，成功时返回 true
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
        /// 清空装备层，板块一片不动，格子还在
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
        /// 两层一起清空，重开与读档兜底用
        /// </summary>
        public void ClearAll()
        {
            if (m_plates.Count == 0 && m_items.Count == 0 && m_plateMask == 0 && m_occupiedMask == 0)
                return;

            m_plates.Clear();
            m_items.Clear();
            m_plateMask = 0;
            m_occupiedMask = 0;
            notifyChanged();
        }

        /// <summary>
        /// 求某格有没有板块铺底，界面画锁格与染布色都靠它
        /// </summary>
        /// <param name="column">容器列</param>
        /// <param name="row">容器行</param>
        public bool IsCellUnlocked(int column, int row)
        {
            return (m_plateMask & bitAt(column, row)) != 0;
        }

        /// <summary>
        /// 求某格有底且没被装备压住，暂存架的装备能否落回网格靠它
        /// </summary>
        /// <param name="column">容器列</param>
        /// <param name="row">容器行</param>
        public bool IsCellFree(int column, int row)
        {
            long bit = bitAt(column, row);
            return (m_plateMask & bit) != 0 && (m_occupiedMask & bit) == 0;
        }

        /// <summary>
        /// 把 supportMask 盖不住的装备挑进 result，只问不改，供调用方先估容量
        /// </summary>
        /// <param name="supportMask">假定还在的可用格</param>
        /// <param name="result">接住悬空装备的列表，由调用方负责清空</param>
        public void CollectFloatingItems(long supportMask, List<BackpackItemInstance> result)
        {
            if (result == null)
                return;

            long floating = m_occupiedMask & ~supportMask;

            if (floating == 0)
                return;

            for (int i = 0; i < m_items.Count; i++)
            {
                if ((BitsOf(m_items[i]) & floating) != 0)
                    result.Add(m_items[i]);
            }
        }

        // ==================== Private Function ====================
        // 板块层落点：只查越界与压到别的板块，装备在上头不算冲突
        private bool canPlacePlate(ShapeMask shape, int anchorColumn, int anchorRow, long excludeBits)
        {
            if (shape.CellCount == 0)
                return false;

            if (!isInsideBoundingBox(shape, anchorColumn, anchorRow))
                return false;

            long mask = shape.Shifted(anchorColumn, anchorRow).Bits;

            return (mask & (m_plateMask & ~excludeBits)) == 0;
        }

        // 装备层落点：越界、没有底、被别的装备压住，三道关
        private bool canPlaceItem(ShapeMask shape, int anchorColumn, int anchorRow, long excludeBits)
        {
            if (shape.CellCount == 0)
                return false;

            if (!isInsideBoundingBox(shape, anchorColumn, anchorRow))
                return false;

            long mask = shape.Shifted(anchorColumn, anchorRow).Bits;

            if ((mask & ~m_plateMask) != 0)
                return false;

            return (mask & (m_occupiedMask & ~excludeBits)) == 0;
        }

        // 转朝向的两层共用流程：先就地收，再就近找位，板块层还要多问一句悬空的接不接得下
        private bool rotate(BackpackItemInstance instance, bool plateLayer, int maxOrphans,
            List<BackpackItemInstance> orphans)
        {
            if (instance == null)
                return false;

            if (!instance.CanRotate)
                return false;

            ShapeMask oldShape = instance.Shape;
            long selfBits = BitsOf(instance);
            int keepColumn = instance.AnchorColumn;
            int keepRow = instance.AnchorRow;

            instance.AdvanceOrientation();
            ShapeMask newShape = instance.Shape;

            int column = Mathf.Min(keepColumn, m_columns - newShape.Width);
            int row = Mathf.Min(keepRow, m_rows - newShape.Height);

            bool fits = plateLayer
                ? canPlacePlate(newShape, column, row, selfBits)
                : canPlaceItem(newShape, column, row, selfBits);

            if (!fits && !tryNearbyAnchor(newShape, oldShape, keepColumn, keepRow, selfBits, plateLayer, out column, out row))
                return false;

            if (!plateLayer)
            {
                instance.AnchorColumn = column;
                instance.AnchorRow = row;
                rebuildOccupiedMask();
                notifyChanged();
                return true;
            }

            long candidate = (m_plateMask & ~selfBits) | shiftedBits(newShape, column, row);

            if (!planPlateChange(candidate, maxOrphans))
                return false;

            instance.AnchorColumn = column;
            instance.AnchorRow = row;
            commitPlateChange(orphans);
            return true;
        }

        // 预演板块变更：挑出失去底子的装备，调用方接不下就不动任何状态
        private bool planPlateChange(long candidateMask, int maxOrphans)
        {
            m_pending.Clear();
            CollectFloatingItems(candidateMask, m_pending);
            return m_pending.Count <= maxOrphans;
        }

        // 板块变更收尾：以列表为准重算两层位，预演挑出的装备摘给调用方
        private void commitPlateChange(List<BackpackItemInstance> orphans)
        {
            rebuildPlateMask();

            for (int i = 0; i < m_pending.Count; i++)
            {
                m_items.Remove(m_pending[i]);

                if (orphans != null)
                    orphans.Add(m_pending[i]);
            }

            m_pending.Clear();
            rebuildOccupiedMask();
            notifyChanged();
        }

        // 全表扫锚点，取中心离原位最近的合法落点，5×5 容器最多 25 次判定
        private bool tryNearbyAnchor(ShapeMask newShape, ShapeMask oldShape, int oldColumn, int oldRow,
            long excludeBits, bool plateLayer, out int bestColumn, out int bestRow)
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
                    bool fits = plateLayer
                        ? canPlacePlate(newShape, column, row, excludeBits)
                        : canPlaceItem(newShape, column, row, excludeBits);

                    if (!fits)
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

        // 平移到容器格上的位图，越界给 0，免得半截形状算成合法占用
        private long shiftedBits(ShapeMask shape, int anchorColumn, int anchorRow)
        {
            if (!isInsideBoundingBox(shape, anchorColumn, anchorRow))
                return 0;

            return shape.Shifted(anchorColumn, anchorRow).Bits;
        }

        private void rebuildPlateMask()
        {
            m_plateMask = 0;

            for (int i = 0; i < m_plates.Count; i++)
                m_plateMask |= BitsOf(m_plates[i]);
        }

        private void rebuildOccupiedMask()
        {
            m_occupiedMask = 0;

            for (int i = 0; i < m_items.Count; i++)
                m_occupiedMask |= BitsOf(m_items[i]);
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