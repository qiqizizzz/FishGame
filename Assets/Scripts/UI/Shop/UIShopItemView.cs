/*
* ┌──────────────────────────────────┐
* │  描    述: 背包格里的一件存放物视图，按形状染色铺块，并把手势翻成旋转与挪位
* │  类    名: UIShopItemView.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 方块四周留暗边、亮面内缩，相邻方块之间的凹槽由两侧暗边拼出来，形状轮廓一眼可辨
* 拖动时视图只跟着手指走，落点合不合规矩一律问模型，摆不成靠宿主重铺把物品弹回原位
* 单击只报「选中」，名字与加成由面板的详情区去报，格子上不再常驻文字
* 旋转、取下、出售都归面板浮出来的操作条，本视图不直接改物品，误点一下也不至于把东西转了向
* 松手落在暂存架那块面板上就是收进架子，落回网格就是挪位；本轮真的拖过就不再触发点击
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
    public sealed class UIShopItemView : UIItemBase, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        private const string BLOCK_NAME = "Img_Block";
        private const string FACE_NAME = "Img_Face";

        // 方块四周留出的暗边厚度，相邻方块之间的凹槽是它的两倍宽
        private const float RIM_THICKNESS = 4f;

        // 暗边相对底色压暗的比例
        private const float RIM_DARKEN = 0.28f;

        // 落点换算用的最小平方距离，位移短到基本没挪就当成没动过
        private const float SNAP_EPSILON = 0.001f;

        // ==================== 字段[私有] ====================
        private RectTransform m_rect;
        private RectTransform m_parentRect;

        // 方块只创建不销毁，换形状时多出来的先藏起来
        private readonly List<RectTransform> m_blocks = new();
        private readonly List<Vector2Int> m_cellBuffer = new();
        private int m_blockUsed;

        // 拖拽三件套：落差、锚点差、本轮是否拖过
        private BackpackItemInstance m_item;
        private IShopBackpackHost m_host;
        private float m_pitch;
        private Vector2 m_grabOffset;
        private Vector2 m_startPosition;
        private int m_anchorDeltaColumn;
        private int m_anchorDeltaRow;
        private bool m_wasDragged;

        // ==================== 生命周期 ====================
        private void Awake()
        {
            bindNodes();
        }

        // ==================== Public Function ====================
        /// <summary>
        /// 按实例当前占用的格子铺一件存放物，颜色走阵营纸色或档位色
        /// </summary>
        /// <param name="item">要铺的存放物实例</param>
        /// <param name="cellSize">单格边长，取自背包网格</param>
        /// <param name="cellSpacing">单格间距，取自背包网格</param>
        /// <param name="host">手势落点的处理方，传 null 则这块物品不可交互</param>
        public void Setup(BackpackItemInstance item, float cellSize, float cellSpacing, IShopBackpackHost host)
        {
            ensureBound();

            if (item?.Definition == null)
            {
                QLog.Error("存放物实例没有定义，无法铺块");
                return;
            }

            m_item = item;
            m_host = host;
            m_wasDragged = false;

            Color goodsColor = UIShopQuality.ColorFor(item.Definition, item.Tier);
            float pitch = cellSize + cellSpacing;
            m_pitch = pitch;

            item.FillWorldCells(m_cellBuffer);

            int minColumn = int.MaxValue;
            int minRow = int.MaxValue;
            int maxColumn = int.MinValue;
            int maxRow = int.MinValue;

            for (int i = 0; i < m_cellBuffer.Count; i++)
            {
                minColumn = Mathf.Min(minColumn, m_cellBuffer[i].x);
                minRow = Mathf.Min(minRow, m_cellBuffer[i].y);
                maxColumn = Mathf.Max(maxColumn, m_cellBuffer[i].x);
                maxRow = Mathf.Max(maxRow, m_cellBuffer[i].y);
            }

            m_anchorDeltaColumn = minColumn - item.AnchorColumn;
            m_anchorDeltaRow = minRow - item.AnchorRow;

            m_rect.anchorMin = new Vector2(0f, 1f);
            m_rect.anchorMax = new Vector2(0f, 1f);
            m_rect.pivot = new Vector2(0f, 1f);
            m_rect.anchoredPosition = new Vector2(minColumn * pitch, -minRow * pitch);

            m_blockUsed = 0;

            for (int i = 0; i < m_cellBuffer.Count; i++)
                showBlock(m_cellBuffer[i].x - minColumn, m_cellBuffer[i].y - minRow, pitch, goodsColor);

            for (int i = m_blockUsed; i < m_blocks.Count; i++)
                m_blocks[i].gameObject.SetActive(false);

            m_rect.sizeDelta = new Vector2((maxColumn - minColumn + 1) * pitch, (maxRow - minRow + 1) * pitch);

        }

        // ==================== 拖拽与点击 ====================
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (m_host == null || m_item == null)
                return;

            m_wasDragged = true;
            m_startPosition = m_rect.anchoredPosition;
            m_grabOffset = pointerInGrid(eventData) - m_startPosition;
            m_rect.SetAsLastSibling();
            // 东西离手了，压在它上面的操作条先收起来，免得拖的时候满屏控件
            m_host.BeginItemDrag(m_item);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (m_host == null || m_item == null)
                return;

            m_rect.anchoredPosition = pointerInGrid(eventData) - m_grabOffset;
        }

        // 松手即落点，四舍五入到最近的格，行主序与铺块时的算法完全一致
        public void OnEndDrag(PointerEventData eventData)
        {
            if (m_host == null || m_item == null)
                return;

            Vector2 position = m_rect.anchoredPosition;

            // 松手在暂存架那块面板上就是下架，摘进架子之后不用再算网格落点
            if (m_host.IsPointerInStash(eventData.position))
                m_host.TryStashItem(m_item);
            else if ((position - m_startPosition).sqrMagnitude > SNAP_EPSILON)
            {
                int column = Mathf.RoundToInt(position.x / m_pitch) - m_anchorDeltaColumn;
                int row = Mathf.RoundToInt(-position.y / m_pitch) - m_anchorDeltaRow;

                m_host.TryMove(m_item, column, row);
            }
            // 没挪动过就只是把手指抬起，重铺一次把物品弹回原来的格子
            else
                m_host.TryMove(m_item, m_item.AnchorColumn, m_item.AnchorRow);

            // 落点定完了才补回操作条，它按新位置浮出来才对得上手上的东西
            m_host.EndItemDrag(m_item);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (m_host == null || m_item == null)
                return;

            if (m_wasDragged)
            {
                m_wasDragged = false;
                return;
            }

            // 单击只管选中与详情，旋转交给操作条，免得误点把东西转了向
            m_host.SelectItem(m_item);
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


        private void showBlock(int column, int row, float pitch, Color goodsColor)
        {
            RectTransform block = m_blockUsed < m_blocks.Count ? m_blocks[m_blockUsed] : createBlock();
            m_blockUsed++;

            block.gameObject.SetActive(true);
            block.anchoredPosition = new Vector2(column * pitch, -row * pitch);
            block.sizeDelta = new Vector2(pitch, pitch);
            block.GetComponent<Image>().color = Color.Lerp(goodsColor, Color.black, RIM_DARKEN);

            RectTransform face = (RectTransform)block.GetChild(0);
            face.sizeDelta = new Vector2(-RIM_THICKNESS * 2f, -RIM_THICKNESS * 2f);
            face.GetComponent<Image>().color = goodsColor;
        }

        // 一块形状格：暗边铺满整块，亮面四周内缩，方块之间的凹槽由两侧暗边拼出来
        // 亮面要吃点击与拖拽，不然整件物品没有任何一处能接住指针
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
            face.sizeDelta = new Vector2(-RIM_THICKNESS * 2f, -RIM_THICKNESS * 2f);
            face.GetComponent<Image>().raycastTarget = true;

            m_blocks.Add(block);
            return block;
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
