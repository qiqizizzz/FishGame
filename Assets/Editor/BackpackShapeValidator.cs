/*
* ┌──────────────────────────────────┐
* │  描    述: 形状库与网格判定的自检菜单，点一次把模板表和放置逻辑全跑一遍
* │  类    名: BackpackShapeValidator.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 工程里没有 EditMode 测试程序集，自检就挂在这个菜单上，改到位图或判定后点一下就知道有没有跑偏
* 格子全靠布铺出来，铺满校验验的是两块开局布拼出的 5 格，以及拆布挤走装备时的暂存架兜底
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
using Module.Fish.Data;
using Utils.log;

public static class BackpackShapeValidator
{
    // ==================== 常量与静态字段 ====================
    private const string SHAPES_FOLDER = "Assets/Data/Backpack/Shapes";
    private const string ITEMS_FOLDER = "Assets/Data/Backpack/Items";
    private const string CONFIG_PATH = "Assets/Data/Config/BackpackConfig.asset";
    private const string HOOK_CONFIG_PATH = "Assets/Data/Config/HookConfig.asset";
    private const int CONTAINER_COLUMNS = 6;
    private const int CONTAINER_ROWS = 5;

    // 铺满校验要逐列铺竖三条加竖二连，5 列刚好盖满 m_allCells 的 25 格
    private const int FULL_TILING_COLUMNS = 5;

    // 开局那 5 格由 3 格布加 2 格布拼出来，自检一切以这块并集为准
    private static readonly Vector2Int[] m_startingCells =
    {
        new Vector2Int(2, 0), new Vector2Int(3, 0),
        new Vector2Int(2, 1), new Vector2Int(3, 1),
        new Vector2Int(2, 2)
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
        checkStartingClothPacking();
        checkRotationSelfExclusion();
        checkOccupancyConflict();
        checkPlateSupport();
        checkClothStashGuard();
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

    // 配置资产是界面与玩法唯一的数据源，开局布拼出的格对不上就说明有人手抖改了摆放
    private static void checkConfigAsset()
    {
        BackpackConfigSO config = AssetDatabase.LoadAssetAtPath<BackpackConfigSO>(CONFIG_PATH);

        if (config == null)
        {
            m_failures.Add("缺少背包配置资产：" + CONFIG_PATH);
            return;
        }

        bool sized = config.Columns == CONTAINER_COLUMNS && config.Rows == CONTAINER_ROWS;
        report($"配置：容器 {config.Columns}×{config.Rows} · 开局布 {config.StartingClothCount} 块 · "
            + $"暂存 {config.StashCapacity} 件 · 售价 {config.TierPrice(1)}/{config.TierPrice(2)}/{config.TierPrice(3)}",
            sized && config.StartingClothCount > 0);

        bool anchorMatch = config.BuildStartingMask() == toBits(m_startingCells);
        report($"开局布拼出的格与自检基准一致：{anchorMatch}", anchorMatch);
    }
    // 开局两块布要正好拼出基准的 5 格，多一格少一格都算配置摆歪了
    private static void checkStartingClothPacking()
    {
        GridShapeSO domino = loadShape("domino_2");
        GridShapeSO line = loadShape("line_3");
        GridShapeSO slab = loadShape("slab_6");

        if (domino == null || line == null || slab == null)
        {
            m_failures.Add("缺少 domino_2 / line_3 / slab_6，开局布铺格校验跑不起来");
            return;
        }

        BackpackModel model = new BackpackModel();
        model.Configure(CONTAINER_COLUMNS, CONTAINER_ROWS);

        BackpackItemInstance lineCloth = createCloth(line, 2, 0);
        BackpackItemInstance dominoCloth = createCloth(domino, 3, 0);

        bool linePaved = model.TryPlacePlate(lineCloth);
        bool dominoPaved = model.TryPlacePlate(dominoCloth);
        bool unionMatch = model.PlateMask == toBits(m_startingCells);

        report($"开局铺格：竖三条 {(linePaved ? "铺上" : "失败")} · 竖两块 {(dominoPaved ? "铺上" : "失败")} · "
            + $"并集 {toHex(model.PlateMask)} 可用格 {model.UnlockedCells} 基准 {toHex(toBits(m_startingCells))}",
            linePaved && dominoPaved && unionMatch && model.UnlockedCells == m_startingCells.Length);

        // 布和布不许重叠，同一格铺两次不会多出一格
        BackpackItemInstance overlapCloth = createCloth(line, 2, 0);
        bool overlapRefused = !model.TryPlacePlate(overlapCloth);
        report($"布压布：同锚点再铺一块被拒 {overlapRefused}", overlapRefused);

        // 格子全靠布铺，6 格的大布在只有 5 格的开局范围里一个锚点都找不到
        BackpackModel probe = new BackpackModel();
        probe.Configure(CONTAINER_COLUMNS, CONTAINER_ROWS);
        bool slabOnEmptyFloor = canPlacePlateAnywhere(probe, slab);

        // 布没铺之前一格都没有，装备一件也落不下去
        report($"空容器：铺得出板 {slabOnEmptyFloor} · 落不下装备 {!canPlaceItemAnywhere(probe, domino)}",
            slabOnEmptyFloor && !canPlaceItemAnywhere(probe, domino));

        // 铺满之后 2×3 装备要能压在布上，这才是买布换格子的好处
        BackpackModel grown = new BackpackModel();
        grown.Configure(CONTAINER_COLUMNS, CONTAINER_ROWS);
        tileFullPlates(grown);
        bool slabItemFits = canPlaceItemAnywhere(grown, slab);
        report($"铺满 25 格后：可用格 {grown.UnlockedCells} · 空格 {grown.FreeCells} · 2×3 装备可落 {slabItemFits}",
            grown.UnlockedCells == m_allCells.Length && slabItemFits);
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
        model.Configure(CONTAINER_COLUMNS, CONTAINER_ROWS);
        tileFullPlates(model);

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
        model.Configure(CONTAINER_COLUMNS, CONTAINER_ROWS);
        tileFullPlates(model);

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

    // 布是格子的唯一来源：拆走压着装备的布必须把装备一起摘下来，接不下就整次不动
    private static void checkPlateSupport()
    {
        GridShapeSO domino = loadShape("domino_2");

        if (domino == null)
        {
            m_failures.Add("缺少 domino_2，板块支撑校验跑不起来");
            return;
        }

        BackpackModel model = new BackpackModel();
        model.Configure(CONTAINER_COLUMNS, CONTAINER_ROWS);

        BackpackItemInstance cloth = createCloth(domino, 0, 0);
        bool paved = model.TryPlacePlate(cloth);
        BackpackItemInstance gear = createItem(domino, 1);
        gear.AnchorColumn = 0;
        gear.AnchorRow = 0;
        bool stacked = model.TryPlace(gear);
        bool cellShared = model.FindPlateAt(0, 1) == cloth && model.FindItemAt(0, 1) == gear;
        // 列 4 行 3 在容器内但没铺布，装备落上去就该被拒
        bool floorlessRefused = !model.CanPlace(domino.OrientationAt(0), 4, 3);

        report($"同格叠层：铺板 {paved} · 压装备 {stacked} · 一格两层 {cellShared} · 无底格放装备被拒 {floorlessRefused}",
            paved && stacked && cellShared && floorlessRefused);

        List<BackpackItemInstance> orphans = new();
        bool overCapacityRefused = muted(() => !model.TryRemovePlate(cloth, 0, orphans));
        bool stateUntouched = model.PlateCount == 1 && model.ItemCount == 1 && model.UnlockedCells == 2;
        bool dropped = model.TryRemovePlate(cloth, 1, orphans);

        report($"拆布挤装备：容量不够被拒 {overCapacityRefused} · 拒绝后状态不变 {stateUntouched} · "
            + $"容量够时拆成 {dropped} · 摘下的装备 {orphans.Count} 件 · 拆完可用格 {model.UnlockedCells}",
            overCapacityRefused && stateUntouched && dropped && orphans.Count == 1 && orphans[0] == gear
                && model.PlateCount == 0 && model.ItemCount == 0 && model.UnlockedCells == 0);
    }

    // 挪布和转布也一样：暂存架接不住悬空装备的这一次就不该发生
    private static void checkClothStashGuard()
    {
        BackpackConfigSO config = AssetDatabase.LoadAssetAtPath<BackpackConfigSO>(CONFIG_PATH);
        GridShapeSO line = loadShape("line_3");
        GridShapeSO domino = loadShape("domino_2");

        if (config == null || line == null || domino == null)
        {
            m_failures.Add("背包配置或形状缺失，暂存架兜底校验跑不起来");
            return;
        }

        BackpackModel model = new BackpackModel();
        model.Configure(CONTAINER_COLUMNS, CONTAINER_ROWS);

        BackpackItemInstance cloth = createCloth(line, 0, 0);
        model.TryPlacePlate(cloth);
        BackpackItemInstance gear = createItem(domino, 1);
        gear.AnchorColumn = 0;
        gear.AnchorRow = 0;
        model.TryPlace(gear);

        List<BackpackItemInstance> orphans = new();
        bool moveRefused = muted(() => !model.TryMovePlate(cloth, 3, 0, 0, orphans));
        bool stillPaved = model.UnlockedCells == line.CellCount && model.UsedCells == domino.CellCount;

        // 挪去列 3 之后列 0 那三格没了底，装备必须被摘下来，一处都不许是半成品
        bool moveAllowed = model.TryMovePlate(cloth, 3, 0, 1, orphans);

        report($"挪布兜底：架满时整次拒绝 {moveRefused} · 拒绝后格子与装备原样 {stillPaved} · "
            + $"接得下时挪走 {moveAllowed} · 被摘装备 {orphans.Count} 件 · 剩可用格 {model.UnlockedCells}",
            moveRefused && stillPaved && moveAllowed && orphans.Count == 1 && model.ItemCount == 0
                && model.PlateCount == 1 && model.UnlockedCells == line.CellCount);
    }

    // 装备库的档位表必须一档比一档大、一档比一档强，形状一律从模板库取
    // 一档比一档强按首档符号定向：负向 BUFF 越负越强，别当成填反了
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

            // 加成的强弱方向看首档的符号，负向 BUFF 是数值越往下走越强
            int direction = 0;

            for (int tier = 1; tier <= tiers; tier++)
            {
                GridShapeSO shape = item.ShapeAt(tier);
                int cells = shape != null ? shape.CellCount : 0;
                float value = item.ValueAt(tier);

                if (cells <= lastCells)
                    cellsGrow = false;

                if (direction == 0 && value != 0f)
                    direction = value < 0f ? -1 : 1;

                if (direction > 0 ? value <= lastValue : value >= lastValue)
                    valuesGrow = false;

                lastCells = cells;
                lastValue = value;

                cellText += (tier > 1 ? "/" : "") + cells;
                valueText += (tier > 1 ? "/" : "") + value;
            }

            string weakPoint = "";

            if (!cellsGrow)
                weakPoint += "格数没有一档比一档大";

            if (!valuesGrow)
                weakPoint += (weakPoint.Length > 0 ? "、" : "") + "加成没有一档比一档强";

            string line = $"  {item.Id} · {item.Label} · {item.Mod} · {tiers} 档 · 格数 {cellText} · 数值 {valueText}"
                + (weakPoint.Length > 0 ? " · 待调：" + weakPoint : "");

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

        HookConfigSO hookConfig = AssetDatabase.LoadAssetAtPath<HookConfigSO>(HOOK_CONFIG_PATH);

        if (hookConfig == null)
        {
            m_failures.Add($"钓竿参数未找到，开局落位校验跳过：{HOOK_CONFIG_PATH}");
            return;
        }

        RunBackpack backpack = new RunBackpack(config, hookConfig);
        backpack.SeedStartingCloths();
        backpack.SeedStartingItems();

        int placed = backpack.Model.ItemCount;
        int stashed = backpack.Stash.Count;

        report($"开局落位：布料 {backpack.Model.PlateCount} 块 · 装备 {starting} 件 · 上格 {placed} · 暂存 {stashed} · 占格 {backpack.Model.UsedCells}/{backpack.Model.UnlockedCells}",
            placed == starting && stashed == 0);
    }

    private static void report(string text, bool passed)
    {
        m_reports.Add(text + (passed ? "  通过" : "  失败"));

        if (!passed)
            m_failures.Add(text);
    }

    // 装备层全锚点扫描：问这件装备在这面容器里到底有没有一个落点
    private static bool canPlaceItemAnywhere(BackpackModel model, GridShapeSO shape)
    {
        return canPlaceAnywhere(model, shape, false);
    }

    // 板块层全锚点扫描：铺板只看会不会压到别的布，装备在上头不算冲突
    private static bool canPlacePlateAnywhere(BackpackModel model, GridShapeSO shape)
    {
        return canPlaceAnywhere(model, shape, true);
    }

    private static bool canPlaceAnywhere(BackpackModel model, GridShapeSO shape, bool plateLayer)
    {
        ShapeMask[] orientations = shape.Orientations;

        for (int i = 0; i < orientations.Length; i++)
        {
            ShapeMask current = orientations[i];

            for (int row = 0; row <= CONTAINER_ROWS - current.Height; row++)
            {
                for (int column = 0; column <= CONTAINER_COLUMNS - current.Width; column++)
                {
                    bool fits = plateLayer
                        ? model.CanPlacePlate(current, column, row)
                        : model.CanPlace(current, column, row);

                    if (fits)
                        return true;
                }
            }
        }

        return false;
    }

    // 满铺基准：每列竖三条盖住上三行、竖两块盖住下两行，五列正好 25 格
    private static void tileFullPlates(BackpackModel model)
    {
        GridShapeSO line = loadShape("line_3");
        GridShapeSO domino = loadShape("domino_2");

        if (line == null || domino == null)
        {
            m_failures.Add("缺少 line_3 / domino_2，满铺基准铺不出来");
            return;
        }

        for (int column = 0; column < FULL_TILING_COLUMNS; column++)
        {
            model.TryPlacePlate(createCloth(line, column, 0));
            model.TryPlacePlate(createCloth(domino, column, 3));
        }
    }

    // 内存造一块布：模型只按形状与锚点裁决，Kind 留给运行期分流，自检不必造真布
    private static BackpackItemInstance createCloth(GridShapeSO shape, int anchorColumn, int anchorRow)
    {
        BackpackItemInstance plate = createItem(shape, 1);
        plate.AnchorColumn = anchorColumn;
        plate.AnchorRow = anchorRow;
        return plate;
    }

    // 位图太长没法读，自检报告统一成十六进制
    private static string toHex(long bits)
    {
        return "0x" + bits.ToString("X2");
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
