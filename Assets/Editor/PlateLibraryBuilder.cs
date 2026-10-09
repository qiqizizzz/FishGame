/*
* ┌──────────────────────────────────┐
* │  描    述: 已停用的阵营与板块库生成器，口径被布料总表取代，仅作历史参考
* │  类    名: PlateLibraryBuilder.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 一张纸只堆一条能力线，正负都存带符号的值，聚合侧按封顶符号决定夹上界还是下界
* 旧口径里阵营是独立资产；布料总表改成一块布自带一个底色，不再需要阵营资产
* 板块 = 纸页 × 形状池，形状一律从形状库取，本生成器不造形状
* 空白纸页只占格不给加成，但形状池最全，它是腾格子的路
* 已存在的资产只重写参数，不重复创建，人工微调过的字段跑一次会被覆盖回表里的值
* 三批遗留资产已挪进 Assets/Data/Backpack/Deprecated，本生成器的输出路径跟着指过去，不会再在旧路径生第二份
*/

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Module.Backpack.Data;
using Utils.log;

public static class PlateLibraryBuilder
{
    // ==================== 常量与静态字段 ====================
    private const string FACTIONS_FOLDER = "Assets/Data/Backpack/Deprecated/Factions";
    private const string PAGES_FOLDER = "Assets/Data/Backpack/Deprecated/Pages";
    private const string PLATES_FOLDER = "Assets/Data/Backpack/Deprecated/Plates";
    private const string SHAPES_FOLDER = "Assets/Data/Backpack/Shapes";

    private const int BUFF_PLATE_PRICE = 40;
    private const int BLANK_PLATE_PRICE = 20;

    private static readonly string[] m_factionIds =
    {
        "deep", "swift", "load", "lure", "coin", "tough", "blank"
    };

    private static readonly string[] m_factionLabels =
    {
        "深潜", "迅捷", "负荷", "饵香", "财运", "抗造", "空白"
    };

    private static readonly Color32[] m_factionColors =
    {
        new Color32(0xA8, 0xC6, 0xE8, 0xFF),
        new Color32(0xA9, 0xD8, 0xC0, 0xFF),
        new Color32(0xF0, 0xC8, 0x9A, 0xFF),
        new Color32(0xD3, 0xBC, 0xE8, 0xFF),
        new Color32(0xF2, 0xDE, 0x9E, 0xFF),
        new Color32(0xBF, 0xCB, 0xD6, 0xFF),
        new Color32(0xED, 0xE0, 0xC8, 0xFF)
    };

    // 纸页键名，纸页 id 自动补 page_ 前缀，板块 id 用它拼形状名
    private static readonly string[] m_pageKeys =
    {
        "spool", "feather", "windmill", "rail", "swivel", "barthorn",
        "basket", "grain", "scalepowder", "fishscale", "knife", "blank"
    };

    private static readonly string[] m_pageLabels =
    {
        "线轴纸", "羽饰纸", "风轮纸", "滑轨纸", "转环纸", "钩芒纸",
        "篓纹纸", "饵粒纸", "鳞粉纸", "鲋鳞纸", "快刀纸", "空白纸页"
    };

    private static readonly string[] m_pageFactionIds =
    {
        "deep", "swift", "swift", "swift", "swift", "swift",
        "load", "lure", "coin", "tough", "tough", "blank"
    };

    private static readonly BackpackStatMod[] m_pageMods =
    {
        BackpackStatMod.LineLength, BackpackStatMod.ReelSpeed,
        BackpackStatMod.DescendSpeed, BackpackStatMod.LateralSpeed,
        BackpackStatMod.LateralDamping, BackpackStatMod.HookRadius,
        BackpackStatMod.MaxCarryCount, BackpackStatMod.LurePull,
        BackpackStatMod.CoinBonus, BackpackStatMod.DetachChance,
        BackpackStatMod.TangledDuration, BackpackStatMod.None
    };

    // 每占一格给多少，负向线为负
    private static readonly float[] m_pagePerCells =
    {
        1.5f, 0.12f, 0.15f, 0.15f, 0.4f, 0.005f,
        0.25f, 0.025f, 0.01f, -0.01f, -0.04f, 0f
    };

