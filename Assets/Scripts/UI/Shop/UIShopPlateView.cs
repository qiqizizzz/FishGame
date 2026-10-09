/*
* ┌──────────────────────────────────┐
* │  描    述: 背包里的一块布，逐格铺一层透明手势区，可拖动可选中，布色由底层格子去画
* │  类    名: UIShopPlateView.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 布是格子的唯一来源，画在装备底下那一层，所以本视图由面板运行时创建，不进预制体
* 布面不再自己画：底层格子本来就按这块布的底色染过一遍，再盖一层纯属重复的占位色块
* 本视图只剩手势，形状占哪几格就在哪几格摆一块透明区，拖与点都只认这几格
* 选中态也不在这里提亮，改由面板最上层给每一格套一圈选中外框，边界比往布色里掺白清楚得多
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
        private const string HIT_NAME = "Img_HitCell";

        // 落点换算用的最小平方距离，位移短到基本没挪就当成没动过
        private const float SNAP_EPSILON = 0.001f;

        // 名称字号，布压在装备底下，字比装备那层稍小一点免得抢视线
        private const float LABEL_FONT_SIZE = 22f;

        // ==================== 字段[私有] ====================
        private RectTransform m_rect;
        private RectTransform m_parentRect;
        private TMP_Text m_labelText;

        // 透明手势区只创建不销毁，换形状换朝向时多出来的先藏起来
        private readonly List<RectTransform> m_hitCells = new();
        private readonly List<Vector2Int> m_cellBuffer = new();
        private int m_hitCellsUsed;

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
        /// 按板块当前占用的格子摆出透明手势区，顺带写上这块布的名字
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

            // 布色在这里只用来给名称挑字色，格子那层由面板按同一份底色染过，不必再画一遍
            Color clothColor = plate.Definition.TintColor;

            float pitch = cellSize + cellSpacing;
            m_pitch = pitch;

            plate.FillWorldCells(m_cellBuffer);

            int minColumn = int.MaxValue;
            int minRow = int.MaxValue;
            int maxColumn = int.MinValue;
            int maxRow = int.MinValue;

            for (int i = 0; i < m_cellBuffer.Count; i++)
            {
                Vector2Int cell = m_cellBuffer[i];
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

            m_hitCellsUsed = 0;

            for (int i = 0; i < m_cellBuffer.Count; i++)
            {
                Vector2Int cell = m_cellBuffer[i];
                showHitCell(cell.x - minColumn, cell.y - minRow, pitch);
            }

            for (int i = m_hitCellsUsed; i < m_hitCells.Count; i++)
                m_hitCells[i].gameObject.SetActive(false);

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

        // 一格手势区：透明但吃射线，形状占到的每一格都摆一块，拖与点只认这几格
        private void showHitCell(int column, int row, float pitch)
        {
            RectTransform hitCell = m_hitCellsUsed < m_hitCells.Count ? m_hitCells[m_hitCellsUsed] : createHitCell();
            m_hitCellsUsed++;

            hitCell.gameObject.SetActive(true);
            hitCell.anchoredPosition = new Vector2(column * pitch, -row * pitch);
            hitCell.sizeDelta = new Vector2(pitch, pitch);
        }

        // 透明块按整格加间距铺，相邻两格的手势区接得上，拖到缝上也不脱手
        private RectTransform createHitCell()
        {
            GameObject hitObject = new GameObject(HIT_NAME, typeof(RectTransform), typeof(Image));
            RectTransform hitCell = (RectTransform)hitObject.transform;
            hitCell.SetParent(transform, false);
            hitCell.anchorMin = new Vector2(0f, 1f);
            hitCell.anchorMax = new Vector2(0f, 1f);
            hitCell.pivot = new Vector2(0f, 1f);

            Image image = hitCell.GetComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;

            m_hitCells.Add(hitCell);
            return hitCell;
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
