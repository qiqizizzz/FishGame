/*
* ┌──────────────────────────────────┐
* │  描    述: 面板中的单个格子，负责空格、布底、锁定与仓库暂存四种外观
* │  类    名: UIShopSlotItem.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 位置与尺寸交给 GridLayoutGroup，脚本只切底图与颜色
* 背包格铺的是布，布的底色就是这格的底色，压在布上的装备由 UIShopItemView 单独画一层
* 仓库格自己就是物品，有图标就贴图、没图标才整格染品质色并标出名称，点一下自动取回，也可以直接拖着摆上网格
* 拖出架子时摆一套幽灵跟着手指：有图标就一路亮着图标，格子垫在底下逐格染色，盖得住的压深、缺格子的染红
* 指针还没进网格就只留图标跟着手，落点摆不成原样留在架上
* 架子上的布照它真实的异形逐格画出来，架上看几格回家就铺几格，整格染色会把二格和六格读成一样大
* 只有装了东西的仓库格才吃射线，空格与锁格把射线让出去，免得挡住网格上的拖拽
*/

using Framework.QTower.View;
using Module.Backpack.Data;
using Module.Backpack.Model;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Utils.log;

namespace UI.Shop
{
    public sealed class UIShopSlotItem : UIItemBase, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerClickHandler
    {
        // 仓库格用来标注物品名称与图标，背包格天生没有这两个子节点
        private const string LABEL_PATH = "Txt_Label";
        private const string ICON_PATH = "Img_Icon";
        private const string CLOTH_SHAPE_PATH = "Node_ClothShape";

        // ==================== 字段[外部设置] ====================
        [Tooltip("空格子的底图，锁定格与仓库格是纯色块，不用它")]
        [SerializeField] private Sprite CellSprite;

        // ==================== 属性 ====================
        /// <summary>空格底图，拖布时的幽灵格要抄同一张脸，两处才认得出是同一块布</summary>
        public Sprite EmptyCellSprite => CellSprite;

        [Tooltip("空格子的颜色，靠透明度压成位置提示，别调到不透明")]
        [SerializeField] private Color EmptyColor = new Color32(0xFF, 0xFF, 0xFF, 0x3C);

        [Tooltip("锁定格的颜色，同样只留一点压暗的凹陷感，别盖过真正的装备块")]
        [SerializeField] private Color LockedColor = new Color32(0x6E, 0x5F, 0x45, 0x3A);

        // ==================== 字段[私有] ====================
        private Image m_bgImage;
        private Image m_iconImage;
        private TMP_Text m_labelText;
        private UIShopClothShape m_clothShape;

        // 装了东西才有的取回入口，空格与锁格清空它，点击就不会误触
        private IShopBackpackHost m_host;
        private int m_stashIndex = -1;

        // 拖出架子要按这件的形状摆幽灵，落点摆不成也靠它弹回原格
        private BackpackItemInstance m_item;
        private RectTransform m_ghost;
        private Color m_qualityColor;
        private bool m_wasDragged;

        // ==================== 生命周期 ====================
        private void Awake()
        {
            bindNodes();
        }

        // 面板关掉时走不到 OnEndDrag，这里补一次收摊，免得幽灵格留在场上
        private void OnDisable()
        {
            UIShopShapeGhost.Recycle(m_ghost);
            m_ghost = null;
            m_wasDragged = false;
        }

        // ==================== Public Function ====================
        /// <summary>
        /// 清成一个可放置的空格
        /// </summary>
        public void SetEmpty()
        {
            ensureBound();

            // 空格只是提示这里能放，压成半透明才不像一块真的占了位的装备
            m_bgImage.sprite = CellSprite;
            m_bgImage.color = EmptyColor;
            m_bgImage.raycastTarget = false;
            clearRecall();
            hideLabel();
            hideIcon();
            hideClothShape();
        }

        /// <summary>
        /// 铺上一块布，整格染成这块布的底色，格子从锁格变成能用
        /// </summary>
        /// <param name="clothColor">布的底色，来自布料总表</param>
        public void SetCloth(Color clothColor)
        {
            ensureBound();

            m_bgImage.sprite = CellSprite;
            m_bgImage.color = clothColor;
            m_bgImage.raycastTarget = false;
            clearRecall();
            hideLabel();
            hideIcon();
            hideClothShape();
        }

        /// <summary>
        /// 变成一块锁定的暗色格子，还没被布铺到
        /// </summary>
        public void SetLocked()
        {
            ensureBound();

            m_bgImage.sprite = null;
            m_bgImage.color = LockedColor;
            m_bgImage.raycastTarget = false;
            clearRecall();
            hideLabel();
            hideIcon();
            hideClothShape();
        }

