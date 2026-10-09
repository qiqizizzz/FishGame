/*
* ┌──────────────────────────────────┐
* │  描    述: 背包配置 Inspector，开局布料用分层彩色画布摆放，容器定价与暂存架走常规字段
* │  类    名: BackpackConfigSOEditor.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 容器里能用的小格全部由布铺出来，所以这里画的不是「解锁了哪几格」而是「开局铺了哪几块布」
* 一块布占一层颜色，画布只读，按在哪块布上就选中它，再拖就把它的锚点拖到鼠标下的格
* 布与异形都按名字选，底下存的仍是布标识加下标，配置期不该逼人读 long
* 分层位图只在某块布的值变了才算一次，每帧重算会把同一句配置错误刷成一屏
* 重叠格与越界块一律标出来，运行期允许布压布，但开局就该铺得干干净净
* 逐级解锁那套字段已经从配置里删了，阶段画布与包含校验跟着一起消失
*/

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Module.Backpack.Data;
using Module.Backpack.Model;

[CustomEditor(typeof(BackpackConfigSO))]
public sealed class BackpackConfigSOEditor : Editor
{
    // ==================== 常量与静态字段 ====================
    private const int UNSET_LAYER = -1;
    private const int MIN_CONTAINER_SIDE = 2;
    private const int SCAN_BIT_LIMIT = 63;
    private const int STRIDE = ShapeMask.ROW_STRIDE;
    private const float POPUP_WIDTH = 140f;
    private const float SLOT_LABEL_WIDTH = 30f;
    private const float SLOT_FIELD_WIDTH = 34f;
    private const float SWATCH_SIZE = 14f;
    private const float DELETE_BUTTON_WIDTH = 30f;

    private static readonly Color m_unmatchedColor = new Color32(0x8C, 0x8C, 0x8C, 0xFF);
    private static readonly string[] m_unmatchedShapes = { "未匹配到布" };

    // 下拉框选完只拿得到下标，布标识与异形数先摊平在这儿，写回时不用再回总表查一遍
    private static string[] m_clothIds = new string[0];
    private static int[] m_shapeCounts = new int[0];

    // ==================== 字段[私有] ====================
    private readonly List<long> m_layerBits = new();
    private readonly List<Color> m_layerColors = new();
    private readonly List<string> m_layerSignatures = new();
    private readonly HashSet<Vector2Int> m_errorCells = new();

    // 画布上按到的那块布，对应条目行跟着加粗，玩家知道自己在动哪一块
    private int m_selectedCloth = UNSET_LAYER;

    // 同一次按住拖动只算一步撤销，一次点击翻十格也不该堆十步
    private bool m_strokeUndoRecorded;

    // 删除按在绘制中途会打乱 Begin/End 配对，先记下来画完再删
    private int m_pendingDelete = UNSET_LAYER;

    // ==================== Public Function ====================
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        BackpackConfigSO config = (BackpackConfigSO)target;
        SerializedProperty gridColumns = serializedObject.FindProperty("GridColumns");
        SerializedProperty gridRows = serializedObject.FindProperty("GridRows");
        SerializedProperty clothCatalogProperty = serializedObject.FindProperty("ClothCatalog");
        SerializedProperty clothList = serializedObject.FindProperty("StartingClothList");

