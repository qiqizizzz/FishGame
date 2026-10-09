/*
* ┌──────────────────────────────────┐
* │  描    述: 把开局布料铺出的格烘进商店背包预制体，Prefab 模式下直接看得见空格
* │  类    名: BackpackSlotAppearanceBaker.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 运行时外观由 UIShopSlotItem 按板块掩码逐格切换，预制体里只留一份默认底图，编辑器下 30 格长一个样
* 这里读同一份背包配置，把每格 Image 的底图与颜色写成实值，写完仍可在 Prefab 模式逐格手调
* 只动格子节点上的 Image 与名称文本，不碰版式、层级与尺寸，重跑不会覆盖手工调过的布局
* 改过开局布料或格子配色后点一次即可，运行时的切换逻辑依旧是唯一权威
*/

using Module.Backpack.Data;
using Module.Backpack.Model;
using UI.Shop;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Utils.log;

public static class BackpackSlotAppearanceBaker
{
    // ==================== 常量与静态字段 ====================
    private const string PANEL_PATH = "Assets/Prefabs/UI/UIShopPanel.prefab";
    private const string CONFIG_PATH = "Assets/Data/Config/BackpackConfig.asset";
    private const string GRID_PATH = "Bg/Panel_Backpack/Grid_Slots";
    private const string STORAGE_PATH = "Bg/Panel_Storage/Grid_Storage";
    private const string LABEL_PATH = "Txt_Label";
    private const string SLOT_NAME_PREFIX = "Slot_";

    // ==================== Public Function ====================
    [MenuItem("Tools/钓鱼游戏/烘焙 背包格子外观")]
    public static void bake()
    {
        BackpackConfigSO config = AssetDatabase.LoadAssetAtPath<BackpackConfigSO>(CONFIG_PATH);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PANEL_PATH);

        if (config == null || prefab == null)
        {
            QLog.Error($"背包配置或预制体未找到：{CONFIG_PATH} / {PANEL_PATH}");
            return;
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(PANEL_PATH);

        try
        {
            Transform grid = contents.transform.Find(GRID_PATH);
            Transform storage = contents.transform.Find(STORAGE_PATH);

            if (grid == null || storage == null)
            {
                QLog.Error($"预制体缺少网格节点：{GRID_PATH} / {STORAGE_PATH}");
                return;
            }

            int usedCells = bakeBackpack(grid.GetComponent<RectTransform>(), config.Columns, config.Rows, config.BuildStartingMask());
            int storageCells = bakeStorage(storage.GetComponent<RectTransform>());

            if (usedCells < 0 || storageCells < 0)
                return;

            PrefabUtility.SaveAsPrefabAsset(contents, PANEL_PATH);
            AssetDatabase.SaveAssets();

            Debug.Log($"[{nameof(BackpackSlotAppearanceBaker)}] 已写进预制体：背包 {usedCells}/{config.Columns * config.Rows} 格为空格，仓库 {storageCells} 格为空格");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    // ==================== Private Function ====================
    // 背包格逐格问开局掩码，格数或命名跟配置的行列对不上就停手，别把错位的版式烘进资产
    private static int bakeBackpack(RectTransform grid, int columns, int rows, long startMask)
    {
        UIShopSlotItem[] slots = grid.GetComponentsInChildren<UIShopSlotItem>(true);
        int expected = columns * rows;

        if (slots.Length != expected)
        {
            QLog.Error($"背包格数与配置不符：预制体 {slots.Length} 格，配置 {columns}×{rows}");
            return -1;
        }

        int emptyCells = 0;

        for (int i = 0; i < slots.Length; i++)
        {
            int row = i / columns;
            int column = i % columns;
            string expectedName = SLOT_NAME_PREFIX + row + "_" + column;

            if (slots[i].name != expectedName)
            {
                QLog.Error($"背包格顺序与命名不符：第 {i} 格是 {slots[i].name}，应为 {expectedName}");
                return -1;
            }

            bool unlocked = (startMask & (1L << (row * ShapeMask.ROW_STRIDE + column))) != 0;

            if (unlocked)
                emptyCells++;

            applyAppearance(slots[i], unlocked);
        }

        return emptyCells;
    }

    // 仓库不参与网格，开局全是空位，外观一律按空格幽灵处理
    private static int bakeStorage(RectTransform grid)
    {
        UIShopSlotItem[] slots = grid.GetComponentsInChildren<UIShopSlotItem>(true);

        for (int i = 0; i < slots.Length; i++)
            applyAppearance(slots[i], true);

        return slots.Length;
    }

    // 空格留圆角底图压成半透明，锁格去掉底图只留一块压暗的凹陷，与运行时的两个接口一一对应
    private static void applyAppearance(UIShopSlotItem slot, bool unlocked)
    {
        if (!slot.TryGetComponent(out Image bg))
        {
            QLog.Error($"格子上未挂载 Image：{slot.gameObject.name}");
            return;
        }

        SerializedObject serialized = new SerializedObject(slot);
        Sprite cellSprite = serialized.FindProperty("CellSprite").objectReferenceValue as Sprite;
        Color emptyColor = serialized.FindProperty("EmptyColor").colorValue;
        Color lockedColor = serialized.FindProperty("LockedColor").colorValue;

        if (unlocked && cellSprite == null)
        {
            QLog.Error($"空格子底图未指定：{slot.gameObject.name}");
            return;
        }

        bg.sprite = unlocked ? cellSprite : null;
        bg.color = unlocked ? emptyColor : lockedColor;

        Transform label = slot.transform.Find(LABEL_PATH);
        if (label != null)
            label.gameObject.SetActive(false);
    }
}
