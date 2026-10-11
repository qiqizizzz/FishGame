/*
* ┌──────────────────────────────────┐
* │  描    述: 商店货架上的一张商品卡，画图标或异形预览与售价，点开看详情，拖进网格或暂存架才算买成
* │  类    名: UIShopGoodsItem.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 卡面只照条目本来的样子画，扣钱与入手全部交给宿主面板，卡片自己不改任何状态
* 卡上不留详细信息行，预览加价钱就够，名字与加成那点字都要到点开详情才写得下
* 买不起只把价钱染红，卡片照样能点能拖，拖起来整块染红、松手也成交不了
* 卡面上半格两类商品各画各的：装备贴图标，布照真实异形逐格画出占几格、什么颜色
* 布不吃图标那条链路，它的形状与颜色本来就是它的「图标」，画成方块等于没画
* 拖商品与拖暂存架里的东西共用同一套幽灵：吸附到落点格逐格染色，缺格染红，出了网格只留图标跟着手
* 松手压在网格上就按那一格入手，落在暂存架面板上就直接进架，两处都没挨上等于没买，钱一分不动
* 点一下只报详情不扣钱，真拖过之后紧跟的那次点击按拖过标记挡掉，免得拖完又弹出小窗
* 已售出的卡不再露出商品本来的样子，预览区整块换成一枚禁止图标，卖完的东西没必要再端详
*/

using Framework.QTower.View;
using Module.Backpack.Data;
using Module.Backpack.Model;
using Module.Shop.Model;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Utils.log;

namespace UI.Shop
{
    public sealed class UIShopGoodsItem : UIItemBase, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const string ICON_PATH = "Img_Icon";
        private const string PRICE_PATH = "Txt_Price";

        // 布形预览带，与 Img_Icon 占同一块地方，两类商品在这块地方互斥
        private const string CLOTH_SHAPE_PATH = "Node_ClothShape";

        // 已售出占位图，同样占在预览区那块地方，与上面两类互斥
        private const string SOLD_OUT_PATH = "Img_SoldOut";

        // ==================== 字段[私有] ====================
        private Image m_iconImage;
        private UIShopClothShape m_clothShape;

        // 卖完就把预览区让给它，卡上只剩价钱那一行写着「已售出」
        private Image m_soldOutImage;

        // 卡片从复用池里拉出来反复填商品，换货时要能还原预制体自带的底图
        private Sprite m_defaultIconSprite;
        private bool m_iconDefaultCached;

        private TMP_Text m_priceText;
        private Button m_button;

        // 价钱原本的颜色从预制体上读一次，染回时不写死，调色板改了不用改代码
        private Color m_priceDefaultColor;

        // 面板是宿主：详情、提层与成交都由它做，卡片只把手势翻译过去，自己不动任何状态
        private IShopBackpackHost m_host;
        private ShopStockEntry m_entry;

        // 幽灵底色抄卡面主色，布走布色装备走品质色，拖起来还认得出是同一件
        private Color m_ghostColor;
        private BackpackItemInstance m_dragInstance;
        private RectTransform m_ghost;

        // 松手那一发点击只是拖完的余波，不能当「按一下看详情」；拖过与否只能自己记
        private bool m_wasDragged;

        // 兜底重绑引用时不能把点击回调再挂一遍
        private bool m_listenerBound;

        // ==================== 生命周期 ====================
        private void Awake()
        {
            bindNodes();
        }

        // 面板关掉或卡片被换成别的商品时走不到 OnEndDrag，这里补一次收摊，免得幽灵格留在场上
        private void OnDisable()
        {
            UIShopShapeGhost.Recycle(m_ghost);
            m_ghost = null;
            m_dragInstance = null;
            m_wasDragged = false;
        }

        // ==================== Public Function ====================
        /// <summary>
        /// 填入一件商品，卡上只留预览与价钱
        /// </summary>
        /// <param name="entry">货架条目，款式档位售价都在里面</param>
        /// <param name="goodsColor">卡面主色，布走这块布自己的底色、装备走档位品质色</param>
        /// <param name="affordable">当前金币买不买得起，只影响价钱颜色</param>
        /// <param name="host">看详情与成交都交给它，卡片不自己扣钱</param>
        /// <param name="cellSprite">背包格子的底图，布形预览抄它才和买回去铺开的格子一个样</param>
        public void Setup(ShopStockEntry entry, Color goodsColor, bool affordable, IShopBackpackHost host,
            Sprite cellSprite)
        {
            ensureBound();

            if (entry == null)
            {
                QLog.Error("商品条目为空，卡片画不出内容");
                return;
            }

            m_entry = entry;
            m_host = host;
            m_ghostColor = goodsColor;
            m_wasDragged = false;

            applyVisual(entry, goodsColor, cellSprite);
            m_priceText.text = entry.IsSold ? "已售出" : $"{entry.Price} 金币";
            m_priceText.color = entry.IsSold || !affordable
                ? UIShopQuality.PriceUnaffordable
                : m_priceDefaultColor;

            m_button.interactable = !entry.IsSold;
        }

