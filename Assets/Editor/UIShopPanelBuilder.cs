/*
* ┌──────────────────────────────────┐
* │  描    述: 商店背包面板版式工具，按背包、仓库、商店三区重建面板结构
* │  类    名: UIShopPanelBuilder.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 版式改动只走这里，菜单跑一次把结果写进预制体，不参与运行时
* 面板主标题、状态条、底部按钮三块不碰，保持手工状态
* 重跑会删掉三个面板重新生成，预制体里手改过的版式会被覆盖
*/

using System.Linq;
using TMPro;
using UI.Shop;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Utils.log;

public static class UIShopPanelBuilder
{
    // ==================== 常量与静态字段 ====================
    private const string PANEL_PATH = "Assets/Prefabs/UI/UIShopPanel.prefab";
    private const string BG_NAME = "Bg";

    private const string PLATE_PATH = "Assets/Art/UI/Setting/Setting menu.png";
    private const string PLATE_NAME = "Setting menu_1";
    private const string CELL_PATH = "Assets/Art/UI/Common/Buttons/Square Buttons 26x26.png";
    private const string CELL_NAME = "Square Buttons 26x26_1";
    private const string BUTTON_PATH = "Assets/Art/UI/Common/Sprite sheet for Basic Pack.png";
    private const string BUTTON_NAME = "Button_long";
    private const string FONT_PATH = "Assets/Font/MaoKenTangYuan-beta0.12 SDF.asset";

    private const float PANEL_WIDTH = 940f;
    private const float BACKPACK_TOP = 338f;
    private const float CONTENT_TOP = 66f;
    private const float PANEL_BOTTOM = 12f;
    private const float SECTION_GAP = 12f;
    private const float SIDE_INSET = 36f;

    private const float TITLE_INSET = 36f;
    private const float TITLE_TOP = 14f;
    private const float TITLE_HEIGHT = 40f;
    private const int TITLE_FONT = 32;

    private const int GRID_COLUMNS = 5;
    private const int GRID_ROWS = 5;
    private const float CELL_SIZE = 156f;
    private const float CELL_SPACING = 8f;

    private const int STORAGE_COUNT = 5;
    private const float STORAGE_CELL = 104f;
    private const float STORAGE_SPACING = 12f;
    private const int STORAGE_LABEL_FONT = 20;

    private const float CARD_WIDTH = 202f;
    private const float CARD_HEIGHT = 150f;
    private const float CARD_SPACING = 16f;

    private static readonly Vector2 m_topLeft = new Vector2(0f, 1f);
    private static readonly Vector2 m_topCenter = new Vector2(0.5f, 1f);
    private static readonly Vector2 m_topRight = new Vector2(1f, 1f);
    private static readonly Vector2 m_bottomCenter = new Vector2(0.5f, 0f);
    private static readonly Vector2 m_center = new Vector2(0.5f, 0.5f);
    private static readonly Color m_textDark = new Color32(0x4A, 0x32, 0x20, 0xFF);

    private static TMP_FontAsset m_font;
    private static Sprite m_plateSprite;
    private static Sprite m_cellSprite;
    private static Sprite m_buttonSprite;

    // ==================== Public Function ====================
    [MenuItem("Tools/钓鱼游戏/重建 商店背包面板版式")]
    public static void rebuild()
    {
        if (!loadAssets())
            return;

        GameObject contents = PrefabUtility.LoadPrefabContents(PANEL_PATH);
        RectTransform bg = contents.transform.Find(BG_NAME) as RectTransform;

        if (bg == null)
        {
            QLog.Error($"预制体 {PANEL_PATH} 下找不到节点 {BG_NAME}，已放弃重建");
            PrefabUtility.UnloadPrefabContents(contents);
            return;
        }

        destroyChild(bg, "Panel_Backpack");
        destroyChild(bg, "Panel_Storage");
        destroyChild(bg, "Panel_Shop");

        float next = buildBackpack(bg, BACKPACK_TOP) + SECTION_GAP;
        next = buildStorage(bg, next) + SECTION_GAP;
        float bottom = buildShop(bg, next);

        PrefabUtility.SaveAsPrefabAsset(contents, PANEL_PATH);
        PrefabUtility.UnloadPrefabContents(contents);
        AssetDatabase.SaveAssets();

        QLog.Info($"商店背包面板版式已重建，商店面板底边距屏幕顶部 {bottom:F0} 像素");
    }

