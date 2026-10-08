/*
* ┌──────────────────────────────────┐
* │  描    述: 形状库与网格判定的自检菜单，点一次把模板表和放置逻辑全跑一遍
* │  类    名: BackpackShapeValidator.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 工程里没有 EditMode 测试程序集，自检就挂在这个菜单上，改到位图或判定后点一下就知道有没有跑偏
* 铺满校验证明开局锚形刚好容纳方块加竖三条，且 2×3 板天然进不去，后期靠尺寸而不是靠额外锁
* 旋转校验专门盯自排除：忘了摘掉物品自己的占用位，原地旋转就会永远转不动
* 装备库与开局落位顺带一起跑，档位表填歪或开局放不下当场就报
* 临时物品只在内存里造，不落盘，不往工程里塞假资产
*/

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Module.Backpack.Data;
using Module.Backpack.Model;
using Utils.log;

public static class BackpackShapeValidator
{
    // ==================== 常量与静态字段 ====================
    private const string SHAPES_FOLDER = "Assets/Data/Backpack/Shapes";
    private const string ITEMS_FOLDER = "Assets/Data/Backpack/Items";
    private const string CONFIG_PATH = "Assets/Data/Config/BackpackConfig.asset";
    private const int CONTAINER_SIDE = 5;

    private static readonly Vector2Int[] m_anchorCells =
    {
        new Vector2Int(0, 0), new Vector2Int(1, 0),
        new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1),
        new Vector2Int(2, 2),
        new Vector2Int(2, 3)
    };

    private static readonly Vector2Int[] m_allCells =
    {
        new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(3, 0), new Vector2Int(4, 0),
        new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(3, 1), new Vector2Int(4, 1),
        new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 2), new Vector2Int(3, 2), new Vector2Int(4, 2),
        new Vector2Int(0, 3), new Vector2Int(1, 3), new Vector2Int(2, 3), new Vector2Int(3, 3), new Vector2Int(4, 3),
        new Vector2Int(0, 4), new Vector2Int(1, 4), new Vector2Int(2, 4), new Vector2Int(3, 4), new Vector2Int(4, 4)
    };

    private static readonly List<string> m_failures = new();
    private static readonly List<string> m_reports = new();

    // ==================== Menu ====================
    [MenuItem("Tools/钓鱼游戏/校验背包形状库与装备库")]
    public static void Validate()
    {
        m_failures.Clear();
        m_reports.Clear();

        printShapeTable();
        checkConfigAsset();
        checkAnchorPacking();
        checkRotationSelfExclusion();
        checkOccupancyConflict();
        checkUnlockContainment();
        checkItemLibrary();

        string text = string.Join("\n", m_reports);
        string tail = m_failures.Count == 0
            ? "全部通过"
            : $"失败 {m_failures.Count} 项：\n - " + string.Join("\n - ", m_failures);

        QLog.Info($"[形状库自检]\n{text}\n{tail}");
    }

    // ==================== Private Function ====================
    private static void printShapeTable()
    {
        string[] guids = AssetDatabase.FindAssets("t:GridShapeSO", new[] { SHAPES_FOLDER });

        if (guids.Length == 0)
        {
            m_failures.Add($"形状库为空，先跑「生成默认形状库与背包配置」");
            return;
        }

        m_reports.Add($"形状模板 {guids.Length} 个：");

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GridShapeSO shape = AssetDatabase.LoadAssetAtPath<GridShapeSO>(path);

            if (shape == null)
            {
                m_failures.Add($"读取形状资产失败：{path}");
                continue;
            }

            ShapeMask baseMask = shape.Base;
            m_reports.Add($"  {shape.Id} · {shape.Label} · 格数 {baseMask.CellCount} · 包围盒 {baseMask.Width}×{baseMask.Height} "
                + $"· 连通 {(baseMask.IsConnected() ? "是" : "否")} · 朝向 {shape.OrientationCount} · "
                + $"合规 {(shape.IsValid ? "是" : "否")}");

            if (!shape.IsValid)
                m_failures.Add($"形状不合规：{shape.Id}");
        }
    }

    // 配置资产是界面与玩法唯一的数据源，基准锚形对不上就说明有人手抖改了掩码
    private static void checkConfigAsset()
    {
        BackpackConfigSO config = AssetDatabase.LoadAssetAtPath<BackpackConfigSO>(CONFIG_PATH);

        if (config == null)
        {
            m_failures.Add("缺少背包配置资产：" + CONFIG_PATH);
            return;
        }

        bool sized = config.Columns == CONTAINER_SIDE && config.Rows == CONTAINER_SIDE;
        report($"配置：容器 {config.Columns}×{config.Rows} · 解锁 {config.UnlockStageCount} 级 · "
            + $"暂存 {config.StashCapacity} 件 · 售价 {config.TierPrice(1)}/{config.TierPrice(2)}/{config.TierPrice(3)}",
            sized && config.UnlockStageCount > 0);

        bool anchorMatch = config.UnlockMaskAt(0) == toBits(m_anchorCells);
        report($"开局锚形与自检基准一致：{anchorMatch}", anchorMatch);
    }
    // 方块加竖三条要正好铺满开局锚形，2×3 板要一个锚点都放不下
    private static void checkAnchorPacking()
    {
        long anchor = toBits(m_anchorCells);
        GridShapeSO block = loadShape("block_4");
        GridShapeSO line = loadShape("line_3");
        GridShapeSO slab = loadShape("slab_6");

        if (block == null || line == null || slab == null)
        {
            m_failures.Add("缺少 block_4 / line_3 / slab_6，铺满校验跑不起来");
            return;
        }

        BackpackModel model = new BackpackModel();
        model.Configure(CONTAINER_SIDE, CONTAINER_SIDE, anchor);

        BackpackItemInstance blockItem = createItem(block, 1);
        blockItem.AnchorColumn = 0;
        blockItem.AnchorRow = 0;
        BackpackItemInstance lineItem = createItem(line, 1);
        lineItem.AnchorColumn = 2;
        lineItem.AnchorRow = 1;

        bool blockPlaced = model.TryPlace(blockItem);
        bool linePlaced = model.TryPlace(lineItem);
        bool packedFull = model.FreeCells == 0 && model.UsedCells == m_anchorCells.Length;

        report($"锚形铺满：方块 {(blockPlaced ? "入" : "拒")} · 竖三条 {(linePlaced ? "入" : "拒")} · 剩余空格 {model.FreeCells}",
            blockPlaced && linePlaced && packedFull);

        BackpackModel probe = new BackpackModel();
        probe.Configure(CONTAINER_SIDE, CONTAINER_SIDE, anchor);
        bool slabFits = canPlaceAnywhere(probe, slab);

        report($"2×3 板在锚形内可放置锚点数：{(slabFits ? "有，后期锁不住" : "零，天然锁在后期")}", !slabFits);
    }

    // 自排除：L 形在空格子容器里连转四圈必须全部成功并回到初始朝向
    private static void checkRotationSelfExclusion()
    {
        GridShapeSO corner = loadShape("corner_3");
        GridShapeSO block = loadShape("block_4");

        if (corner == null || block == null)
        {
            m_failures.Add("缺少 corner_3 / block_4，旋转校验跑不起来");
            return;
        }

        BackpackModel model = new BackpackModel();
        model.Configure(CONTAINER_SIDE, CONTAINER_SIDE, toBits(m_allCells));

        BackpackItemInstance cornerItem = createItem(corner, 1);
        cornerItem.AnchorColumn = 1;
        cornerItem.AnchorRow = 1;

        if (!model.TryPlace(cornerItem))
        {
            m_failures.Add("L 形在空容器里放不下，自排除逻辑或位图有问题");
            return;
        }

        bool rotatedAll = true;

        for (int step = 0; step < 4; step++)
        {
            int before = cornerItem.OrientationIndex;
            rotatedAll &= model.TryRotate(cornerItem);
            rotatedAll &= cornerItem.OrientationIndex != before || corner.OrientationCount == 1;
        }

        bool backToStart = cornerItem.OrientationIndex == 0;
        bool cellsStable = model.UsedCells == corner.CellCount;

        report($"L 形连转四圈：{(rotatedAll ? "全通过" : "有失败")} · 回到初始朝向 {backToStart} · 占用格数 {model.UsedCells}",
            rotatedAll && backToStart && cellsStable);

        BackpackItemInstance blockItem = createItem(block, 1);
        blockItem.AnchorColumn = 3;
        blockItem.AnchorRow = 3;
        bool blockPlaced = model.TryPlace(blockItem);
        bool blockRotateRefused = !model.TryRotate(blockItem);

        report($"方块旋转：{(blockPlaced ? "已放入" : "放入失败")} · {(blockRotateRefused ? "拒绝旋转，界面不给按钮" : "居然转了")}",
            blockPlaced && blockRotateRefused);
    }

    // 同格必须拒，挪开必须过，取走后原格要能再放
    private static void checkOccupancyConflict()
    {
        GridShapeSO domino = loadShape("domino_2");

        if (domino == null)
        {
            m_failures.Add("缺少 domino_2，占用校验跑不起来");
            return;
        }

        BackpackModel model = new BackpackModel();
        model.Configure(CONTAINER_SIDE, CONTAINER_SIDE, toBits(m_allCells));

        BackpackItemInstance first = createItem(domino, 1);
        first.AnchorColumn = 0;
        first.AnchorRow = 0;
        BackpackItemInstance second = createItem(domino, 1);
        second.AnchorColumn = 0;
        second.AnchorRow = 0;
        BackpackItemInstance third = createItem(domino, 1);
        third.AnchorColumn = 1;
        third.AnchorRow = 0;

        bool firstPlaced = model.TryPlace(first);
        bool sameCellRefused = !model.TryPlace(second);
        bool neighborPlaced = model.TryPlace(third);
        bool hitTest = model.FindItemAt(0, 1) == first && model.FindItemAt(1, 0) == third;
        bool moveRefused = !model.TryMove(third, 0, 0);
        bool removed = model.Remove(first);
        bool refillAllowed = model.TryPlace(second);

        bool passed = firstPlaced && sameCellRefused && neighborPlaced && hitTest
            && moveRefused && removed && refillAllowed;

        report($"占用判定：同格拒 {sameCellRefused} · 邻格过 {neighborPlaced} · 命中查物 {hitTest} "
            + $"· 移入占用格拒 {moveRefused} · 取走后可补 {refillAllowed}", passed);
    }

    // 解锁只能往外长，往回收必须被拒绝，否则放好的物品会悬空
    private static void checkUnlockContainment()
    {
        long anchor = toBits(m_anchorCells);
        long full = toBits(m_allCells);
        BackpackModel model = new BackpackModel();
        model.Configure(CONTAINER_SIDE, CONTAINER_SIDE, anchor);

        GridShapeSO domino = loadShape("domino_2");

        if (domino == null)
        {
            m_failures.Add("缺少 domino_2，解锁包含校验跑不起来");
            return;
        }

        BackpackItemInstance item = createItem(domino, 1);
        item.AnchorColumn = 0;
        item.AnchorRow = 0;
        model.TryPlace(item);

        bool shrinkRefused = muted(() => !model.TrySetUnlockMask(anchor & ~(1L << 8)));
        bool expandAllowed = model.TrySetUnlockMask(full);
        bool movedFar = model.TryMove(item, 4, 3);
        bool shrinkAfterExpandRefused = muted(() => !model.TrySetUnlockMask(anchor));

        report($"解锁变更：关掉占用格被拒 {shrinkRefused} · 扩到满格被允许 {expandAllowed} · 移到新格 {movedFar} · 移过去后再关掉被拒 {shrinkAfterExpandRefused}",
            shrinkRefused && expandAllowed && movedFar && shrinkAfterExpandRefused);
    }

    // 装备库的档位表必须一档比一档大、一档比一档强，形状一律从模板库取
    private static void checkItemLibrary()
    {
        string[] guids = AssetDatabase.FindAssets("t:BackpackItemSO", new[] { ITEMS_FOLDER });

        if (guids.Length == 0)
        {
            m_failures.Add("装备库为空，先跑「生成默认装备库」");
            return;
        }

        m_reports.Add($"装备 {guids.Length} 件：");

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            BackpackItemSO item = AssetDatabase.LoadAssetAtPath<BackpackItemSO>(path);

            if (item == null)
            {
                m_failures.Add($"读取装备资产失败：{path}");
                continue;
            }

            int tiers = item.MaxTier;
            bool cellsGrow = true;
            bool valuesGrow = true;
            string cellText = "";
            string valueText = "";
            int lastCells = 0;
            float lastValue = 0f;

            for (int tier = 1; tier <= tiers; tier++)
            {
                GridShapeSO shape = item.ShapeAt(tier);
                int cells = shape != null ? shape.CellCount : 0;
                float value = item.ValueAt(tier);

                if (cells <= lastCells)
                    cellsGrow = false;

                if (value <= lastValue)
                    valuesGrow = false;

                lastCells = cells;
                lastValue = value;

                cellText += (tier > 1 ? "/" : "") + cells;
                valueText += (tier > 1 ? "/" : "") + value;
            }

            string line = $"  {item.Id} · {item.Label} · {item.Mod} · {tiers} 档 · 格数 {cellText} · 数值 {valueText}";
            report(line, cellsGrow && valuesGrow && tiers <= BackpackItemSO.MAX_TIER);
        }

        checkStartingPlacement();
    }

    // 开局装备一次全落网格、一件都不退暂存架，商店打开时看到的才是设计里的那个背包
    private static void checkStartingPlacement()
    {
        BackpackConfigSO config = AssetDatabase.LoadAssetAtPath<BackpackConfigSO>(CONFIG_PATH);

        if (config == null)
        {
            m_failures.Add($"背包配置未找到，开局落位校验跳过：{CONFIG_PATH}");
            return;
        }

        int starting = config.StartingItems.Count;

        if (starting == 0)
        {
            m_failures.Add("开局装备列表为空，进商店只能看到一个空背包");
            return;
        }

        RunBackpack backpack = new RunBackpack(config);
        backpack.SeedStartingItems();

        int placed = backpack.Model.ItemCount;
        int stashed = backpack.Stash.Count;

        report($"开局落位：装备 {starting} 件 · 上格 {placed} · 暂存 {stashed} · 占格 {backpack.Model.UsedCells}/{backpack.Model.UnlockedCells}",
            placed == starting && stashed == 0);
    }

    private static void report(string text, bool passed)
    {
        m_reports.Add(text + (passed ? "  通过" : "  失败"));

        if (!passed)
            m_failures.Add(text);
    }

    private static bool canPlaceAnywhere(BackpackModel model, GridShapeSO shape)
    {
        ShapeMask[] orientations = shape.Orientations;

        for (int i = 0; i < orientations.Length; i++)
        {
            ShapeMask current = orientations[i];

            for (int row = 0; row <= CONTAINER_SIDE - current.Height; row++)
            {
                for (int column = 0; column <= CONTAINER_SIDE - current.Width; column++)
                {
                    if (model.CanPlace(current, column, row))
                        return true;
                }
            }
        }

        return false;
    }

    // 内存造一件装备定义，档位表只挂一个形状，不落盘不留垃圾资产
    private static BackpackItemInstance createItem(GridShapeSO shape, int tier)
    {
        BackpackItemSO definition = ScriptableObject.CreateInstance<BackpackItemSO>();
        SerializedObject serialized = new SerializedObject(definition);
        serialized.FindProperty("ItemId").stringValue = $"probe_{shape.Id}";
        serialized.FindProperty("ItemLabel").stringValue = "自检";
        serialized.FindProperty("ModType").intValue = (int)BackpackStatMod.LineLength;

        SerializedProperty shapes = serialized.FindProperty("TierShapes");
        shapes.arraySize = tier;
        shapes.GetArrayElementAtIndex(tier - 1).objectReferenceValue = shape;

        SerializedProperty values = serialized.FindProperty("TierValues");
        values.arraySize = tier;
        values.GetArrayElementAtIndex(tier - 1).floatValue = 1f;

        serialized.ApplyModifiedPropertiesWithoutUndo();

        return new BackpackItemInstance { Definition = definition, Tier = tier };
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

    // 被拒的判定本身要报 Error，自检里这是正确行为，静音掉免得 Console 全是假警报
    private static T muted<T>(Func<T> body)
    {
        bool keep = Debug.unityLogger.logEnabled;
        Debug.unityLogger.logEnabled = false;

        try
        {
            return body();
        }
        finally
        {
            Debug.unityLogger.logEnabled = keep;
        }
    }

    private static long toBits(Vector2Int[] cells)
    {
        long bits = 0;

        for (int i = 0; i < cells.Length; i++)
            bits |= 1L << (cells[i].y * ShapeMask.ROW_STRIDE + cells[i].x);

        return bits;
    }
}