        EditorGUILayout.LabelField("容器网格", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(gridColumns);
        EditorGUILayout.PropertyField(gridRows);

        int columns = Mathf.Clamp(gridColumns.intValue, MIN_CONTAINER_SIDE, ShapeMask.MAX_GRID_SIDE);
        int rows = Mathf.Clamp(gridRows.intValue, MIN_CONTAINER_SIDE, ShapeMask.MAX_GRID_SIDE);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("布料来源", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(clothCatalogProperty, new GUIContent("布料总表"));

        ClothCatalogSO catalog = clothCatalogProperty.objectReferenceValue as ClothCatalogSO;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("开局布料", EditorStyles.boldLabel);
        drawCloths(config, catalog, clothList, columns, rows);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("商品定价", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("TierPriceList"), new GUIContent("一/二/三级售价"), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("暂存架", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("StashCapacityLimit"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("开局装备", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("StartingItemList"), new GUIContent("开局装备"), true);

        bool changed = GUI.changed;

        // 2022.3 没有 hasUnappliedModifiedProperties，写回一律交给标准流程
        serializedObject.ApplyModifiedProperties();

        if (changed)
            Repaint();
    }

    // ==================== Private Function ====================
    // 开局布料这一段：彩色画布在上，条目在中，并集预览在底
    private void drawCloths(BackpackConfigSO config, ClothCatalogSO catalog, SerializedProperty list,
        int columns, int rows)
    {
        if (catalog == null)
        {
            EditorGUILayout.HelpBox("还没有指定布料总表，开局布取不到颜色与异形。", MessageType.Warning);
            return;
        }

        if (catalog.ClothCount == 0)
        {
            EditorGUILayout.HelpBox("布料总表里一块布都没有，先去总表加一块。", MessageType.Warning);
            return;
        }

        refreshLayers(config, catalog, list);
        drawCanvas(config, list, columns, rows);

        EditorGUILayout.Space();

        drawEntryRows(config, catalog, list, columns, rows);
        drawListToolbar(config, catalog, list);

        EditorGUILayout.Space();

        drawUnionPreview(list, columns, rows);
        applyPendingDelete(config, list);
    }

    // 分层位图只在某块布的值变了才算一次，报错也就只报一次
    private void refreshLayers(BackpackConfigSO config, ClothCatalogSO catalog, SerializedProperty list)
    {
        int count = list.arraySize;

        while (m_layerSignatures.Count > count)
        {
            m_layerSignatures.RemoveAt(m_layerSignatures.Count - 1);
            m_layerBits.RemoveAt(m_layerBits.Count - 1);
            m_layerColors.RemoveAt(m_layerColors.Count - 1);
        }

        for (int i = 0; i < count; i++)
        {
            SerializedProperty element = list.GetArrayElementAtIndex(i);
            string clothId = element.FindPropertyRelative("ClothId").stringValue;
            int shapeIndex = element.FindPropertyRelative("ShapeSlot").intValue;
            int orientation = element.FindPropertyRelative("OrientationSlot").intValue;
            int column = element.FindPropertyRelative("CellColumn").intValue;
            int row = element.FindPropertyRelative("CellRow").intValue;

            string signature = $"{catalog.name}|{clothId}|{shapeIndex}|{orientation}|{column}|{row}";

            if (i < m_layerSignatures.Count && m_layerSignatures[i] == signature)
                continue;

            config.TryBuildBits(clothId, shapeIndex, orientation, column, row, out long bits);

            while (m_layerSignatures.Count <= i)
            {
                m_layerSignatures.Add(string.Empty);
                m_layerBits.Add(0L);
                m_layerColors.Add(m_unmatchedColor);
            }

            m_layerSignatures[i] = signature;
            m_layerBits[i] = bits;
            m_layerColors[i] = clothColorOf(catalog, clothId);
        }

        if (m_selectedCloth >= count)
            m_selectedCloth = UNSET_LAYER;
    }

    // 画布只报鼠标下的格，位图本身一个位都不改
    private void drawCanvas(BackpackConfigSO config, SerializedProperty list, int columns, int rows)
    {
        Event current = Event.current;

        if (current.type == EventType.MouseDown)
            m_strokeUndoRecorded = false;

        MaskPainterControl.PaintLayers(m_layerBits, m_layerColors, columns, rows, m_selectedCloth,
            out Vector2Int hitCell, out bool pressed);

        if (pressed)
            handleCanvasPress(config, list, hitCell);
    }

    // 按下只换选中不动位置，拖动才写锚点，点一下不该把布弹走
    private void handleCanvasPress(BackpackConfigSO config, SerializedProperty list, Vector2Int cell)
    {
        EventType type = Event.current.type;
        long bit = 1L << (cell.y * STRIDE + cell.x);

        if (type == EventType.MouseDown)
        {
            int grabbed = topLayerAt(bit);

            if (grabbed >= 0)
                m_selectedCloth = grabbed;

            return;
        }

        if (type != EventType.MouseDrag || m_selectedCloth < 0 || m_selectedCloth >= list.arraySize)
            return;

        SerializedProperty element = list.GetArrayElementAtIndex(m_selectedCloth);
        SerializedProperty columnProperty = element.FindPropertyRelative("CellColumn");
        SerializedProperty rowProperty = element.FindPropertyRelative("CellRow");

        if (columnProperty.intValue == cell.x && rowProperty.intValue == cell.y)
            return;

        if (!m_strokeUndoRecorded)
        {
            Undo.RecordObject(config, "拖动开局布料");
            m_strokeUndoRecorded = true;
        }

        columnProperty.intValue = cell.x;
        rowProperty.intValue = cell.y;
    }

    // 一条一块布：先摆本色小方片和名字，再按名字选布与异形，朝向与锚点留成小数字格
    private void drawEntryRows(BackpackConfigSO config, ClothCatalogSO catalog, SerializedProperty list,
        int columns, int rows)
    {
        string[] clothNames = clothNamesOf(catalog);

        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty element = list.GetArrayElementAtIndex(i);
            SerializedProperty clothProperty = element.FindPropertyRelative("ClothId");
            SerializedProperty shapeProperty = element.FindPropertyRelative("ShapeSlot");
            SerializedProperty orientationProperty = element.FindPropertyRelative("OrientationSlot");

            int clothIndex = catalog.IndexOfCloth(clothProperty.stringValue);
            ClothEntry cloth = clothIndex >= 0 ? catalog.ClothAt(clothIndex) : null;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            drawEntryTitle(cloth, shapeOf(cloth, shapeProperty.intValue), i, clothColorOf(catalog, clothProperty.stringValue));

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("布", GUILayout.Width(SLOT_LABEL_WIDTH));

            int pickedCloth = drawClothPopup(clothNames, clothIndex, clothProperty, shapeProperty, config);

            EditorGUILayout.LabelField("异形", GUILayout.Width(SLOT_LABEL_WIDTH));
            drawShapePopup(pickedCloth >= 0 ? catalog.ClothAt(pickedCloth) : cloth,
                shapeProperty.intValue, shapeProperty, config);

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            drawSlotField(config, orientationProperty, "朝向", int.MaxValue);
            drawSlotField(config, element.FindPropertyRelative("CellColumn"), "列", columns - 1);
            drawSlotField(config, element.FindPropertyRelative("CellRow"), "行", rows - 1);

            EditorGUILayout.LabelField("锚点是异形包围盒左上角", EditorStyles.miniLabel);

            if (GUILayout.Button("删", GUILayout.Width(DELETE_BUTTON_WIDTH)))
                m_pendingDelete = i;

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }
    }

    // 标题行左带头一块本色方片，玩家一眼对上画布上那层的颜色
    private void drawEntryTitle(ClothEntry cloth, GridShapeSO shape, int index, Color color)
    {
        EditorGUILayout.BeginHorizontal();

        Rect swatch = GUILayoutUtility.GetRect(SWATCH_SIZE, SWATCH_SIZE, GUILayout.Width(SWATCH_SIZE));
        EditorGUI.DrawRect(swatch, color);

        int cells = shape != null ? shape.CellCount : 0;
        string title = cloth == null
            ? $"第 {index + 1} 块 · 未匹配到布"
            : $"第 {index + 1} 块 · {cloth.Label} · {cells} 格";

        EditorGUILayout.LabelField(title, index == m_selectedCloth ? EditorStyles.boldLabel : EditorStyles.label);
        EditorGUILayout.EndHorizontal();
    }

    // 按异形下标取形状，越界给 null，标题只为显示占格数，不该因为配置写歪而崩
    private static GridShapeSO shapeOf(ClothEntry cloth, int shapeIndex)
    {
        if (cloth == null || shapeIndex < 0 || shapeIndex >= cloth.ShapeCount)
            return null;

        return cloth.Shapes[shapeIndex];
    }

    // 布料下拉，换布之后异形下标很可能越界，一律夹到新布的形状列表里
    private int drawClothPopup(string[] clothNames, int clothIndex, SerializedProperty clothProperty,
        SerializedProperty shapeProperty, BackpackConfigSO config)
    {
        int picked = EditorGUILayout.Popup(Mathf.Max(clothIndex, 0), clothNames, GUILayout.Width(POPUP_WIDTH));

        if (picked == clothIndex || picked < 0)
            return clothIndex;

        Undo.RecordObject(config, "更换开局布料");
        clothProperty.stringValue = m_clothIds[picked];
        shapeProperty.intValue = Mathf.Clamp(shapeProperty.intValue, 0, m_shapeCounts[picked] - 1);
        return picked;
    }

    private void drawShapePopup(ClothEntry cloth, int shapeIndex, SerializedProperty shapeProperty, BackpackConfigSO config)
    {
        if (cloth == null)
        {
            EditorGUILayout.Popup(0, m_unmatchedShapes, GUILayout.Width(POPUP_WIDTH));
            return;
        }

        int picked = EditorGUILayout.Popup(Mathf.Clamp(shapeIndex, 0, cloth.ShapeCount - 1),
            shapeNamesOf(cloth), GUILayout.Width(POPUP_WIDTH));

        if (picked >= 0 && picked < cloth.ShapeCount && picked != shapeIndex)
        {
            Undo.RecordObject(config, "更换开局异形");
            shapeProperty.intValue = picked;
        }
    }

    // 小数字格统一处理，越界只往下夹，负值当场归零
    private void drawSlotField(BackpackConfigSO config, SerializedProperty property, string label, int maxValue)
    {
        EditorGUILayout.LabelField(label, GUILayout.Width(SLOT_LABEL_WIDTH));

        int value = EditorGUILayout.IntField(property.intValue, GUILayout.Width(SLOT_FIELD_WIDTH));

        if (value == property.intValue)
            return;

        Undo.RecordObject(config, "调整开局布料摆放");
        property.intValue = Mathf.Clamp(Mathf.Max(0, value), 0, maxValue);
    }

    private void drawListToolbar(BackpackConfigSO config, ClothCatalogSO catalog, SerializedProperty list)
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("加一块布", GUILayout.Width(92f)))
        {
            Undo.RecordObject(config, "新增开局布料");
            list.InsertArrayElementAtIndex(list.arraySize);

            SerializedProperty added = list.GetArrayElementAtIndex(list.arraySize - 1);
            added.FindPropertyRelative("ClothId").stringValue = catalog.ClothAt(0).Id;
            added.FindPropertyRelative("ShapeSlot").intValue = 0;
            added.FindPropertyRelative("OrientationSlot").intValue = 0;
            added.FindPropertyRelative("CellColumn").intValue = 0;
            added.FindPropertyRelative("CellRow").intValue = 0;
        }

        if (list.arraySize > 0 && GUILayout.Button("删末块", GUILayout.Width(76f)))
        {
            Undo.RecordObject(config, "删除末块开局布料");
            list.DeleteArrayElementAtIndex(list.arraySize - 1);
        }

        EditorGUILayout.LabelField($"共 {list.arraySize} 块", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }

    // 并集预览：所有布叠在一起就是开局能用的格，重叠与越界当场标出来
    private void drawUnionPreview(SerializedProperty list, int columns, int rows)
    {
        long union = 0L;
        int overlapped = 0;
        int outside = 0;

        m_errorCells.Clear();

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                long bit = 1L << (row * STRIDE + column);
                int covered = 0;

                for (int i = 0; i < m_layerBits.Count; i++)
                {
                    if ((m_layerBits[i] & bit) != 0)
                        covered++;
                }

                if (covered == 0)
                    continue;

                if (covered > 1)
                {
                    m_errorCells.Add(new Vector2Int(column, row));
                    overlapped++;
                }

                union |= bit;
            }
        }

        for (int i = 0; i < m_layerBits.Count; i++)
        {
            if (!isLayerInside(m_layerBits[i], columns, rows))
                outside++;
        }

        EditorGUILayout.LabelField("开局可用格", EditorStyles.boldLabel);
        MaskPainterControl.PaintPreview(union, columns, rows, m_errorCells);
        EditorGUILayout.LabelField($"共 {MaskPainterControl.CountCells(union)} 格 · 重叠 {overlapped} 格 · 越界 {outside} 块",
            EditorStyles.miniLabel);

        if (outside > 0)
            EditorGUILayout.HelpBox("有布铺到了容器外面，超出部分不产生格子，把锚点往回拖即可。", MessageType.Warning);
        else if (overlapped > 0)
            EditorGUILayout.HelpBox("两块布压在同一格上不算错，但叠掉的格等于少一格。", MessageType.Info);
    }