    // ==================== Private Function ====================
    // 背包面板：标题 + 5x5 网格 + 盖在网格上的物品图层，返回面板底边距屏幕顶的距离
    private static float buildBackpack(RectTransform bg, float top)
    {
        float gridSide = GRID_COLUMNS * CELL_SIZE + (GRID_COLUMNS - 1) * CELL_SPACING;
        RectTransform panel = createPanel(bg, "Panel_Backpack", top, CONTENT_TOP + gridSide + PANEL_BOTTOM, 2);
        createPanelTitle(panel, "背包");

        RectTransform grid = createNode(panel, "Grid_Slots", m_topCenter, m_topCenter, new Vector2(0f, -CONTENT_TOP), new Vector2(gridSide, gridSide));
        addGrid(grid, CELL_SIZE, CELL_SPACING, GRID_COLUMNS);

        for (int row = 0; row < GRID_ROWS; row++)
        {
            for (int column = 0; column < GRID_COLUMNS; column++)
            {
                RectTransform cell = createCell(grid, $"Slot_{row}_{column}", CELL_SIZE, m_cellSprite);
                cell.gameObject.AddComponent<UIShopSlotItem>();
                wireSlot(cell.gameObject);
            }
        }

        RectTransform items = createNode(panel, "Layer_Items", m_topCenter, m_topCenter, new Vector2(0f, -CONTENT_TOP), new Vector2(gridSide, gridSide));
        float pitch = CELL_SIZE + CELL_SPACING;
        RectTransform template = createNode(items, "Item_Template", m_topLeft, m_topLeft, Vector2.zero, new Vector2(pitch, pitch));
        template.gameObject.AddComponent<UIShopItemView>();

        RectTransform label = createStretchNode(template, "Txt_Label", Vector2.zero);
        addText(label, string.Empty, 22, m_textDark, TextAlignmentOptions.Center);
        template.gameObject.SetActive(false);

        return top + panel.sizeDelta.y;
    }

    // 仓库面板：标题 + 一行暂存格，每格自带名称文字
    private static float buildStorage(RectTransform bg, float top)
    {
        float rowWidth = STORAGE_COUNT * STORAGE_CELL + (STORAGE_COUNT - 1) * STORAGE_SPACING;
        RectTransform panel = createPanel(bg, "Panel_Storage", top, CONTENT_TOP + STORAGE_CELL + PANEL_BOTTOM, 3);
        createPanelTitle(panel, "仓库");

        RectTransform grid = createNode(panel, "Grid_Storage", m_topCenter, m_topCenter, new Vector2(0f, -CONTENT_TOP), new Vector2(rowWidth, STORAGE_CELL));
        addGrid(grid, STORAGE_CELL, STORAGE_SPACING, STORAGE_COUNT);

        for (int i = 0; i < STORAGE_COUNT; i++)
        {
            RectTransform cell = createCell(grid, $"Storage_{i}", STORAGE_CELL, m_cellSprite);
            cell.gameObject.AddComponent<UIShopSlotItem>();
            wireSlot(cell.gameObject);

            RectTransform label = createNode(cell, "Txt_Label", m_center, m_center, Vector2.zero, new Vector2(STORAGE_CELL - 8f, 28f));
            addText(label, string.Empty, STORAGE_LABEL_FONT, m_textDark, TextAlignmentOptions.Center);
        }

        return top + panel.sizeDelta.y;
    }

    // 商店面板：标题 + 刷新按钮 + 一排商品卡，卡片用隐藏模板复制
    private static float buildShop(RectTransform bg, float top)
    {
        RectTransform panel = createPanel(bg, "Panel_Shop", top, CONTENT_TOP + CARD_HEIGHT + PANEL_BOTTOM, 4);
        createPanelTitle(panel, "商店");

        RectTransform refresh = createNode(panel, "Btn_Refresh", m_topRight, m_topRight, new Vector2(-30f, -10f), new Vector2(150f, 48f));
        addImage(refresh, m_buttonSprite, Color.white, true, true);
        addImageButton(refresh);
        addText(createStretchNode(refresh, "Text", Vector2.zero), "刷新", 28, m_textDark, TextAlignmentOptions.Center);

        float rowWidth = PANEL_WIDTH - SIDE_INSET * 2f;
        RectTransform row = createNode(panel, "Row_Goods", m_topCenter, m_topCenter, new Vector2(0f, -CONTENT_TOP), new Vector2(rowWidth, CARD_HEIGHT));
        HorizontalLayoutGroup goodsLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        goodsLayout.spacing = CARD_SPACING;
        goodsLayout.childAlignment = TextAnchor.MiddleCenter;
        goodsLayout.childControlWidth = false;
        goodsLayout.childControlHeight = false;
        goodsLayout.childForceExpandWidth = false;
        goodsLayout.childForceExpandHeight = false;

        RectTransform card = createNode(row, "Goods_Template", m_topLeft, m_topLeft, Vector2.zero, new Vector2(CARD_WIDTH, CARD_HEIGHT));
        addImage(card, m_plateSprite, Color.white, true, true);
        addImageButton(card);
        card.gameObject.AddComponent<UIShopGoodsItem>();

        addImage(createNode(card, "Img_Icon", m_topCenter, m_topCenter, new Vector2(0f, -12f), new Vector2(72f, 72f)), null, Color.white, false, false);
        addText(createNode(card, "Txt_Name", m_topCenter, m_topCenter, new Vector2(0f, -86f), new Vector2(CARD_WIDTH - 12f, 26f)), string.Empty, 24, m_textDark, TextAlignmentOptions.Center);
        addText(createNode(card, "Txt_Price", m_bottomCenter, m_bottomCenter, new Vector2(0f, 6f), new Vector2(CARD_WIDTH - 12f, 28f)), string.Empty, 26, m_textDark, TextAlignmentOptions.Center);
        card.gameObject.SetActive(false);

        return top + panel.sizeDelta.y;
    }

