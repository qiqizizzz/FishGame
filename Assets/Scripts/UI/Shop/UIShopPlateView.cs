/*
* ┌──────────────────────────────────┐
* │  描    述: 背包里的一块布，逐格铺出布色并在整块布的外轮廓留暗边，可拖动可选中
* │  类    名: UIShopPlateView.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 布是格子的唯一来源，画在装备底下那一层，所以本视图由面板运行时创建，不进预制体
* 暗边只出现在整块布的最外圈，相邻两格之间一点不留，读起来是一整块布而不是一堆方块
* 有没有邻居靠世界格去查，同一块布的格子全收进集合，朝向再怎么变判定也不用改
* 单击只负责选中不负责旋转，旋转拆卖都得更正到操作条上按，误点一下不至于把地板抽走
* 拖动只跟着手指走，落点合不合规矩一律问宿主，摆不成靠重铺把布弹回原位
*/

using System.Collections.Generic;
using Framework.QTower.View;
using Module.Backpack.Model;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Utils.log;

namespace UI.Shop
{
    public sealed class UIShopPlateView : UIItemBase, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        private const string LABEL_NAME = "Txt_Label";
        private const string BLOCK_NAME = "Img_Block";
        private const string FACE_NAME = "Img_Face";

        // 外轮廓暗边厚度，只有整块布的最外圈用得上
        private const float RIM_THICKNESS = 4f;

        // 暗边相对布色压暗的比例，比装备那层再深一点，布读起来更像垫在底下的底布
        private const float RIM_DARKEN = 0.34f;

        // 选中时往布色里掺的白，只提亮不改色相，一眼认得出还是原来那块布
        private const float SELECT_WHITEN = 0.22f;

        // 落点换算用的最小平方距离，位移短到基本没挪就当成没动过
        private const float SNAP_EPSILON = 0.001f;

        // 名称字号，布压在装备底下，字比装备那层稍小一点免得抢视线
        private const float LABEL_FONT_SIZE = 22f;

        // ==================== 字段[私有] ====================
        private RectTransform m_rect;
        private RectTransform m_parentRect;
        private TMP_Text m_labelText;

        // 方块只创建不销毁，换形状换朝向时多出来的先藏起来
        private readonly List<RectTransform> m_blocks = new();
        private readonly List<Vector2Int> m_cellBuffer = new();
        private readonly HashSet<Vector2Int> m_worldCells = new();
        private int m_blockUsed;

        // 拖拽三件套：落差、锚点差、本轮是否拖过
        private BackpackItemInstance m_plate;
        private IShopBackpackHost m_host;
        private float m_pitch;
        private Vector2 m_grabOffset;
        private Vector2 m_startPosition;
        private int m_anchorDeltaColumn;
        private int m_anchorDeltaRow;
        private bool m_wasDragged;

        // ==================== Public Function ====================
        /// <summary>
        /// 按板块当前占用的格子铺一块布，颜色取这块布自己的底色，选中时提亮
        /// </summary>
        /// <param name="plate">要铺的板块实例</param>
        /// <param name="cellSize">单格边长，取自背包网格</param>
        /// <param name="cellSpacing">单格间距，取自背包网格</param>
        /// <param name="host">手势落点的处理方，传 null 则这块布不可交互</param>
        /// <param name="labelFont">中文字体，运行时创建的名称节点必须自己带一份</param>
        public void Setup(BackpackItemInstance plate, float cellSize, float cellSpacing,
            IShopBackpackHost host, TMP_FontAsset labelFont)
        {
            ensureBound();

            if (plate?.Definition == null)
            {
                QLog.Error("板块实例没有定义，无法铺布");
                return;
            }

            m_plate = plate;
            m_host = host;
            m_wasDragged = false;

            Color clothColor = plate.Definition.TintColor;

            if (m_host != null && m_host.IsPlateSelected(plate))
                clothColor = Color.Lerp(clothColor, Color.white, SELECT_WHITEN);

            Color rimColor = Color.Lerp(clothColor, Color.black, RIM_DARKEN);

            float pitch = cellSize + cellSpacing;
            m_pitch = pitch;

            plate.FillWorldCells(m_cellBuffer);
            m_worldCells.Clear();

            int minColumn = int.MaxValue;
            int minRow = int.MaxValue;
            int maxColumn = int.MinValue;
            int maxRow = int.MinValue;

            for (int i = 0; i < m_cellBuffer.Count; i++)
            {
                Vector2Int cell = m_cellBuffer[i];
                m_worldCells.Add(cell);
                minColumn = Mathf.Min(minColumn, cell.x);
                minRow = Mathf.Min(minRow, cell.y);
                maxColumn = Mathf.Max(maxColumn, cell.x);
                maxRow = Mathf.Max(maxRow, cell.y);
            }

            m_anchorDeltaColumn = minColumn - plate.AnchorColumn;
            m_anchorDeltaRow = minRow - plate.AnchorRow;

            m_rect.anchorMin = new Vector2(0f, 1f);
            m_rect.anchorMax = new Vector2(0f, 1f);
            m_rect.pivot = new Vector2(0f, 1f);
            m_rect.anchoredPosition = new Vector2(minColumn * pitch, -minRow * pitch);
            m_rect.sizeDelta = new Vector2((maxColumn - minColumn + 1) * pitch, (maxRow - minRow + 1) * pitch);

            m_blockUsed = 0;

            for (int i = 0; i < m_cellBuffer.Count; i++)
            {
                Vector2Int cell = m_cellBuffer[i];
                showBlock(cell.x - minColumn, cell.y - minRow, pitch, cell, clothColor, rimColor);
            }

            for (int i = m_blockUsed; i < m_blocks.Count; i++)
                m_blocks[i].gameObject.SetActive(false);

            ensureLabel(labelFont);

            m_labelText.text = plate.Definition.Label;
            m_labelText.color = UIShopQuality.LabelColor(clothColor);
            m_labelText.outlineColor = new Color32(0x1B, 0x12, 0x08, 0xB0);
            m_labelText.outlineWidth = 0.14f;
            m_labelText.transform.SetAsLastSibling();
        }

