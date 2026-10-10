/*
* ┌──────────────────────────────────┐
* │  描    述: 背包里的一块布，逐格铺一层透明手势区，拖动时亮出跟手的幽灵布块
* │  类    名: UIShopPlateView.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 布是格子的唯一来源，画在装备底下那一层，所以本视图由面板运行时创建，不进预制体
* 静止时本视图只剩手势：布的样子就是底层格子染上这块布的底色，这里再盖一层纯属重复的占位色块
* 拖起来才亮出一套幽灵格，暗边加内缩亮面跟着手指走，玩家手里看见的就是那块布本身而不是别的东西
* 布名不写在这里，点一下交给面板的详情区报两行，常驻文字会和布上装备的字叠成一团
* 选中态也不在这里提亮，改由面板最上层给每一格套一圈选中外框，边界比往布色里掺白清楚得多
* 单击只负责选中不负责旋转，旋转拆卖都得更正到操作条上按，误点一下不至于把地板抽走
* 指针还在网格内就一格一格吸附着走，出了网格才恢复成跟着手指自由挪
* 落点合不合规矩一律问宿主，摆不成靠重铺把布弹回原位
* 松手落在暂存架那块面板上就是把布拆进架子，和按操作条上的「拆下」是同一件事
*/

using System.Collections.Generic;
using Framework.QTower.View;
using Module.Backpack.Model;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Utils.log;

namespace UI.Shop
{
    public sealed class UIShopPlateView : UIItemBase, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        private const string HIT_NAME = "Img_HitCell";
        private const string CLOTH_NAME = "Img_ClothCell";
        private const string CLOTH_FACE_NAME = "Img_ClothFace";

        // 落点换算用的最小平方距离，位移短到基本没挪就当成没动过
        private const float SNAP_EPSILON = 0.001f;

        // 幽灵格四周的暗边厚度，与装备块同一套数值，两层看着才是一路东西
        private const float RIM_THICKNESS = 4f;

        // 暗边相对布色压暗的比例
        private const float RIM_DARKEN = 0.28f;

        // ==================== 字段[私有] ====================
        private RectTransform m_rect;
        private RectTransform m_parentRect;

        // 透明手势区与幽灵格都只创建不销毁，换形状换朝向时多出来的先藏起来
        private readonly List<RectTransform> m_hitCells = new();
        private readonly List<RectTransform> m_clothCells = new();
        private readonly List<Vector2Int> m_cellBuffer = new();
        private int m_hitCellsUsed;
        private int m_clothCellsUsed;

        // 拖拽三件套：落差、锚点差、本轮是否拖过
        private BackpackItemInstance m_plate;
        private IShopBackpackHost m_host;
        private float m_pitch;
        private Vector2 m_grabOffset;
        private Vector2 m_startPosition;
        private int m_anchorDeltaColumn;
        private int m_anchorDeltaRow;
        private bool m_wasDragged;

        // 拖布期间整层布被提到最后，松手要按原序号放回去，幽灵格也只在这期间亮着
        private int m_clothLayerIndex;
        private bool m_isDragging;

        // ==================== 生命周期 ====================
        private void Awake()
        {
            bindNodes();
        }

        // 面板关掉、物体被隐藏时走不到 OnEndDrag，这里补一次收摊，免得幽灵格和空地板留在场上
        private void OnDisable()
        {
            endDrag();
        }

        // ==================== Public Function ====================
        /// <summary>
        /// 按板块当前占用的格子摆出透明手势区与幽灵格，幽灵格只在被拖着的时候亮
        /// </summary>
        /// <param name="plate">要铺的板块实例</param>
        /// <param name="cellSize">单格边长，取自背包网格</param>
        /// <param name="cellSpacing">单格间距，取自背包网格</param>
        /// <param name="host">手势落点的处理方，传 null 则这块布不可交互</param>
        /// <param name="cellSprite">底层格子的底图，幽灵格抄它才和染过色的格子一个样</param>
        public void Setup(BackpackItemInstance plate, float cellSize, float cellSpacing,
            IShopBackpackHost host, Sprite cellSprite)
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

            // 布色由底层格子去画，这里只拿它给幽灵格上同样的色，两处必须是一个色才认得出是同一块布
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
            m_clothCellsUsed = 0;

            for (int i = 0; i < m_cellBuffer.Count; i++)
            {
                Vector2Int cell = m_cellBuffer[i];
                int localColumn = cell.x - minColumn;
                int localRow = cell.y - minRow;

                showHitCell(localColumn, localRow, pitch);
                showClothCell(localColumn, localRow, pitch, clothColor, cellSprite);
            }

            for (int i = m_hitCellsUsed; i < m_hitCells.Count; i++)
                m_hitCells[i].gameObject.SetActive(false);

            for (int i = m_clothCellsUsed; i < m_clothCells.Count; i++)
                m_clothCells[i].gameObject.SetActive(false);