        // ==================== 拖拽与点击 ====================
        // 商品还没到手就先造一个临时实例拿去做幽灵，落点摆不摆得开由宿主逐格回答
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (m_host == null || m_entry == null || m_entry.IsSold)
                return;

            m_wasDragged = true;
            m_host.BeginGoodsDrag(m_entry);
            m_dragInstance = new BackpackItemInstance
            {
                Definition = m_entry.Definition,
                Tier = m_entry.Tier
            };
            m_ghost = UIShopShapeGhost.Build(m_dragInstance, m_ghostColor, m_host.CellPitch,
                m_host.GhostCellSprite, m_host.GhostLayer);
            moveGhost(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            moveGhost(eventData.position);
        }

        // 松手才结算：压在有位置的网格上就按那格入手，落在暂存架面板上就直接进架，两处都没挨上就不扣钱
        public void OnEndDrag(PointerEventData eventData)
        {
            if (m_host == null || m_entry == null)
                return;

            Vector2 screenPoint = eventData.position;
            UIShopShapeGhost.Recycle(m_ghost);
            m_ghost = null;
            m_dragInstance = null;
            // 松手在货架与网格之外只收得到 endDrag，收不到 click，标记只能在这里清，否则下一次真点击会被吞掉
            m_wasDragged = false;

            // 先把提上去的背包面板还回原位，成交之后的重铺才排在正常的层级上
            m_host.EndGoodsDrag(m_entry);

            if (m_entry.IsSold)
                return;

            if (m_host.IsPointerInStash(screenPoint))
            {
                m_host.TryBuyGoodsToStash(m_entry);
                return;
            }

            if (m_host.TryPointerCell(screenPoint, out int anchorColumn, out int anchorRow))
                m_host.TryBuyGoodsAt(m_entry, anchorColumn, anchorRow);
        }

        // ==================== Private Function ====================
        // 已售出只留一枚禁止图标，图标与异形布都收掉；在卖的才照布与装备两类各画各的
        private void applyVisual(ShopStockEntry entry, Color goodsColor, Sprite cellSprite)
        {
            m_soldOutImage.gameObject.SetActive(entry.IsSold);
            if (entry.IsSold)
            {
                m_clothShape.SetVisible(false);
                m_iconImage.gameObject.SetActive(false);
                return;
            }

            bool isCloth = entry.Definition.Kind == BackpackDefKind.Cloth;

            m_clothShape.SetVisible(isCloth);
            m_iconImage.gameObject.SetActive(!isCloth);

            if (isCloth)
            {
                m_clothShape.Setup(entry.Definition.ShapeAt(entry.Tier), goodsColor, cellSprite);
                return;
            }

            Sprite icon = entry.Definition.Icon;

            m_iconImage.sprite = icon != null ? icon : m_defaultIconSprite;
            m_iconImage.color = icon != null ? Color.white : goodsColor;
        }

        // 幽灵整块吸附到指针所在的那一格，顺带逐格问宿主放不放得下；指针还在网格外就只留图标跟着手
        private void moveGhost(Vector2 screenPoint)
        {
            if (m_ghost == null || m_host == null)
                return;

            if (m_host.TryPointerCell(screenPoint, out int anchorColumn, out int anchorRow))
            {
                UIShopShapeGhost.PlaceAt(m_ghost, m_dragInstance, anchorColumn, anchorRow, m_host.CellPitch, m_host);
                return;
            }

            UIShopShapeGhost.FollowPointer(m_ghost, m_dragInstance, pointerInGrid(screenPoint), m_host.CellPitch);
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

        // 点击一律只看详情，扣钱那条路交给拖拽，按钮本身不管买不买得起
        private void handleClicked()
        {
            if (m_wasDragged)
            {
                m_wasDragged = false;
                return;
            }

            if (m_host == null || m_entry == null)
                return;

            m_host.ShowGoodsDetail(m_entry, transform as RectTransform);
        }

        private void bindNodes()
        {
            m_iconImage = FindComponent<Image>(ICON_PATH);
            if (!m_iconDefaultCached)
            {
                m_defaultIconSprite = m_iconImage != null ? m_iconImage.sprite : null;
                m_iconDefaultCached = true;
            }

            m_clothShape = FindComponent<UIShopClothShape>(CLOTH_SHAPE_PATH);
            m_soldOutImage = FindComponent<Image>(SOLD_OUT_PATH);
            m_priceText = FindComponent<TMP_Text>(PRICE_PATH);
            m_button = GetComponent<Button>();

            m_priceDefaultColor = m_priceText != null ? m_priceText.color : Color.white;

            if (m_button != null && !m_listenerBound)
            {
                m_button.onClick.AddListener(handleClicked);
                m_listenerBound = true;
            }
        }

        // 模板复制出来的实例可能还没走 Awake，兜一次绑引用
        private void ensureBound()
        {
            if (m_iconImage == null || m_clothShape == null || m_soldOutImage == null
                || m_priceText == null)
                bindNodes();
        }
    }
}
