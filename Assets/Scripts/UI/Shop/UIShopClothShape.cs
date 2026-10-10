/*
* ┌──────────────────────────────────┐
* │  描    述: 商品卡上的布形预览，照这块布真实的异形逐格画出占几格、什么颜色、朝哪个方向
* │  类    名: UIShopClothShape.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 布在货架上原本只染一个方块，二格和六格看着一样大，买回家铺开才发现占的是哪几格
* 这里逐格复制格子底图，颜色与背包静止态的布完全同源，卡上数得几格回去就铺几格
* 整面货架共用同一个单格尺寸，格数多的预览天生更大，这份大小对比才是给玩家看的
* 朝向取买入手时的那个，也就是形状资产的基准朝向，所见即所得，卡上不会摆出买回去摆不出的形
* 格子只创建不销毁，卡片在复用池里反复换货，遇到更大的形状再补几个，换小的只是藏起来
* 每一格都不吃射线，点击仍然归卡片上的按钮
*/

using System.Collections.Generic;
using Framework.QTower.View;
using Module.Backpack.Data;
using Module.Backpack.Model;
using UnityEngine;
using UnityEngine.UI;
using Utils.log;

namespace UI.Shop
{
    public sealed class UIShopClothShape : UIItemBase
    {
        private const string CELL_NAME = "Img_ClothCell";

        // ==================== 字段[外部设置] ====================
        [Tooltip("单格边长，整面货架共用这个尺寸，格数多的预览就更大")]
        [SerializeField, Min(2f)] private float CellSize = 15f;

        [Tooltip("格与格的间距，背包网格是 4，这里按卡面缩到 1")]
        [SerializeField, Min(0f)] private float CellSpacing = 1f;

        [Tooltip("单格模板，自己保持关闭，只用来逐格复制，底图与背包格子同一张\n锚点取预览带顶边中线、轴心取左上角，代码按包围盒左上角逐格偏移，改了锚点预览会跑偏")]
        [SerializeField] private GameObject CellTemplate;

        // ==================== 字段[私有] ====================
        private RectTransform m_rect;

        // 卡片反复换货，格子只增不减，用不完的藏起来等下一块更大的布
        private readonly List<RectTransform> m_cells = new();
        private int m_cellsUsed;

        // ==================== 生命周期 ====================
        private void Awake()
        {
            bindNodes();
        }

        // ==================== Public Function ====================
        /// <summary>
        /// 按形状模板的基准朝向画出这块布占的格子
        /// </summary>
        /// <param name="template">形状模板，异形与朝向都在里面</param>
        /// <param name="cellColor">每一格的底色，布自己的颜色，已售出时是压暗色</param>
        /// <param name="cellSprite">格子底图，传 null 就沿用模板自带的那张</param>
        public void Setup(GridShapeSO template, Color cellColor, Sprite cellSprite)
        {
            Setup(template, cellColor, cellSprite, 0);
        }

        /// <summary>
        /// 按形状模板的指定朝向画出这块布占的格子，暂存架里的布要照它此刻的朝向画
        /// </summary>
        /// <param name="template">形状模板，异形与朝向都在里面</param>
        /// <param name="cellColor">每一格的底色，布自己的颜色，已售出时是压暗色</param>
        /// <param name="cellSprite">格子底图，传 null 就沿用模板自带的那张</param>
        /// <param name="orientationIndex">朝向下标，越界由形状资产自己夹住</param>
        public void Setup(GridShapeSO template, Color cellColor, Sprite cellSprite, int orientationIndex)
        {
            ensureBound();

            if (template == null)
            {
                QLog.Error("布形预览没拿到形状模板，卡面画不出异形");
                Clear();
                return;
            }

            drawShape(template.OrientationAt(orientationIndex), cellColor, cellSprite);
        }

        /// <summary>
        /// 收起全部格子，卡片换成装备图标时用
        /// </summary>
        public void Clear()
        {
            ensureBound();

            for (int i = 0; i < m_cells.Count; i++)
                m_cells[i].gameObject.SetActive(false);

            m_cellsUsed = 0;
        }

        // ==================== Private Function ====================
        // 逐格摆出形状，包围盒在预览带里上下左右都居中
        private void drawShape(ShapeMask shape, Color cellColor, Sprite cellSprite)
        {
            float pitch = CellSize + CellSpacing;
            float bboxWidth = shape.Width * pitch - CellSpacing;
            float bboxHeight = shape.Height * pitch - CellSpacing;

            // 轴心在带子顶边正中，x 以中线对称，y 从顶边往下量
            float left = -bboxWidth * 0.5f;
            float top = (m_rect.rect.height - bboxHeight) * 0.5f;

            m_cellsUsed = 0;

            foreach (Vector2Int cell in shape.Cells())
            {
                RectTransform cellRect = acquireCell();
                cellRect.anchoredPosition = new Vector2(left + cell.x * pitch, -(top + cell.y * pitch));
                cellRect.sizeDelta = new Vector2(CellSize, CellSize);
                cellRect.gameObject.SetActive(true);

                Image cellImage = cellRect.GetComponent<Image>();
                if (cellSprite != null)
                    cellImage.sprite = cellSprite;
                cellImage.color = cellColor;

                m_cellsUsed++;
            }

            for (int i = m_cellsUsed; i < m_cells.Count; i++)
                m_cells[i].gameObject.SetActive(false);
        }

        // 取一个可用的格子，复用池见底就从模板补一个，卡片换货不重复创建对象
        private RectTransform acquireCell()
        {
            if (m_cellsUsed < m_cells.Count)
                return m_cells[m_cellsUsed];

            GameObject clone = Instantiate(CellTemplate, m_rect);
            clone.name = CELL_NAME;

            RectTransform cloneRect = clone.GetComponent<RectTransform>();
            m_cells.Add(cloneRect);
            return cloneRect;
        }

        private void bindNodes()
        {
            m_rect = transform as RectTransform;

#if UNITY_EDITOR
            if (m_rect == null)
                QLog.Error($"布形预览所在节点没有 RectTransform：{gameObject.name}");
            if (CellTemplate == null)
                QLog.Error($"单格模板未指定，卡面画不出异形：{gameObject.name}");
            else if (CellTemplate.GetComponent<Image>() == null)
                QLog.Error($"单格模板上没有 Image，画不出格子：{CellTemplate.name}");
#endif
        }

        // 卡片是复制模板出来的，可能还没走过 Awake，兜一次绑引用
        private void ensureBound()
        {
            if (m_rect == null)
                bindNodes();
        }
    }
}