        // ==================== 拖拽与点击 ====================
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (m_host == null || m_plate == null)
                return;

            m_wasDragged = true;
            m_startPosition = m_rect.anchoredPosition;
            m_grabOffset = pointerInGrid(eventData) - m_startPosition;
            m_rect.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (m_host == null || m_plate == null)
                return;

            m_rect.anchoredPosition = pointerInGrid(eventData) - m_grabOffset;
        }

        // 松手即落点，四舍五入到最近的格，行主序与铺块时的算法完全一致
        public void OnEndDrag(PointerEventData eventData)
        {
            if (m_host == null || m_plate == null)
                return;

            Vector2 position = m_rect.anchoredPosition;

            if ((position - m_startPosition).sqrMagnitude > SNAP_EPSILON)
            {
                int column = Mathf.RoundToInt(position.x / m_pitch) - m_anchorDeltaColumn;
                int row = Mathf.RoundToInt(-position.y / m_pitch) - m_anchorDeltaRow;

                m_host.TryMovePlate(m_plate, column, row);
                return;
            }

            // 没挪动过就只是把手指抬起，重铺一次把布弹回原来的格子
            m_host.TryMovePlate(m_plate, m_plate.AnchorColumn, m_plate.AnchorRow);
        }

        // 布只认选中不认旋转，旋转拆卖一律走宿主浮出来的操作条
        public void OnPointerClick(PointerEventData eventData)
        {
            if (m_host == null || m_plate == null)
                return;

            if (m_wasDragged)
            {
                m_wasDragged = false;
                return;
            }

            m_host.SelectPlate(m_plate);
        }

        // ==================== Private Function ====================
        // 把屏幕坐标换算成「网格左上角为原点」的平面坐标，与铺块用的坐标系完全对齐
        private Vector2 pointerInGrid(PointerEventData eventData)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(m_parentRect, eventData.position,
                eventData.pressEventCamera, out local);

            return local - new Vector2(m_parentRect.rect.xMin, m_parentRect.rect.yMax);
        }

        // 一格布面：暗底铺满整块，布色只在四条外轮廓边上内缩，有邻居的那条边贴满不断缝
        private void showBlock(int column, int row, float pitch, Vector2Int worldCell, Color faceColor, Color rimColor)
        {
            RectTransform block = m_blockUsed < m_blocks.Count ? m_blocks[m_blockUsed] : createBlock();
            m_blockUsed++;

            block.gameObject.SetActive(true);
            block.anchoredPosition = new Vector2(column * pitch, -row * pitch);
            block.sizeDelta = new Vector2(pitch, pitch);
            block.GetComponent<Image>().color = rimColor;

            RectTransform face = (RectTransform)block.GetChild(0);

            bool hasLeft = m_worldCells.Contains(new Vector2Int(worldCell.x - 1, worldCell.y));
            bool hasRight = m_worldCells.Contains(new Vector2Int(worldCell.x + 1, worldCell.y));
            bool hasTop = m_worldCells.Contains(new Vector2Int(worldCell.x, worldCell.y - 1));
            bool hasBottom = m_worldCells.Contains(new Vector2Int(worldCell.x, worldCell.y + 1));

            face.offsetMin = new Vector2(hasLeft ? 0f : RIM_THICKNESS, hasBottom ? 0f : RIM_THICKNESS);
            face.offsetMax = new Vector2(hasRight ? 0f : -RIM_THICKNESS, hasTop ? 0f : -RIM_THICKNESS);
            face.GetComponent<Image>().color = faceColor;
        }

        // 一块形状格：暗底铺满整块不吃射线，布色面贴在其内并按需留边，只有布色面接指针
        private RectTransform createBlock()
        {
            GameObject blockObject = new GameObject(BLOCK_NAME, typeof(RectTransform), typeof(Image));
            RectTransform block = (RectTransform)blockObject.transform;
            block.SetParent(transform, false);
            block.anchorMin = new Vector2(0f, 1f);
            block.anchorMax = new Vector2(0f, 1f);
            block.pivot = new Vector2(0f, 1f);
            block.GetComponent<Image>().raycastTarget = false;

            GameObject faceObject = new GameObject(FACE_NAME, typeof(RectTransform), typeof(Image));
            RectTransform face = (RectTransform)faceObject.transform;
            face.SetParent(block, false);
            face.anchorMin = Vector2.zero;
            face.anchorMax = Vector2.one;
            face.pivot = new Vector2(0.5f, 0.5f);
            face.GetComponent<Image>().raycastTarget = true;

            m_blocks.Add(block);
            return block;
        }

        // 布是运行时建的，名称节点也得运行时建，字体由面板把预制体上那份抄过来
        private void ensureLabel(TMP_FontAsset font)
        {
            if (m_labelText != null)
                return;

            Transform label = FindNode(LABEL_NAME);

            if (label != null)
            {
                m_labelText = label.GetComponent<TMP_Text>();
                return;
            }

            GameObject labelObject = new GameObject(LABEL_NAME, typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform labelRect = (RectTransform)labelObject.transform;
            labelRect.SetParent(transform, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = LABEL_FONT_SIZE;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            m_labelText = text;
        }

        private void bindNodes()
        {
            m_rect = (RectTransform)transform;
            m_parentRect = m_rect.parent as RectTransform;
        }

        private void ensureBound()
        {
            if (m_rect == null)
                bindNodes();
        }
    }
}
