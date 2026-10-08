/*
* ┌──────────────────────────────────┐
* │  描    述: 形状模板 Inspector，点格子配形状并实时校验与预览朝向
* │  类    名: GridShapeSOEditor.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 位图藏在 HideInInspector 里，改它走 SerializedProperty，不暴露 long 输入框
* 格数超上限时拒绝写回：擦格子永远让格数变小，所以拦下加法不会把人锁死在错误状态
* 不连通的那一格当场描红，比只提示一句「不连通」省掉来回数格子的时间
* 朝向预览画的是去重后的真实朝向，方块只有一个朝向，界面就知道不该给旋转按钮
*/

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Module.Backpack.Data;
using Module.Backpack.Model;

[CustomEditor(typeof(GridShapeSO))]
public sealed class GridShapeSOEditor : Editor
{
    // ==================== 常量与静态字段 ====================
    private const string PREF_COLUMNS = "FishGame.Backpack.ShapeCanvasColumns";
    private const string PREF_ROWS = "FishGame.Backpack.ShapeCanvasRows";
    private const int DEFAULT_SIDE = 5;

    private static readonly Vector2Int[] m_directionOffsets =
    {
        new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0)
    };

    // ==================== Public Function ====================
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        GridShapeSO shape = (GridShapeSO)target;
        SerializedProperty bitsProperty = serializedObject.FindProperty("MaskBits");

        EditorGUILayout.PropertyField(serializedObject.FindProperty("ShapeId"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("ShapeLabel"));

        int columns = Mathf.Clamp(EditorPrefs.GetInt(PREF_COLUMNS, DEFAULT_SIDE), 2, ShapeMask.MAX_GRID_SIDE);
        int rows = Mathf.Clamp(EditorPrefs.GetInt(PREF_ROWS, DEFAULT_SIDE), 2, ShapeMask.MAX_GRID_SIDE);

        drawCanvasSize(columns, rows);
        long painted = drawPainter(bitsProperty.longValue, columns, rows);
        painted = clampCellLimit(painted, shape);

        if (painted != bitsProperty.longValue)
        {
            bitsProperty.longValue = painted;
            serializedObject.ApplyModifiedProperties();
            shape.InvalidateCache();
            EditorUtility.SetDirty(shape);
        }
        else
        {
            serializedObject.ApplyModifiedProperties();
        }

        drawStatistics(shape);
        drawOrientationPreviews(shape);
    }

    // ==================== Private Function ====================
    // 视口尺寸只管编辑区大小，不影响形状本身，所以存 EditorPrefs 不进资产
    private void drawCanvasSize(int columns, int rows)
    {
        EditorGUILayout.LabelField("编辑视口", EditorStyles.boldLabel);

        Rect region = GUILayoutUtility.GetRect(320f, 18f);
        GUI.Label(new Rect(region.x, region.y, 58f, 18f), "列 / 行");

        int newColumns = (int)GUI.HorizontalSlider(new Rect(region.x + 64f, region.y + 2f, 110f, 14f), columns, 2, ShapeMask.MAX_GRID_SIDE);
        int newRows = (int)GUI.HorizontalSlider(new Rect(region.x + 194f, region.y + 2f, 110f, 14f), rows, 2, ShapeMask.MAX_GRID_SIDE);

        GUI.Label(new Rect(region.x + 312f, region.y, 60f, 18f), $"{columns} × {rows}");

        if (newColumns != columns || newRows != rows)
        {
            EditorPrefs.SetInt(PREF_COLUMNS, newColumns);
            EditorPrefs.SetInt(PREF_ROWS, newRows);
            Repaint();
        }

        EditorGUILayout.Space(2f);
    }

    // 工具条一次给齐清空、填满、翻转、旋转，调形状不用回画布重描
    private long drawPainter(long bits, int columns, int rows)
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("清空", GUILayout.Width(56f)))
            bits = 0;

        if (GUILayout.Button("填满", GUILayout.Width(56f)))
            bits = MaskPainterControl.Fill(columns, rows);

        if (GUILayout.Button("横翻", GUILayout.Width(56f)))
            bits = MaskPainterControl.FlipHorizontal(bits);

        if (GUILayout.Button("纵翻", GUILayout.Width(56f)))
            bits = MaskPainterControl.FlipVertical(bits);

        if (GUILayout.Button("转 90°", GUILayout.Width(64f)))
            bits = MaskPainterControl.Rotate90(bits);

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4f);

        ShapeMask.TryCreate(bits, out ShapeMask pending);
        HashSet<Vector2Int> isolated = pending.CellCount > 0 && !pending.IsConnected() ? isolatedCells(pending) : null;

        return MaskPainterControl.Paint(bits, columns, rows, isolated, out _);
    }

    // 超过格数上限就退回已生效的值，让配置期根本产不出放不下的模板
    private long clampCellLimit(long bits, GridShapeSO shape)
    {
        int cells = MaskPainterControl.CountCells(bits);

        if (cells <= GridShapeSO.MAX_CELLS)
            return bits;

        EditorGUILayout.HelpBox($"已点 {cells} 格，超过上限 {GridShapeSO.MAX_CELLS}，本次改动不写入，当前生效的仍是 {shape.CellCount} 格形状。", MessageType.Error);
        return shape.Bits;
    }

    private void drawStatistics(GridShapeSO shape)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("校验结果", EditorStyles.boldLabel);

        ShapeMask baseMask = shape.Base;

        if (baseMask.CellCount == 0)
        {
            EditorGUILayout.HelpBox("还没有点亮任何格子。", MessageType.Warning);
            return;
        }

        bool connected = baseMask.IsConnected();
        string summary = $"格数 {baseMask.CellCount} · 包围盒 {baseMask.Width}×{baseMask.Height} · "
            + $"连通 {(connected ? "是" : "否")} · 朝向数 {shape.OrientationCount}";

        EditorGUILayout.LabelField(summary, connected ? EditorStyles.miniLabel : EditorStyles.boldLabel);

        if (!connected)
            EditorGUILayout.HelpBox("存在孤立的格子，这样的形状无法一次摆放，请在红框处补上一格。", MessageType.Error);
        else if (baseMask.CellCount < GridShapeSO.MIN_CELLS)
            EditorGUILayout.HelpBox($"格数低于下限 {GridShapeSO.MIN_CELLS}，单格件没有取舍可言。", MessageType.Error);

        if (shape.OrientationCount <= 1)
            EditorGUILayout.HelpBox("该形状旋转后与自身重合，界面不会给它旋转按钮。", MessageType.Info);
    }

    // 逐个画出真实朝向，去重后有几个就画几个，尺寸按各自包围盒收紧
    private void drawOrientationPreviews(GridShapeSO shape)
    {
        ShapeMask[] orientations = shape.Orientations;

        if (orientations.Length == 0)
            return;

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("朝向预览", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        for (int i = 0; i < orientations.Length; i++)
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField($"朝向 {i}", EditorStyles.miniLabel);
            MaskPainterControl.PaintPreview(orientations[i].Bits, Mathf.Max(1, orientations[i].Width), Mathf.Max(1, orientations[i].Height));
            EditorGUILayout.EndVertical();

            if (i < orientations.Length - 1)
                EditorGUILayout.Space(6f);
        }

        EditorGUILayout.EndHorizontal();
    }

    // 从第一格做四连通泛洪，没被走到的格子就是红框要标的那几格
    private static HashSet<Vector2Int> isolatedCells(ShapeMask mask)
    {
        HashSet<Vector2Int> cells = new();

        foreach (Vector2Int cell in mask.Cells())
            cells.Add(cell);

        HashSet<Vector2Int> reached = new();
        Queue<Vector2Int> pending = new();
        List<Vector2Int> ordered = new(cells);

        if (ordered.Count == 0)
            return reached;

        pending.Enqueue(ordered[0]);
        reached.Add(ordered[0]);

        while (pending.Count > 0)
        {
            Vector2Int current = pending.Dequeue();

            for (int direction = 0; direction < m_directionOffsets.Length; direction++)
            {
                Vector2Int neighbor = current + m_directionOffsets[direction];

                if (cells.Contains(neighbor) && reached.Add(neighbor))
                    pending.Enqueue(neighbor);
            }
        }

        cells.ExceptWith(reached);
        return cells;
    }
}