    // 这张纸在所有格子上最多叠到多少，负向线为负，空白页为 0
    private static readonly float[] m_pageCaps =
    {
        10f, 1.2f, 1.2f, 0.9f, 2.4f, 0.03f,
        2f, 0.15f, 0.1f, -0.1f, -0.4f, 0f
    };

    private static readonly string[][] m_pageShapePools =
    {
        new[] { "domino_2", "line_3", "corner_3" },
        new[] { "domino_2", "corner_3", "line_3", "block_4" },
        new[] { "domino_2", "corner_3", "block_4" },
        new[] { "domino_2", "line_3", "block_4" },
        new[] { "domino_2", "corner_3", "line_3" },
        new[] { "domino_2", "corner_3" },
        new[] { "line_3", "block_4", "slab_6" },
        new[] { "domino_2", "corner_3", "block_4" },
        new[] { "domino_2", "corner_3", "line_3", "block_4" },
        new[] { "domino_2", "corner_3", "block_4" },
        new[] { "domino_2", "line_3", "block_4" },
        new[] { "domino_2", "corner_3", "line_3", "block_4", "slab_6" }
    };

    // ==================== Menu ====================
    // 阵营与板块库已被布料总表 ClothCatalog 取代，这里只留作历史口径的参考
    // 菜单暂时停用，免得误跑一遍把旧的 58 个资产又生回来；需要回看时把下面这行的注释去掉即可
    // [MenuItem("Tools/钓鱼游戏/生成阵营与板块库")]
    public static void Build()
    {
        ensureFolder(FACTIONS_FOLDER);
        ensureFolder(PAGES_FOLDER);
        ensureFolder(PLATES_FOLDER);

        int pageCount = 0;

        for (int i = 0; i < m_pageKeys.Length; i++)
        {
            if (buildPage(i))
                pageCount++;
        }

        int plateCount = buildPlates();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        QLog.Info($"板块库已就绪：阵营 {m_factionIds.Length} 个，纸页 {pageCount} 张，板块 {plateCount} 块");
    }

    // ==================== Public Function ====================
    /// <summary>
    /// 取阵营资产，供其它生成器复用，取不到报错并返回 null
    /// </summary>
    /// <param name="factionId">阵营标识</param>
    public static BuffFactionSO LoadFaction(string factionId)
    {
        string[] guids = AssetDatabase.FindAssets($"t:BuffFactionSO {factionId}", new[] { FACTIONS_FOLDER });

        for (int i = 0; i < guids.Length; i++)
        {
            BuffFactionSO faction = AssetDatabase.LoadAssetAtPath<BuffFactionSO>(AssetDatabase.GUIDToAssetPath(guids[i]));

            if (faction != null && faction.Id == factionId)
                return faction;
        }

        QLog.Error($"阵营资产未找到：{factionId}，请先跑「生成阵营与板块库」");
        return null;
    }

    /// <summary>
    /// 载入板块库里的全部板块，供商店奖池灌列表用
    /// </summary>
    public static List<BackpackPlateSO> LoadAllPlates()
    {
        var result = new List<BackpackPlateSO>();
        string[] guids = AssetDatabase.FindAssets("t:BackpackPlateSO", new[] { PLATES_FOLDER });

        for (int i = 0; i < guids.Length; i++)
        {
            BackpackPlateSO plate = AssetDatabase.LoadAssetAtPath<BackpackPlateSO>(AssetDatabase.GUIDToAssetPath(guids[i]));

            if (plate != null)
                result.Add(plate);
        }

        result.Sort((a, b) => string.Compare(a.Id, b.Id, System.StringComparison.Ordinal));
        return result;
    }