        /// <summary>
        /// 仓库格放入一件物品，装备整格染成品质色，布照它此刻的朝向逐格画出异形，并接上点一下取回与拖上网格两个入口
        /// </summary>
        /// <param name="item">架上的这件物品</param>
        /// <param name="stashIndex">在暂存架里的下标</param>
        /// <param name="host">落点与取回都由它处理</param>
        public void SetStored(BackpackItemInstance item, int stashIndex, IShopBackpackHost host)
        {
            ensureBound();

            Color qualityColor = UIShopQuality.ColorFor(item.Definition, item.Tier);
            bool isCloth = item.Definition.Kind == BackpackDefKind.Cloth;

            // 布染满整格只剩一块方块，占几格什么形全读不出来，改画异形，底格退回空格那点淡底
            m_bgImage.sprite = isCloth ? CellSprite : null;
            m_bgImage.color = isCloth ? EmptyColor : qualityColor;
            m_bgImage.raycastTarget = true;

            if (m_clothShape != null)
            {
                m_clothShape.SetVisible(isCloth);
                if (isCloth)
                    m_clothShape.Setup(item.ShapeDef, qualityColor, CellSprite, item.OrientationIndex);
            }

            m_host = host;
            m_stashIndex = stashIndex;
            m_item = item;
            m_qualityColor = qualityColor;
            m_wasDragged = false;

            // 有图标就贴图藏字，没图标的布才靠名称认物品
            Sprite icon = item.Definition.Icon;
            if (m_iconImage != null)
            {
                m_iconImage.gameObject.SetActive(icon != null);
                m_iconImage.sprite = icon;
            }

            if (m_labelText != null)
            {
                bool showLabel = icon == null;
                m_labelText.gameObject.SetActive(showLabel);
                if (showLabel)
                {
                    m_labelText.text = UIShopGoodsName.Of(item.Definition, item.Tier);
                    m_labelText.color = UIShopQuality.LabelColor(qualityColor);
                }
            }
        }

        // ==================== 拖拽与点击 ====================
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (m_host == null || m_item == null)
                return;

            m_wasDragged = true;
            m_ghost = UIShopShapeGhost.Build(m_item, m_qualityColor, m_host.CellPitch,
                m_host.GhostCellSprite, m_host.GhostLayer);
            moveGhost(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            moveGhost(eventData.position);
        }

        // 松手按幽灵吸附的那格摆放，指到网格外就什么都不做，东西留在架上
        // 落在网格外时收得到 endDrag 却收不到 click，拖过的标记只能在这里清，否则下一次真点击会被吞掉
        public void OnEndDrag(PointerEventData eventData)
        {
            if (m_host == null || m_item == null)
                return;

            Vector2 screenPoint = eventData.position;
            UIShopShapeGhost.Recycle(m_ghost);
            m_ghost = null;
            m_wasDragged = false;

            if (!m_host.TryPointerCell(screenPoint, out int anchorColumn, out int anchorRow))
                return;

            m_host.TryPlaceFromStashAt(m_stashIndex, anchorColumn, anchorRow);
        }

        /// <summary>
        /// 点一下把这件从暂存架取回上网格，落点由模型自己找
        /// </summary>
        /// <param name="eventData">点击事件数据</param>
        public void OnPointerClick(PointerEventData eventData)
        {
            // 拖过就只是松手，点击紧跟其后触发，不能当成「按一下取回」
            if (m_wasDragged)
            {
                m_wasDragged = false;
                return;
            }

            if (m_host == null || m_stashIndex < 0)
                return;

            m_host.TryRecallFromStash(m_stashIndex);
        }

        // ==================== Private Function ====================
        private void bindNodes()
        {
            m_bgImage = GetComponent<Image>();

            Transform label = FindNode(LABEL_PATH);
            m_labelText = label != null ? label.GetComponent<TMP_Text>() : null;

            // 异形预览排在仓库格的名字上方，背包格铺的本来就是布，两个可选节点缺了都不算配置错误
            Transform clothShape = FindNode(CLOTH_SHAPE_PATH);
            m_clothShape = clothShape != null ? clothShape.GetComponent<UIShopClothShape>() : null;

            // 图标节点只有仓库格有，缺了就只走染色与名称，不算配置错误
            Transform icon = FindNode(ICON_PATH);
            m_iconImage = icon != null ? icon.GetComponent<Image>() : null;

#if UNITY_EDITOR
            if (m_bgImage == null)
                QLog.Error($"格子上未挂载 Image：{gameObject.name}");
            if (CellSprite == null)
                QLog.Error($"空格子底图未指定：{gameObject.name}");
#endif
        }

        private void clearRecall()
        {
            m_host = null;
            m_stashIndex = -1;
            m_item = null;
            m_qualityColor = default;
        }

        // 幽灵整块吸附到指针所在的那一格，顺带逐格问宿主放不放得下；指针还在网格外就只留图标跟着手
        private void moveGhost(Vector2 screenPoint)
        {
            if (m_ghost == null || m_host == null)
                return;

            if (m_host.TryPointerCell(screenPoint, out int anchorColumn, out int anchorRow))
            {
                UIShopShapeGhost.PlaceAt(m_ghost, m_item, anchorColumn, anchorRow, m_host.CellPitch, m_host);
                return;
            }

            UIShopShapeGhost.FollowPointer(m_ghost, m_item, pointerInGrid(screenPoint), m_host.CellPitch);
        }

        // 把屏幕坐标换算成「网格左上角为原点」的平面坐标，与幽灵格用的是同一套算法
        private Vector2 pointerInGrid(Vector2 screenPoint)
        {
            RectTransform layer = m_host.GhostLayer;
            if (layer == null)
                return Vector2.zero;

            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screenPoint, null, out local);

            return local - new Vector2(layer.rect.xMin, layer.rect.yMax);
        }

        private void hideLabel()
        {
            if (m_labelText != null)
                m_labelText.gameObject.SetActive(false);
        }

        private void hideClothShape()
        {
            if (m_clothShape != null)
                m_clothShape.SetVisible(false);
        }

        private void hideIcon()
        {
            if (m_iconImage != null)
                m_iconImage.gameObject.SetActive(false);
        }

        // 模板复制出来的实例可能还没走 Awake，兜一次绑引用
        private void ensureBound()
        {
            if (m_bgImage == null)
                bindNodes();
        }
    }
}
