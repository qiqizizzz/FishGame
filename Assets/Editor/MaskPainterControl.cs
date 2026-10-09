/*
* ┌──────────────────────────────────┐
* │  描    述: 位图网格绘制器，Inspector 里点格子直接改占用掩码
* │  类    名: MaskPainterControl.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 位图手填数字既难看又容易点错行，配置期一律靠点格子，写回的仍是 long 位图
* 可用格画浅米色填充加深棕描边，空格只描边，实心黑块在浅色皮肤上根本看不清
* 按下时记下这次是画还是擦，拖动途中沿用同一模式，连刷一片不会来回翻烧饼
* 翻转与旋转都以内容的包围盒为基准，不拿编辑视口的尺寸算，画完位置不跑偏
*/

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class MaskPainterControl
{
    // ==================== 常量与静态字段 ====================
    private const float CELL_SIZE = 26f;
    private const float CELL_SPACING = 3f;
    private const float CELL_BORDER = 1.5f;
    private const int STRIDE = 8;
    private const int SCAN_BIT_LIMIT = 63;

    private static readonly Color m_filledColor = new Color32(0xF5, 0xE6, 0xC8, 0xFF);
    private static readonly Color m_filledLine = new Color32(0x5B, 0x3A, 0x21, 0xFF);
    private static readonly Color m_emptyLine = new Color32(0x9E, 0x93, 0x82, 0xFF);
    private static readonly Color m_lockedFill = new Color32(0xD3, 0xCE, 0xC4, 0xFF);
    private static readonly Color m_hoverColor = new Color(1f, 0.95f, 0.84f, 0.45f);
    private static readonly Color m_errorLine = new Color32(0xC0, 0x39, 0x2B, 0xFF);

    private const int PAINT_NONE = 0;
    private const int PAINT_ADD = 1;
    private const int PAINT_ERASE = 2;

    // 拖动期间的涂抹模式，同一时刻只可能有一个绘制器在被拖
    private static int m_paintMode;

    // ==================== Public Function ====================
    /// <summary>
    /// 绘制可点选的网格，返回点选后的位图
    /// </summary>
    /// <param name="bits">当前位图</param>
    /// <param name="columns">视口列数</param>
    /// <param name="rows">视口行数</param>
    /// <param name="edited">本次是否发生了修改</param>
    public static long Paint(long bits, int columns, int rows, out bool edited)
    {
        return Paint(bits, columns, rows, null, out edited);
    }

    /// <summary>
    /// 绘制可点选的网格并把指定格子描红，用于边画边提示孤立格
    /// </summary>
    /// <param name="bits">当前位图</param>
    /// <param name="columns">视口列数</param>
    /// <param name="rows">视口行数</param>
    /// <param name="errorCells">要标红的格子集合，可为空</param>
    /// <param name="edited">本次是否发生了修改</param>
    public static long Paint(long bits, int columns, int rows, HashSet<Vector2Int> errorCells, out bool edited)
    {
        return paint(bits, columns, rows, 0L, false, errorCells, true, out edited);
    }

    /// <summary>
    /// 绘制带解锁层的网格，未解锁格压暗且不可点
    /// </summary>
    /// <param name="bits">当前点亮的格</param>
    /// <param name="columns">视口列数</param>
    /// <param name="rows">视口行数</param>
    /// <param name="unlockMask">解锁层掩码</param>
    /// <param name="edited">本次是否发生了修改</param>
    public static long PaintWithUnlock(long bits, int columns, int rows, long unlockMask, out bool edited)
    {
        return paint(bits, columns, rows, unlockMask, true, null, true, out edited);
    }

    /// <summary>
    /// 绘制只读网格，用于预览朝向与解锁阶段
    /// </summary>
    /// <param name="bits">要显示的位图</param>
    /// <param name="columns">视口列数</param>
    /// <param name="rows">视口行数</param>
    public static void PaintPreview(long bits, int columns, int rows)
    {
        paint(bits, columns, rows, 0L, false, null, false, out _);
    }

    /// <summary>
    /// 绘制只读网格并把指定格子描红，越界与不连通一眼看得见
    /// </summary>
    /// <param name="bits">要显示的位图</param>
    /// <param name="columns">视口列数</param>
    /// <param name="rows">视口行数</param>
    /// <param name="errorCells">要标红的格子集合，可为空</param>
    public static void PaintPreview(long bits, int columns, int rows, HashSet<Vector2Int> errorCells)
    {
        paint(bits, columns, rows, 0L, false, errorCells, false, out _);
    }

    /// <summary>
    /// 绕内容包围盒顺时针转 90 度
    /// </summary>
    /// <param name="bits">原始位图</param>
    public static long Rotate90(long bits)
    {
        return transform(bits, (cell, size) => new Vector2Int(size.y - 1 - cell.y, cell.x));
    }

    /// <summary>
    /// 绕内容包围盒左右翻转
    /// </summary>
    /// <param name="bits">原始位图</param>
    public static long FlipHorizontal(long bits)
    {
        return transform(bits, (cell, size) => new Vector2Int(size.x - 1 - cell.x, cell.y));
    }

    /// <summary>
    /// 绕内容包围盒上下翻转
    /// </summary>
    /// <param name="bits">原始位图</param>
    public static long FlipVertical(long bits)
    {
        return transform(bits, (cell, size) => new Vector2Int(cell.x, size.y - 1 - cell.y));
    }

    /// <summary>
    /// 点亮视口内全部格子
    /// </summary>
    /// <param name="columns">视口列数</param>
    /// <param name="rows">视口行数</param>
    public static long Fill(int columns, int rows)
    {
        long bits = 0;

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
                bits |= 1L << (row * STRIDE + column);
        }

        return bits;
    }

    /// <summary>
    /// 位图占用格数
    /// </summary>
    /// <param name="bits">原始位图</param>
    public static int CountCells(long bits)
    {
        int count = 0;

        for (int bit = 0; bit < SCAN_BIT_LIMIT; bit++)
        {
            if ((bits & (1L << bit)) != 0)
                count++;
        }

        return count;
    }

    /// <summary>
    /// 只读绘制分层网格，每层用自己的颜色铺格，上层盖住下层，同时把鼠标下的格报出去
    /// </summary>
    /// <param name="layerBits">各层的位图，下标小的先画，大的盖在上面</param>
    /// <param name="layerColors">各层的颜色，与位图一一对应，短了就按最后一格的颜色补</param>
    /// <param name="columns">视口列数</param>
    /// <param name="rows">视口行数</param>
    /// <param name="highlightLayer">要描粗边的层，负数表示一层都不标</param>
    /// <param name="hitCell">鼠标正压着的格，不在网格内时是 (-1,-1)</param>
    /// <param name="pressed">本次事件里左键正压着某个格，按下与拖动途中都为真</param>
    public static void PaintLayers(IReadOnlyList<long> layerBits, IReadOnlyList<Color> layerColors,
        int columns, int rows, int highlightLayer, out Vector2Int hitCell, out bool pressed)
    {
        hitCell = new Vector2Int(-1, -1);
        pressed = false;

        columns = Mathf.Max(1, columns);
        rows = Mathf.Max(1, rows);

        float pitch = CELL_SIZE + CELL_SPACING;
        Rect area = GUILayoutUtility.GetRect(columns * pitch - CELL_SPACING, rows * pitch - CELL_SPACING);
        Event current = Event.current;

        computeHover(area, pitch, columns, rows, current.mousePosition, out int hoverColumn, out int hoverRow);

        bool hovering = hoverColumn >= 0;

        if (hovering)
            hitCell = new Vector2Int(hoverColumn, hoverRow);

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                Rect cellRect = new Rect(area.x + column * pitch, area.y + row * pitch, CELL_SIZE, CELL_SIZE);
                int owner = topLayer(layerBits, 1L << (row * STRIDE + column));

                if (owner >= 0)
                    EditorGUI.DrawRect(cellRect, colorAt(layerColors, owner));

                if (hovering && current.type == EventType.Repaint && column == hoverColumn && row == hoverRow)
                    EditorGUI.DrawRect(cellRect, m_hoverColor);

                drawBorder(cellRect, owner == highlightLayer ? m_filledLine : m_emptyLine);
            }
        }

        if (!hovering)
            return;

        if (current.type == EventType.MouseDown && current.button == 0)
        {
            pressed = true;
            current.Use();
        }
        else if (current.type == EventType.MouseDrag && current.button == 0)
        {
            pressed = true;
            current.Use();
        }
    }

    // 这一格归最上面那一层，一层都没盖到时返回 -1
    private static int topLayer(IReadOnlyList<long> layerBits, long bit)
    {
        for (int i = layerBits.Count - 1; i >= 0; i--)
        {
            if ((layerBits[i] & bit) != 0)
                return i;
        }

        return -1;
    }

    // 颜色表比层数短时重复最后一格的颜色，配置半途中不该把绘制器搞崩
    private static Color colorAt(IReadOnlyList<Color> colors, int index)
    {
        if (colors == null || colors.Count == 0)
            return m_filledColor;

        return colors[Mathf.Min(index, colors.Count - 1)];
    }

    // ==================== Private Function ====================
    // 唯一绘制入口，hasLock 为真时 lockMask 外的格压暗且拒绝点选，interactive 为假时只画不接输入
    private static long paint(long bits, int columns, int rows, long lockMask, bool hasLock,
        HashSet<Vector2Int> errorCells, bool interactive, out bool edited)
    {
        edited = false;
        columns = Mathf.Max(1, columns);
        rows = Mathf.Max(1, rows);

        float pitch = CELL_SIZE + CELL_SPACING;
        Rect area = GUILayoutUtility.GetRect(columns * pitch - CELL_SPACING, rows * pitch - CELL_SPACING);
        Event current = Event.current;

        int hoverColumn = -1;
        int hoverRow = -1;

        if (interactive)
            computeHover(area, pitch, columns, rows, current.mousePosition, out hoverColumn, out hoverRow);

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                Rect cellRect = new Rect(area.x + column * pitch, area.y + row * pitch, CELL_SIZE, CELL_SIZE);
                long bit = 1L << (row * STRIDE + column);
                bool filled = (bits & bit) != 0;
                bool locked = hasLock && (lockMask & bit) == 0;
                bool error = errorCells != null && errorCells.Contains(new Vector2Int(column, row));

                EditorGUI.DrawRect(cellRect, locked ? m_lockedFill : (filled ? m_filledColor : Color.clear));

                if (interactive && current.type == EventType.Repaint && column == hoverColumn && row == hoverRow)
                    EditorGUI.DrawRect(cellRect, m_hoverColor);

                drawBorder(cellRect, error ? m_errorLine : (filled ? m_filledLine : m_emptyLine));
            }
        }

        if (!interactive)
            return bits;

        bool hovering = hoverRow >= 0;

        if (current.type == EventType.MouseDown && current.button == 0 && hovering)
        {
            long bit = 1L << (hoverRow * STRIDE + hoverColumn);

            if (!hasLock || (lockMask & bit) != 0)
            {
                bool wasFilled = (bits & bit) != 0;
                bits = wasFilled ? bits & ~bit : bits | bit;
                m_paintMode = wasFilled ? PAINT_ERASE : PAINT_ADD;
                edited = true;
                current.Use();
            }
        }
        else if (current.type == EventType.MouseDrag && current.button == 0 && m_paintMode != PAINT_NONE && hovering)
        {
            long bit = 1L << (hoverRow * STRIDE + hoverColumn);

            if (!hasLock || (lockMask & bit) != 0)
            {
                long next = m_paintMode == PAINT_ADD ? bits | bit : bits & ~bit;

                if (next != bits)
                {
                    bits = next;
                    edited = true;
                }
            }

            current.Use();
        }
        else if (current.type == EventType.MouseUp && current.button == 0)
        {
            m_paintMode = PAINT_NONE;
        }

        if (edited)
            GUI.changed = true;

        return bits;
    }

    // 命中格计算，落在格子间的缝隙上视为未命中
    private static void computeHover(Rect area, float pitch, int columns, int rows, Vector2 mouse,
        out int hoverColumn, out int hoverRow)
    {
        hoverColumn = -1;
        hoverRow = -1;

        if (!area.Contains(mouse))
            return;

        float localX = mouse.x - area.x;
        float localY = mouse.y - area.y;

        if (localX % pitch > CELL_SIZE || localY % pitch > CELL_SIZE)
            return;

        int column = Mathf.FloorToInt(localX / pitch);
        int row = Mathf.FloorToInt(localY / pitch);

        if (column < 0 || column >= columns || row < 0 || row >= rows)
            return;

        hoverColumn = column;
        hoverRow = row;
    }

    // 只描四边，不用 Handles 画线，避免编辑器主题下粗细不一致
    private static void drawBorder(Rect rect, Color color)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, CELL_BORDER), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - CELL_BORDER, rect.width, CELL_BORDER), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, CELL_BORDER, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - CELL_BORDER, rect.y, CELL_BORDER, rect.height), color);
    }

    private static long transform(long bits, Func<Vector2Int, Vector2Int, Vector2Int> mapper)
    {
        if (bits == 0)
            return 0;

        normalize(bits, out long aligned, out int width, out int height);
        Vector2Int size = new Vector2Int(width, height);
        long result = 0;

        foreach (Vector2Int cell in cells(aligned))
        {
            Vector2Int mapped = mapper(cell, size);
            result |= 1L << (mapped.y * STRIDE + mapped.x);
        }

        return result;
    }

    // 把内容平移到视口左上角，同时算出包围盒尺寸
    private static void normalize(long bits, out long aligned, out int width, out int height)
    {
        int minColumn = int.MaxValue;
        int minRow = int.MaxValue;
        int maxColumn = int.MinValue;
        int maxRow = int.MinValue;

        foreach (Vector2Int cell in cells(bits))
        {
            minColumn = Mathf.Min(minColumn, cell.x);
            minRow = Mathf.Min(minRow, cell.y);
            maxColumn = Mathf.Max(maxColumn, cell.x);
            maxRow = Mathf.Max(maxRow, cell.y);
        }

        if (minColumn == int.MaxValue)
        {
            aligned = 0;
            width = 0;
            height = 0;
            return;
        }

        aligned = 0;

        foreach (Vector2Int cell in cells(bits))
            aligned |= 1L << ((cell.y - minRow) * STRIDE + (cell.x - minColumn));

        width = maxColumn - minColumn + 1;
        height = maxRow - minRow + 1;
    }

    private static IEnumerable<Vector2Int> cells(long bits)
    {
        for (int bit = 0; bit < SCAN_BIT_LIMIT; bit++)
        {
            if ((bits & (1L << bit)) == 0)
                continue;

            yield return new Vector2Int(bit % STRIDE, bit / STRIDE);
        }
    }
}