            // 重铺也可能发生在拖拽中途，摆完按「现在在不在手上」统一决定幽灵格亮不亮
            applyClothCellsVisible();
        }

        // ==================== 拖拽与点击 ====================
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (m_host == null || m_plate == null)
                return;

            m_wasDragged = true;
            m_isDragging = true;
            m_startPosition = m_rect.anchoredPosition;
            m_grabOffset = pointerInGrid(eventData) - m_startPosition;

            // 手里的布要压过装备与选中外框，整层提到最后；布层与物品层是同一套 rect，坐标换算不受影响
            m_clothLayerIndex = m_parentRect.GetSiblingIndex();
            m_parentRect.SetAsLastSibling();
            m_rect.SetAsLastSibling();

            applyClothCellsVisible();
            // 布已经到手上了，它铺出来的那几格连底板都空了，落点不成靠重铺弹回原位
            m_host.BeginPlateDrag(m_plate);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (m_host == null || m_plate == null)
                return;

            Vector2 free = pointerInGrid(eventData) - m_grabOffset;

            // 指针还在网格内就把包围盒左上角吸附到整格，抓着手感那点偏移照算，手指底下那一格不会跳走
            // 幽灵格是本 rect 的子节点，rect 吸附了它们自然跟着对齐，不必单独摆
            if (m_host.TryPointerCell(eventData.position, out _, out _))
                m_rect.anchoredPosition = new Vector2(Mathf.RoundToInt(free.x / m_pitch) * m_pitch,
                    Mathf.RoundToInt(free.y / m_pitch) * m_pitch);
            else
                m_rect.anchoredPosition = free;
        }

        // 松手即落点，四舍五入到最近的格，行主序与铺块时的算法完全一致
        // 松手在网格外时收得到 endDrag 却收不到 click，拖过的标记只能在这里清，否则下一次真点击会被吞掉
        public void OnEndDrag(PointerEventData eventData)
        {
            if (m_host == null || m_plate == null)
                return;

            Vector2 position = m_rect.anchoredPosition;
            int anchorColumn = m_plate.AnchorColumn;
            int anchorRow = m_plate.AnchorRow;

            if ((position - m_startPosition).sqrMagnitude > SNAP_EPSILON)
            {
                anchorColumn = Mathf.RoundToInt(position.x / m_pitch) - m_anchorDeltaColumn;
                anchorRow = Mathf.RoundToInt(-position.y / m_pitch) - m_anchorDeltaRow;
            }
            // 没挪动过就只是把手指抬起，落点回到原来的格子，重铺一次把布弹回去

            endDrag();

            // 松手在暂存架那块面板上就是拆下架，压在布上的装备由宿主一并挪进架子
            if (m_host.IsPointerInStash(eventData.position))
            {
                m_host.TryStashPlate(m_plate);
                return;
            }

            m_host.TryMovePlate(m_plate, anchorColumn, anchorRow);
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

        // 收摊顺序不能颠倒：先解除手持再让宿主重铺，否则重铺时这几格还按「布不在家」染
        private void endDrag()
        {
            if (!m_isDragging)
                return;

            m_isDragging = false;
            m_wasDragged = false;
            applyClothCellsVisible();

            if (m_parentRect != null)
                m_parentRect.SetSiblingIndex(m_clothLayerIndex);

            if (m_host != null && m_plate != null)
                m_host.EndPlateDrag(m_plate);
        }

        // 幽灵格只在手里拖着这块布时亮着，其余时候整批收掉
        private void applyClothCellsVisible()
        {
            for (int i = 0; i < m_clothCellsUsed; i++)
                m_clothCells[i].gameObject.SetActive(m_isDragging);
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

        // 一格幽灵布：暗边铺满整格，亮面内缩一圈，底图与底层格子同一张，颜色与布色同一个
        private void showClothCell(int column, int row, float pitch, Color clothColor, Sprite cellSprite)
        {
            RectTransform cell = m_clothCellsUsed < m_clothCells.Count ? m_clothCells[m_clothCellsUsed] : createClothCell();
            m_clothCellsUsed++;

            cell.anchoredPosition = new Vector2(column * pitch, -row * pitch);
            cell.sizeDelta = new Vector2(pitch, pitch);

            Image rim = cell.GetComponent<Image>();
            rim.sprite = cellSprite;
            rim.color = Color.Lerp(clothColor, Color.black, RIM_DARKEN);

            RectTransform face = (RectTransform)cell.GetChild(0);
            face.sizeDelta = new Vector2(-RIM_THICKNESS * 2f, -RIM_THICKNESS * 2f);

            Image faceImage = face.GetComponent<Image>();
            faceImage.sprite = cellSprite;
            faceImage.color = clothColor;
        }

        // 幽灵格不吃射线，拖拽的落点仍然由同一格上的透明手势区接，两套区域重叠也不抢事件
        private RectTransform createClothCell()
        {
            GameObject cellObject = new GameObject(CLOTH_NAME, typeof(RectTransform), typeof(Image));
            RectTransform cell = (RectTransform)cellObject.transform;
            cell.SetParent(transform, false);
            cell.anchorMin = new Vector2(0f, 1f);
            cell.anchorMax = new Vector2(0f, 1f);
            cell.pivot = new Vector2(0f, 1f);
            cell.GetComponent<Image>().raycastTarget = false;

            GameObject faceObject = new GameObject(CLOTH_FACE_NAME, typeof(RectTransform), typeof(Image));
            RectTransform face = (RectTransform)faceObject.transform;
            face.SetParent(cell, false);
            face.anchorMin = Vector2.zero;
            face.anchorMax = Vector2.one;
            face.pivot = new Vector2(0.5f, 0.5f);
            face.sizeDelta = new Vector2(-RIM_THICKNESS * 2f, -RIM_THICKNESS * 2f);
            face.GetComponent<Image>().raycastTarget = false;

            m_clothCells.Add(cell);
            return cell;
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
