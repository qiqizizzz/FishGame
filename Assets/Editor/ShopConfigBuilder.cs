/*
* ┌──────────────────────────────────┐
* │  描    述: 商店配置生成器，灌好装备奖池并挂上布料总表，写好抽取与涨价参数
* │  类    名: ShopConfigBuilder.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 奖池不是手抄清单，而是回库里捞，库里新增一件就自动多一件，省得两边对不上
* 板块不再摆清单，只挂一张布料总表，抽什么布、抽中多重、卖多贵全在那张表里配
* 参数写死成一张表，想改手感就在 Inspector 上微调，再跑一次会被覆盖回表里的值
* 顺手把配置挂进钓鱼场景资产，只认那条场景的路径，不碰用户当前打开的场景
*/

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Module.Backpack.Data;
using Module.Fish;
using Module.Shop.Data;
using Utils.log;

public static class ShopConfigBuilder
{
    // ==================== 常量与静态字段 ====================
    private const string CONFIG_FOLDER = "Assets/Data/Config";
    private const string CONFIG_PATH = CONFIG_FOLDER + "/ShopConfig.asset";
    private const string ITEMS_FOLDER = "Assets/Data/Backpack/Items";
    private const string FISHING_SCENE_PATH = "Assets/Scenes/Fishing.unity";

    private const int GOODS_SLOT_COUNT = 4;
    private const float ITEM_CATEGORY_WEIGHT = 55f;
    private const float CLOTH_CATEGORY_WEIGHT = 35f;
    private const float BLANK_CLOTH_CATEGORY_WEIGHT = 10f;
    private const float PRICE_JITTER_RATIO = 0.1f;
    private const int REFRESH_BASE_COST = 10;
    private const float REFRESH_COST_GROWTH = 1.6f;
    private const int REFRESH_COST_CEILING = 60;

    private static readonly float[] m_tierWeights = { 70f, 25f, 5f };

    // ==================== Menu ====================
    [MenuItem("Tools/钓鱼游戏/生成商店配置")]
    public static void Build()
    {
        ClothCatalogBuilder.Build();
        BackpackItemLibraryBuilder.Build();

        ensureFolder(CONFIG_FOLDER);

        ShopConfigSO config = AssetDatabase.LoadAssetAtPath<ShopConfigSO>(CONFIG_PATH);

        if (config == null)
        {
            config = ScriptableObject.CreateInstance<ShopConfigSO>();
            AssetDatabase.CreateAsset(config, CONFIG_PATH);
        }

        List<BackpackItemSO> items = loadAllItems();
        ClothCatalogSO catalog = ClothCatalogBuilder.LoadCatalog();

        SerializedObject serialized = new SerializedObject(config);
        serialized.FindProperty("GoodsSlotCount").intValue = GOODS_SLOT_COUNT;
        serialized.FindProperty("ItemCategoryWeight").floatValue = ITEM_CATEGORY_WEIGHT;
        serialized.FindProperty("ClothCategoryWeight").floatValue = CLOTH_CATEGORY_WEIGHT;
        serialized.FindProperty("BlankClothCategoryWeight").floatValue = BLANK_CLOTH_CATEGORY_WEIGHT;
        serialized.FindProperty("PriceJitterRatio").floatValue = PRICE_JITTER_RATIO;
        serialized.FindProperty("RefreshBaseCost").intValue = REFRESH_BASE_COST;
        serialized.FindProperty("RefreshCostGrowth").floatValue = REFRESH_COST_GROWTH;
        serialized.FindProperty("RefreshCostCeiling").intValue = REFRESH_COST_CEILING;

        SerializedProperty tierWeights = serialized.FindProperty("TierWeightList");
        tierWeights.arraySize = m_tierWeights.Length;

        for (int i = 0; i < m_tierWeights.Length; i++)
            tierWeights.GetArrayElementAtIndex(i).floatValue = m_tierWeights[i];

        writePool(serialized.FindProperty("ItemPool"), items);
        serialized.FindProperty("ClothCatalog").objectReferenceValue = catalog;

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();

        QLog.Info($"商店配置已就绪：{CONFIG_PATH}，装备 {items.Count} 件，布料总表 {catalog.ClothCount} 块");

        wireIntoScene(config);
    }