    // 绘制全部走完再删，避免在半开的 Horizontal 里改列表
    private void applyPendingDelete(BackpackConfigSO config, SerializedProperty list)
    {
        if (m_pendingDelete < 0 || m_pendingDelete >= list.arraySize)
            return;

        Undo.RecordObject(config, "删除开局布料");
        list.DeleteArrayElementAtIndex(m_pendingDelete);

        if (m_selectedCloth == m_pendingDelete)
            m_selectedCloth = UNSET_LAYER;
        else if (m_selectedCloth > m_pendingDelete)
            m_selectedCloth--;

        m_pendingDelete = UNSET_LAYER;
        GUI.changed = true;
    }

    private int topLayerAt(long bit)
    {
        for (int i = m_layerBits.Count - 1; i >= 0; i--)
        {
            if ((m_layerBits[i] & bit) != 0)
                return i;
        }

        return UNSET_LAYER;
    }

    // 布在总表里查不到时给中性灰，画布照样画得出来，只是颜色对不上
    private static Color clothColorOf(ClothCatalogSO catalog, string clothId)
    {
        int index = catalog.IndexOfCloth(clothId);
        return index >= 0 ? catalog.ClothAt(index).Color : m_unmatchedColor;
    }

    private static string[] clothNamesOf(ClothCatalogSO catalog)
    {
        int count = catalog.ClothCount;

        m_clothIds = new string[count];
        m_shapeCounts = new int[count];

        string[] names = new string[count];

        for (int i = 0; i < count; i++)
        {
            ClothEntry cloth = catalog.ClothAt(i);
            names[i] = cloth.Label;
            m_clothIds[i] = cloth.Id;
            m_shapeCounts[i] = cloth.ShapeCount;
        }

        return names;
    }

