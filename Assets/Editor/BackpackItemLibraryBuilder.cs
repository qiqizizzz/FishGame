/*
* ┌──────────────────────────────────┐
* │  描    述: 背包装备库生成器，按形状模板给四件装备铺好档位表
* │  类    名: BackpackItemLibraryBuilder.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 形状一律按 id 去形状库取，装备不自己造形状，模板库解耦才有意义
* 每档一个模板，一级多米诺起步，升档换更大的模板，装备变强也变占地
* 已存在的资产只重写档位表，不重复创建；开局装备列表只在为空时写一次
*/

using UnityEditor;
using UnityEngine;
using Module.Backpack.Data;
using Utils.log;

public static class BackpackItemLibraryBuilder
{
    // ==================== 常量与静态字段 ====================
    private const string ITEMS_FOLDER = "Assets/Data/Backpack/Items";
    private const string SHAPES_FOLDER = "Assets/Data/Backpack/Shapes";
    private const string CONFIG_PATH = "Assets/Data/Config/BackpackConfig.asset";

    private static readonly string[] m_itemIds =
    {
        "rod_training", "line_extension", "reel_handle", "lure_bait"
    };

    private static readonly string[] m_itemLabels =
    {
        "教学鱼竿", "延长鱼线", "卷线手柄", "强力鱼饵"
    };

    private static readonly BackpackStatMod[] m_itemMods =
    {
        BackpackStatMod.MaxCarryCount, BackpackStatMod.LineLength,
        BackpackStatMod.ReelSpeed, BackpackStatMod.LurePull
    };

    // 索引 0 是一档，档位越高模板越大，六格板是开局锁不住的那一档
    private static readonly string[][] m_tierShapeIds =
    {
        new[] { "domino_2", "line_3", "slab_6" },
        new[] { "domino_2", "corner_3", "slab_6" },
        new[] { "domino_2", "corner_3", "block_4" },
        new[] { "domino_2", "line_3", "block_4" }
    };

    // 与形状表等长，该档对本件能力线的修改量，数值先给整数档好读好改
    private static readonly float[][] m_tierValues =
    {
        new[] { 1f, 2f, 4f },
        new[] { 6f, 12f, 24f },
        new[] { 0.8f, 1.6f, 3.2f },
        new[] { 0.2f, 0.4f, 0.8f }
    };

    // ==================== Menu ====================
    [MenuItem("Tools/钓鱼游戏/生成默认装备库")]
    public static void Build()
    {
        ensureFolder(ITEMS_FOLDER);

        BackpackItemSO[] items = new BackpackItemSO[m_itemIds.Length];

        for (int i = 0; i < m_itemIds.Length; i++)
            items[i] = buildItem(i);

        writeStartingItem(items[0]);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        QLog.Info($"装备库已就绪：{ITEMS_FOLDER} 共 {items.Length} 件，开局装备 {m_itemLabels[0]}");
    }

    // ==================== Private Function ====================
    private static BackpackItemSO buildItem(int index)
    {
        string path = $"{ITEMS_FOLDER}/{m_itemIds[index]}.asset";
        BackpackItemSO item = AssetDatabase.LoadAssetAtPath<BackpackItemSO>(path);

        if (item == null)
        {
            item = ScriptableObject.CreateInstance<BackpackItemSO>();
            AssetDatabase.CreateAsset(item, path);
        }

        SerializedObject serialized = new SerializedObject(item);
        serialized.FindProperty("ItemId").stringValue = m_itemIds[index];
        serialized.FindProperty("ItemLabel").stringValue = m_itemLabels[index];
        serialized.FindProperty("ModType").intValue = (int)m_itemMods[index];

        string[] shapeIds = m_tierShapeIds[index];
        float[] values = m_tierValues[index];

        SerializedProperty shapes = serialized.FindProperty("TierShapes");
        shapes.arraySize = shapeIds.Length;

        for (int tier = 0; tier < shapeIds.Length; tier++)
        {
            GridShapeSO shape = loadShape(shapeIds[tier]);

            if (shape == null)
                QLog.Error($"形状模板未找到：{shapeIds[tier]}，{m_itemIds[index]} 第 {tier + 1} 档会空着");

            shapes.GetArrayElementAtIndex(tier).objectReferenceValue = shape;
        }

        SerializedProperty propertyValues = serialized.FindProperty("TierValues");
        propertyValues.arraySize = values.Length;

        for (int tier = 0; tier < values.Length; tier++)
            propertyValues.GetArrayElementAtIndex(tier).floatValue = values[tier];

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);

        return item;
    }

    // 只在开局列表还空着的时候写一次，人工改过的开局装备不覆盖
    private static void writeStartingItem(BackpackItemSO rod)
    {
        BackpackConfigSO config = AssetDatabase.LoadAssetAtPath<BackpackConfigSO>(CONFIG_PATH);

        if (config == null)
        {
            QLog.Error($"背包配置未找到，开局装备写不进去：{CONFIG_PATH}，先跑「生成默认形状库与背包配置」");
            return;
        }

        SerializedObject serialized = new SerializedObject(config);
        SerializedProperty starting = serialized.FindProperty("StartingItemList");

        if (starting.arraySize > 0)
        {
            QLog.Info($"开局装备已有 {starting.arraySize} 项，保留人工配置不覆盖");
            return;
        }

        starting.arraySize = 1;
        starting.GetArrayElementAtIndex(0).objectReferenceValue = rod;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(config);
    }

    private static GridShapeSO loadShape(string shapeId)
    {
        string[] guids = AssetDatabase.FindAssets($"t:GridShapeSO {shapeId}", new[] { SHAPES_FOLDER });

        for (int i = 0; i < guids.Length; i++)
        {
            GridShapeSO shape = AssetDatabase.LoadAssetAtPath<GridShapeSO>(AssetDatabase.GUIDToAssetPath(guids[i]));

            if (shape != null && shape.Id == shapeId)
                return shape;
        }

        return null;
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