    // 面板底板，三块面板共用同一套内边距，标题带下方 CONTENT_TOP 起放内容
    private static RectTransform createPanel(RectTransform bg, string panelName, float top, float height, int siblingIndex)
    {
        RectTransform panel = createNode(bg, panelName, m_topCenter, m_topCenter, new Vector2(0f, -top), new Vector2(PANEL_WIDTH, height));
        addImage(panel, m_plateSprite, Color.white, false, true);
        panel.SetSiblingIndex(siblingIndex);
        return panel;
    }

    private static void createPanelTitle(RectTransform panel, string title)
    {
        RectTransform label = createNode(panel, "Txt_Title", m_topLeft, m_topLeft, new Vector2(TITLE_INSET, -TITLE_TOP), new Vector2(400f, TITLE_HEIGHT));
        addText(label, title, TITLE_FONT, m_textDark, TextAlignmentOptions.MidlineLeft);
    }

    private static RectTransform createCell(RectTransform grid, string cellName, float cellSize, Sprite sprite)
    {
        RectTransform cell = createNode(grid, cellName, m_topLeft, m_topLeft, Vector2.zero, new Vector2(cellSize, cellSize));
        addImage(cell, sprite, Color.white, false, false);
        return cell;
    }

    private static void addGrid(RectTransform grid, float cellSize, float spacing, int columns)
    {
        GridLayoutGroup layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(cellSize, cellSize);
        layout.spacing = new Vector2(spacing, spacing);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = columns;
        layout.childAlignment = TextAnchor.UpperLeft;
    }

    // 格子底图只能在预制体里配，脚本字段是私有的，走序列化对象写入
    private static void wireSlot(GameObject cell)
    {
        SerializedObject serialized = new SerializedObject(cell.GetComponent<UIShopSlotItem>());
        serialized.FindProperty("CellSprite").objectReferenceValue = m_cellSprite;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static RectTransform createNode(RectTransform parent, string nodeName, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        GameObject nodeObject = new GameObject(nodeName, typeof(RectTransform));
        RectTransform node = (RectTransform)nodeObject.transform;
        node.SetParent(parent, false);
        node.anchorMin = anchor;
        node.anchorMax = anchor;
        node.pivot = pivot;
        node.anchoredPosition = position;
        node.sizeDelta = size;
        return node;
    }

    private static RectTransform createStretchNode(RectTransform parent, string nodeName, Vector2 sizeOffset)
    {
        RectTransform node = createNode(parent, nodeName, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, sizeOffset);
        node.anchorMax = Vector2.one;
        return node;
    }

    private static Image addImage(RectTransform node, Sprite sprite, Color color, bool raycast, bool sliced)
    {
        Image image = node.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = raycast;

        if (sliced && sprite != null)
            image.type = Image.Type.Sliced;

        return image;
    }

    private static Button addImageButton(RectTransform node)
    {
        Button button = node.gameObject.AddComponent<Button>();
        button.targetGraphic = node.GetComponent<Image>();
        return button;
    }

    private static TextMeshProUGUI addText(RectTransform node, string content, int fontSize, Color color, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI text = node.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = m_font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.text = content;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static void destroyChild(RectTransform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
            Object.DestroyImmediate(child.gameObject, true);
    }

    private static bool loadAssets()
    {
        m_font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);
        m_plateSprite = loadSprite(PLATE_PATH, PLATE_NAME);
        m_cellSprite = loadSprite(CELL_PATH, CELL_NAME);
        m_buttonSprite = loadSprite(BUTTON_PATH, BUTTON_NAME);

        if (m_font == null || m_plateSprite == null || m_cellSprite == null || m_buttonSprite == null)
        {
            QLog.Error("版式重建缺少素材或字体，已放弃重建");
            return false;
        }

        return true;
    }

    private static Sprite loadSprite(string assetPath, string spriteName)
    {
        return AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().FirstOrDefault(sprite => sprite.name == spriteName);
    }
}