    [MenuItem("Tools/钓鱼游戏/把商店配置挂进场景")]
    public static void WireOnly()
    {
        ShopConfigSO config = AssetDatabase.LoadAssetAtPath<ShopConfigSO>(CONFIG_PATH);

        if (config == null)
        {
            QLog.Error($"商店配置还没生成，先跑「生成商店配置」：{CONFIG_PATH}");
            return;
        }

        wireIntoScene(config);
    }

    // ==================== Private Function ====================
    private static void writePool<T>(SerializedProperty pool, List<T> assets) where T : UnityEngine.Object
    {
        pool.arraySize = assets.Count;

        for (int i = 0; i < assets.Count; i++)
            pool.GetArrayElementAtIndex(i).objectReferenceValue = assets[i];
    }

    private static List<BackpackItemSO> loadAllItems()
    {
        var result = new List<BackpackItemSO>();
        string[] guids = AssetDatabase.FindAssets("t:BackpackItemSO", new[] { ITEMS_FOLDER });

        for (int i = 0; i < guids.Length; i++)
        {
            BackpackItemSO item = AssetDatabase.LoadAssetAtPath<BackpackItemSO>(AssetDatabase.GUIDToAssetPath(guids[i]));

            if (item != null)
                result.Add(item);
        }

        result.Sort((a, b) => string.Compare(a.Id, b.Id, System.StringComparison.Ordinal));
        return result;
    }
    // 商店配置只认钓鱼场景这一个宿主：场景没打开就临时附加进来，改完存盘再关掉，
    // 全程不读也不写当前活动场景，免得把用户正在编辑的场景顺手保存了
    private static void wireIntoScene(ShopConfigSO config)
    {
        if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(FISHING_SCENE_PATH)))
        {
            QLog.Error($"找不到钓鱼场景资产，商店配置没处挂：{FISHING_SCENE_PATH}");
            return;
        }

        bool wasOpened = tryFindOpenedScene(FISHING_SCENE_PATH, out Scene scene);

        if (!wasOpened)
        {
            scene = EditorSceneManager.OpenScene(FISHING_SCENE_PATH, OpenSceneMode.Additive);

            if (!scene.IsValid())
            {
                QLog.Error($"临时打开钓鱼场景失败：{FISHING_SCENE_PATH}");
                return;
            }
        }

        int wired = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (FishingScene fishingScene in root.GetComponentsInChildren<FishingScene>(true))
            {
                SerializedObject serialized = new SerializedObject(fishingScene);
                SerializedProperty property = serialized.FindProperty("ShopConfigAsset");

                if (property == null)
                {
                    QLog.Error($"钓鱼场景上没有 ShopConfigAsset 字段，请检查 {nameof(FishingScene)}");
                    closeIfNeeded(wasOpened, scene);
                    return;
                }

                if (property.objectReferenceValue == config)
                    continue;

                property.objectReferenceValue = config;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                wired++;
            }
        }

        if (wired == 0)
        {
            QLog.Info($"钓鱼场景的商店配置引用已是最新，未改动场景：{FISHING_SCENE_PATH}");
            closeIfNeeded(wasOpened, scene);
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);

        if (EditorSceneManager.SaveScene(scene))
            QLog.Info($"商店配置已挂进 {FISHING_SCENE_PATH}，共 {wired} 处");
        else
            QLog.Warning($"已挂好但场景没保存成功，请在编辑器里手动保存一次：{FISHING_SCENE_PATH}");

        closeIfNeeded(wasOpened, scene);
    }

    private static bool tryFindOpenedScene(string path, out Scene found)
    {
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            Scene candidate = EditorSceneManager.GetSceneAt(i);

            if (candidate.path == path)
            {
                found = candidate;
                return true;
            }
        }

        found = default;
        return false;
    }

    // 临时借来的场景用完就关，别在用户的场景页签里留残渣
    private static void closeIfNeeded(bool wasOpened, Scene scene)
    {
        if (wasOpened || !scene.IsValid())
            return;

        if (scene.isDirty)
            EditorSceneManager.SaveScene(scene);

        EditorSceneManager.CloseScene(scene, true);
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
