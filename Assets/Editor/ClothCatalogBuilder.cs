/*
* ┌──────────────────────────────────┐
* │  描    述: 布料总表生成器，把 4 档稀有度与 15 块布一次性灌进唯一的一张 ClothCatalogSO
* │  类    名: ClothCatalogBuilder.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 数据全写死在下面这张表里，重跑一次即覆盖回表中的值，日常手感微调直接在 Inspector 上改
* 异形只从形状模板库捞现成的资产，库里没有的形状会当场报错并跳过那一格
* 本类是 Editor 目录下的工具，不进运行时包体
*/

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Module.Backpack.Data;
using Utils.log;

public static class ClothCatalogBuilder
{
    // ==================== 常量与静态字段 ====================
    private const string CATALOG_FOLDER = "Assets/Data/Backpack";
    private const string CATALOG_PATH = CATALOG_FOLDER + "/ClothCatalog.asset";
    private const string SHAPES_FOLDER = "Assets/Data/Backpack/Shapes";

    private static readonly string[] m_tierIds = { "common", "uncommon", "rare", "epic" };
    private static readonly string[] m_tierLabels = { "常见", "少见", "稀有", "珍罕" };
    private static readonly float[] m_tierDrawWeights = { 62f, 26f, 10f, 2f };
    private static readonly float[] m_tierPriceScales = { 1f, 1.6f, 2.4f, 4f };

    // 布料清单：一条布的名字、颜色、稀有度、单格价、几条 BUFF、能剪成哪几种异形
    private static readonly ClothSpec[] m_cloths =
    {
        new ClothSpec("cloth_blank", "空白布", 0, 0xED, 0xE0, 0xC8, 8,
            new BuffSpec[0],
            new[] { "block_4", "corner_3", "domino_2", "line_3", "slab_6" }),

        new ClothSpec("cloth_dive", "深潜布", 0, 0xA8, 0xC6, 0xE8, 14,
            new[] { new BuffSpec(BackpackStatMod.LineLength, 1.5f, 10f) },
            new[] { "corner_3", "domino_2", "line_3" }),

        new ClothSpec("cloth_sink", "快潜布", 0, 0xA9, 0xD8, 0xC0, 14,
            new[] { new BuffSpec(BackpackStatMod.DescendSpeed, 0.15f, 1.2f) },
            new[] { "block_4", "corner_3", "domino_2" }),

        new ClothSpec("cloth_glide", "迅捷布", 0, 0xA9, 0xD8, 0xC0, 14,
            new[] { new BuffSpec(BackpackStatMod.LateralSpeed, 0.15f, 0.9f) },
            new[] { "block_4", "domino_2", "line_3" }),

        new ClothSpec("cloth_reel", "轻收布", 0, 0xA9, 0xD8, 0xC0, 14,
            new[] { new BuffSpec(BackpackStatMod.ReelSpeed, 0.12f, 1.2f) },
            new[] { "block_4", "corner_3", "domino_2", "line_3" }),

        new ClothSpec("cloth_steady", "稳手布", 1, 0xA9, 0xD8, 0xC0, 14,
            new[] { new BuffSpec(BackpackStatMod.LateralDamping, 0.4f, 2.4f) },
            new[] { "corner_3", "domino_2", "line_3" }),

        new ClothSpec("cloth_cargo", "重载布", 1, 0xF0, 0xC8, 0x9A, 14,
            new[] { new BuffSpec(BackpackStatMod.MaxCarryCount, 0.25f, 2f) },
            new[] { "block_4", "line_3", "slab_6" }),

        new ClothSpec("cloth_lure", "诱鱼布", 1, 0xD3, 0xBC, 0xE8, 14,
            new[] { new BuffSpec(BackpackStatMod.LurePull, 0.025f, 0.15f) },
            new[] { "block_4", "corner_3", "domino_2" }),

        new ClothSpec("cloth_gape", "广钩布", 2, 0xA9, 0xD8, 0xC0, 14,
            new[] { new BuffSpec(BackpackStatMod.HookRadius, 0.005f, 0.03f) },
            new[] { "corner_3", "domino_2" }),

        new ClothSpec("cloth_coin", "财运布", 2, 0xF2, 0xDE, 0x9E, 14,
            new[] { new BuffSpec(BackpackStatMod.CoinBonus, 0.01f, 0.1f) },
            new[] { "block_4", "corner_3", "domino_2", "line_3" }),

        new ClothSpec("cloth_guard", "防脱布", 2, 0xBF, 0xCB, 0xD6, 14,
            new[] { new BuffSpec(BackpackStatMod.DetachChance, -0.01f, -0.1f) },
            new[] { "block_4", "corner_3", "domino_2" }),

        new ClothSpec("cloth_cut", "快解布", 2, 0xBF, 0xCB, 0xD6, 14,
            new[] { new BuffSpec(BackpackStatMod.TangledDuration, -0.04f, -0.4f) },
            new[] { "block_4", "domino_2", "line_3" }),

        new ClothSpec("cloth_current", "顺流布", 3, 0xA9, 0xD8, 0xC0, 16,
            new[]
            {
                new BuffSpec(BackpackStatMod.DescendSpeed, 0.12f, 0.6f),
                new BuffSpec(BackpackStatMod.ReelSpeed, 0.12f, 0.6f),
                new BuffSpec(BackpackStatMod.LateralSpeed, 0.10f, 0.6f)
            },
            new[] { "corner_3", "block_4" }),

        new ClothSpec("cloth_harvest", "满篓布", 3, 0xF0, 0xC8, 0x9A, 16,
            new[]
            {
                new BuffSpec(BackpackStatMod.MaxCarryCount, 0.20f, 1.6f),
                new BuffSpec(BackpackStatMod.CoinBonus, 0.012f, 0.12f),
                new BuffSpec(BackpackStatMod.LurePull, 0.02f, 0.12f)
            },
            new[] { "block_4", "corner_3" }),

        new ClothSpec("cloth_mesh", "韧网布", 3, 0xBF, 0xCB, 0xD6, 16,
            new[]
            {
                new BuffSpec(BackpackStatMod.DetachChance, -0.012f, -0.12f),
                new BuffSpec(BackpackStatMod.TangledDuration, -0.03f, -0.3f),
                new BuffSpec(BackpackStatMod.HookRadius, 0.004f, 0.024f)
            },
            new[] { "slab_6", "line_3" })
    };