    private static string[] shapeNamesOf(ClothEntry cloth)
    {
        IReadOnlyList<GridShapeSO> shapes = cloth.Shapes;
        string[] names = new string[shapes.Count];

        for (int i = 0; i < names.Length; i++)
            names[i] = shapeNameOf(cloth, i);

        return names;
    }

    // 形状名后面跟个占格数，同名的长条与方块一眼分得开
    private static string shapeNameOf(ClothEntry cloth, int shapeIndex)
    {
        if (cloth == null)
            return "空槽";

        IReadOnlyList<GridShapeSO> shapes = cloth.Shapes;

        if (shapeIndex < 0 || shapeIndex >= shapes.Count)
            return "空槽";

        GridShapeSO shape = shapes[shapeIndex];

        return shape != null ? $"{shape.Label}·{shape.CellCount}格" : "空槽";
    }

    // 整块布是否完全落在容器内，越界的位不产生格子，必须单独提醒
    private static bool isLayerInside(long bits, int columns, int rows)
    {
        if (bits == 0)
            return false;

        for (int bit = 0; bit < SCAN_BIT_LIMIT; bit++)
        {
            if ((bits & (1L << bit)) == 0)
                continue;

            if (bit % STRIDE >= columns || bit / STRIDE >= rows)
                return false;
        }

        return true;
    }
}
