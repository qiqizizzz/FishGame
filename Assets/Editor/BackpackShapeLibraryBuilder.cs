/*
* ┌──────────────────────────────────┐
* │  描    述: 形状模板库生成器，一键建出五个模板与背包容器配置资产
* │  类    名: BackpackShapeLibraryBuilder.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 位图用格子坐标表写死，读得懂也改得动，不让人去猜十六进制
* 只建开局那一级解锁掩码，后面几级留给人工在 Inspector 里点，不替策划决定成长曲线
* 已存在的资产只覆盖位图与格数校验，不重复创建，菜单可以反复点
*/

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Module.Backpack.Data;
using Module.Backpack.Model;
using Utils.log;

public static class BackpackShapeLibraryBuilder
{
    // ==================== 常量与静态字段 ====================
    private const string SHAPES_FOLDER = "Assets/Data/Backpack/Shapes";
    private const string CONFIG_PATH = "Assets/Data/Config/BackpackConfig.asset";

    private const int CONTAINER_COLUMNS = 6;
    private const int CONTAINER_ROWS = 5;
    private const int STASH_CAPACITY = 7;

    // 多米诺 2 格，竖着的一条
    private static readonly Vector2Int[] m_dominoCells =
    {
        new Vector2Int(0, 0), new Vector2Int(0, 1)
    };

    // L 形 3 格
    private static readonly Vector2Int[] m_cornerCells =
    {
        new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 1)
    };

    // I 形 3 格
    private static readonly Vector2Int[] m_lineCells =
    {
        new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2)
    };

    // 方块 4 格，旋转后与自身重合，界面不该给它旋转按钮
    private static readonly Vector2Int[] m_blockCells =
    {
        new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1)
    };

    // 板条 6 格，2×3，封顶尺寸
    private static readonly Vector2Int[] m_slabCells =
    {
        new Vector2Int(0, 0), new Vector2Int(1, 0),
        new Vector2Int(0, 1), new Vector2Int(1, 1),
        new Vector2Int(0, 2), new Vector2Int(1, 2)
    };

    // ==================== Menu ====================
    [MenuItem("Tools/钓鱼游戏/生成默认形状库与背包配置")]
    public static void Build()
    {
        ensureFolder(SHAPES_FOLDER);

        GridShapeSO domino = buildShape("domino_2", "多米诺", m_dominoCells);
        GridShapeSO corner = buildShape("corner_3", "L 形", m_cornerCells);
        GridShapeSO line = buildShape("line_3", "I 形", m_lineCells);
        GridShapeSO block = buildShape("block_4", "方块", m_blockCells);
        GridShapeSO slab = buildShape("slab_6", "2×3 板", m_slabCells);

        BackpackConfigSO config = buildConfig(domino, corner, line, block, slab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        QLog.Info($"形状库与配置已就绪：{SHAPES_FOLDER} 五个模板，配置 {CONFIG_PATH}，开局格由配置里的开局布料列表铺出，容器 {CONTAINER_COLUMNS}×{CONTAINER_ROWS}");
    }

    // ==================== Private Function ====================
    private static GridShapeSO buildShape(string shapeId, string label, Vector2Int[] cells)
    {
        string path = $"{SHAPES_FOLDER}/{shapeId}.asset";
        GridShapeSO shape = AssetDatabase.LoadAssetAtPath<GridShapeSO>(path);
        long bits = toBits(cells);

        if (!ShapeMask.TryCreate(bits, out ShapeMask mask))
        {
            QLog.Error($"形状 {shapeId} 位图为空，跳过创建");
            return null;
        }

        if (!mask.IsConnected())
            QLog.Error($"形状 {shapeId} 不连通，仍然写入，交由 OnValidate 报错提醒");

        if (shape == null)
        {
            shape = ScriptableObject.CreateInstance<GridShapeSO>();
            AssetDatabase.CreateAsset(shape, path);
        }

        SerializedObject serialized = new SerializedObject(shape);
        serialized.FindProperty("ShapeId").stringValue = shapeId;
        serialized.FindProperty("ShapeLabel").stringValue = label;
        serialized.FindProperty("MaskBits").longValue = bits;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(shape);
        return shape;
    }

    private static BackpackConfigSO buildConfig(params GridShapeSO[] shapes)
    {
        BackpackConfigSO config = AssetDatabase.LoadAssetAtPath<BackpackConfigSO>(CONFIG_PATH);
        bool created = config == null;

        if (created)
            config = ScriptableObject.CreateInstance<BackpackConfigSO>();

        SerializedObject serialized = new SerializedObject(config);
        serialized.FindProperty("GridColumns").intValue = CONTAINER_COLUMNS;
        serialized.FindProperty("GridRows").intValue = CONTAINER_ROWS;
        serialized.FindProperty("StashCapacityLimit").intValue = STASH_CAPACITY;

        SerializedProperty prices = serialized.FindProperty("TierPriceList");
        prices.arraySize = BackpackConfigSO.TIER_COUNT;
        int[] tiers = { 80, 200, 560 };

        for (int i = 0; i < prices.arraySize; i++)
            prices.GetArrayElementAtIndex(i).intValue = tiers[i];

        serialized.ApplyModifiedPropertiesWithoutUndo();

        if (created)
            AssetDatabase.CreateAsset(config, CONFIG_PATH);

        EditorUtility.SetDirty(config);

        int missing = 0;

        for (int i = 0; i < shapes.Length; i++)
        {
            if (shapes[i] == null)
                missing++;
        }

        if (missing > 0)
            QLog.Error($"有 {missing} 个形状模板创建失败，装备档位引用前请先修好它们");

        return config;
    }

    private static long toBits(Vector2Int[] cells)
    {
        long bits = 0;

        for (int i = 0; i < cells.Length; i++)
            bits |= 1L << (cells[i].y * ShapeMask.ROW_STRIDE + cells[i].x);

        return bits;
    }

    private static void ensureFolder(string folder)
    {
        string[] parts = folder.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";

            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }
}