    // ==================== Menu ====================
    [MenuItem("Tools/钓鱼游戏/生成布料总表")]
    public static void Build()
    {
        ensureFolder(CATALOG_FOLDER);

        ClothCatalogSO catalog = AssetDatabase.LoadAssetAtPath<ClothCatalogSO>(CATALOG_PATH);

        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<ClothCatalogSO>();
            AssetDatabase.CreateAsset(catalog, CATALOG_PATH);
        }

        SerializedObject serialized = new SerializedObject(catalog);

        writeTiers(serialized.FindProperty("RarityTierList"));
        int written = writeCloths(serialized.FindProperty("ClothList"));

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        catalog.InvalidatePieceCache();

        QLog.Info($"布料总表已就绪：{CATALOG_PATH}，稀有度 {m_tierIds.Length} 档，布料 {written} 块");
    }

    // ==================== Public Function ====================
    /// <summary>
    /// 读回总表资产，没建过就当场建一次，商店配置生成器用它挂引用
    /// </summary>
    public static ClothCatalogSO LoadCatalog()
    {
        ClothCatalogSO catalog = AssetDatabase.LoadAssetAtPath<ClothCatalogSO>(CATALOG_PATH);

        if (catalog != null)
            return catalog;

        Build();
        return AssetDatabase.LoadAssetAtPath<ClothCatalogSO>(CATALOG_PATH);
    }

    // ==================== Private Function ====================
    private static void writeTiers(SerializedProperty tiers)
    {
        tiers.arraySize = m_tierIds.Length;

        for (int i = 0; i < m_tierIds.Length; i++)
        {
            SerializedProperty tier = tiers.GetArrayElementAtIndex(i);
            tier.FindPropertyRelative("TierId").stringValue = m_tierIds[i];
            tier.FindPropertyRelative("TierLabel").stringValue = m_tierLabels[i];
            tier.FindPropertyRelative("DrawWeight").floatValue = m_tierDrawWeights[i];
            tier.FindPropertyRelative("PriceScale").floatValue = m_tierPriceScales[i];
        }
    }

    private static int writeCloths(SerializedProperty cloths)
    {
        cloths.arraySize = m_cloths.Length;

        for (int i = 0; i < m_cloths.Length; i++)
        {
            ClothSpec spec = m_cloths[i];
            SerializedProperty cloth = cloths.GetArrayElementAtIndex(i);

            cloth.FindPropertyRelative("ClothId").stringValue = spec.Id;
            cloth.FindPropertyRelative("ClothLabel").stringValue = spec.Label;
            cloth.FindPropertyRelative("ClothColor").colorValue = spec.Color;
            cloth.FindPropertyRelative("RarityTierIndex").intValue = spec.RarityIndex;
            cloth.FindPropertyRelative("CellBasePrice").intValue = spec.CellBasePrice;

            writeBuffs(cloth.FindPropertyRelative("BuffList"), spec.Buffs);
            writeShapes(cloth.FindPropertyRelative("ShapeList"), spec.ShapeIds, spec.Label);
        }

        return m_cloths.Length;
    }

    private static void writeBuffs(SerializedProperty buffList, BuffSpec[] buffs)
    {
        buffList.arraySize = buffs.Length;

        for (int i = 0; i < buffs.Length; i++)
        {
            SerializedProperty buff = buffList.GetArrayElementAtIndex(i);
            buff.FindPropertyRelative("ModType").intValue = (int) buffs[i].Mod;
            buff.FindPropertyRelative("PerCell").floatValue = buffs[i].PerCell;
            buff.FindPropertyRelative("Cap").floatValue = buffs[i].Cap;
        }
    }

    // 形状按资产路径直取，库里少一个就报哪块布缺哪一款，不静默少一格
    private static void writeShapes(SerializedProperty shapeList, string[] shapeIds, string clothLabel)
    {
        var shapes = new List<GridShapeSO>(shapeIds.Length);

        for (int i = 0; i < shapeIds.Length; i++)
        {
            GridShapeSO shape = AssetDatabase.LoadAssetAtPath<GridShapeSO>($"{SHAPES_FOLDER}/{shapeIds[i]}.asset");

            if (shape == null)
            {
                QLog.Error($"形状模板未找到：{shapeIds[i]}，布料「{clothLabel}」少这一款异形");
                continue;
            }

            shapes.Add(shape);
        }

        shapeList.arraySize = shapes.Count;

        for (int i = 0; i < shapes.Count; i++)
            shapeList.GetArrayElementAtIndex(i).objectReferenceValue = shapes[i];
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

    // ==================== 类型 ====================
    // 一块布的写法模板，只在这份清单里存在，不进任何运行时对象
    private sealed class ClothSpec
    {
        public readonly string Id;
        public readonly string Label;
        public readonly int RarityIndex;
        public readonly Color32 Color;
        public readonly int CellBasePrice;
        public readonly BuffSpec[] Buffs;
        public readonly string[] ShapeIds;

        public ClothSpec(string id, string label, int rarityIndex,
                         int r, int g, int b, int cellBasePrice,
                         BuffSpec[] buffs, string[] shapeIds)
        {
            Id = id;
            Label = label;
            RarityIndex = rarityIndex;
            Color = new Color32((byte) r, (byte) g, (byte) b, 0xFF);
            CellBasePrice = cellBasePrice;
            Buffs = buffs;
            ShapeIds = shapeIds;
        }
    }

    // 布上的一条 BUFF，能力线加每格额度加封顶
    private sealed class BuffSpec
    {
        public readonly BackpackStatMod Mod;
        public readonly float PerCell;
        public readonly float Cap;

        public BuffSpec(BackpackStatMod mod, float perCell, float cap)
        {
            Mod = mod;
            PerCell = perCell;
            Cap = cap;
        }
    }
}