    // ==================== Private Function ====================
    private static bool buildPage(int index)
    {
        buildFaction(index);

        string pageId = "page_" + m_pageKeys[index];
        string path = $"{PAGES_FOLDER}/{pageId}.asset";
        BuffPageSO page = AssetDatabase.LoadAssetAtPath<BuffPageSO>(path);

        if (page == null)
        {
            page = ScriptableObject.CreateInstance<BuffPageSO>();
            AssetDatabase.CreateAsset(page, path);
        }

        SerializedObject serialized = new SerializedObject(page);
        serialized.FindProperty("PageId").stringValue = pageId;
        serialized.FindProperty("PageLabel").stringValue = m_pageLabels[index];
        serialized.FindProperty("FactionAsset").objectReferenceValue = LoadFaction(m_pageFactionIds[index]);
        serialized.FindProperty("ModType").intValue = (int)m_pageMods[index];
        serialized.FindProperty("PerCell").floatValue = m_pagePerCells[index];
        serialized.FindProperty("BonusCap").floatValue = m_pageCaps[index];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(page);

        return true;
    }

    // 阵营跟着纸页一起建，钩芒纸和线轴纸同属一个阵营也不会重复建
    private static void buildFaction(int pageIndex)
    {
        string factionId = m_pageFactionIds[pageIndex];
        string path = $"{FACTIONS_FOLDER}/{factionId}.asset";

        if (AssetDatabase.LoadAssetAtPath<BuffFactionSO>(path) != null)
            return;

        BuffFactionSO faction = ScriptableObject.CreateInstance<BuffFactionSO>();
        AssetDatabase.CreateAsset(faction, path);

        SerializedObject serialized = new SerializedObject(faction);
        serialized.FindProperty("FactionId").stringValue = factionId;
        serialized.FindProperty("FactionLabel").stringValue = m_factionLabels[factionIndex(factionId)];
        serialized.FindProperty("FactionPageColor").colorValue = m_factionColors[factionIndex(factionId)];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(faction);
    }

    private static int factionIndex(string factionId)
    {
        for (int i = 0; i < m_factionIds.Length; i++)
        {
            if (m_factionIds[i] == factionId)
                return i;
        }

        QLog.Error($"阵营不在清单里：{factionId}");
        return 0;
    }

    // 每张纸按自己的形状池铺一批板块，同一条线上占格多的自然比占格少的贵
    private static int buildPlates()
    {
        int count = 0;

        for (int pageIndex = 0; pageIndex < m_pageKeys.Length; pageIndex++)
        {
            BuffPageSO page = AssetDatabase.LoadAssetAtPath<BuffPageSO>($"{PAGES_FOLDER}/page_{m_pageKeys[pageIndex]}.asset");

            if (page == null)
            {
                QLog.Error($"纸页资产缺失，跳过它的板块：page_{m_pageKeys[pageIndex]}");
                continue;
            }

            string[] shapeIds = m_pageShapePools[pageIndex];

            for (int shapeIndex = 0; shapeIndex < shapeIds.Length; shapeIndex++)
            {
                GridShapeSO shape = loadShape(shapeIds[shapeIndex]);

                if (shape == null)
                {
                    QLog.Error($"形状模板未找到：{shapeIds[shapeIndex]}，纸页 {page.Label} 缺这一款板块");
                    continue;
                }

                string plateId = $"plate_{m_pageKeys[pageIndex]}_{shapeIds[shapeIndex]}";
                string path = $"{PLATES_FOLDER}/{plateId}.asset";
                BackpackPlateSO plate = AssetDatabase.LoadAssetAtPath<BackpackPlateSO>(path);

                if (plate == null)
                {
                    plate = ScriptableObject.CreateInstance<BackpackPlateSO>();
                    AssetDatabase.CreateAsset(plate, path);
                }

                SerializedObject serialized = new SerializedObject(plate);
                serialized.FindProperty("PlateId").stringValue = plateId;
                serialized.FindProperty("PlateLabel").stringValue = $"{page.Label}·{shape.CellCount}格";
                serialized.FindProperty("PageAsset").objectReferenceValue = page;
                serialized.FindProperty("ShapeAsset").objectReferenceValue = shape;
                serialized.FindProperty("Price").intValue =
                    page.Mod == BackpackStatMod.None ? BLANK_PLATE_PRICE : BUFF_PLATE_PRICE;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(plate);

                count++;
            }
        }

        return count;
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
