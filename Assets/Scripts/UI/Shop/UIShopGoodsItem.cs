/*
* ┌──────────────────────────────────┐
* │  描    述: 商店中的单件商品卡，显示色块图标、名称与价格
* │  类    名: UIShopGoodsItem.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 卡片由面板用隐藏模板复制出来反复开关，所以引用在 Awake 一次绑好
* 购买交互与商品数据等商店模块接上再补，这里只负责显示
*/

using Framework.QTower.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utils.log;

namespace UI.Shop
{
    public sealed class UIShopGoodsItem : UIItemBase
    {
        private const string ICON_PATH = "Img_Icon";
        private const string NAME_PATH = "Txt_Name";
        private const string PRICE_PATH = "Txt_Price";

        // ==================== 字段[私有] ====================
        private Image m_iconImage;
        private TMP_Text m_nameText;
        private TMP_Text m_priceText;

        // ==================== 生命周期 ====================
        private void Awake()
        {
            bindNodes();
        }

        // ==================== Public Function ====================
        /// <summary>
        /// 填入一件商品
        /// </summary>
        /// <param name="goodsName">商品名称</param>
        /// <param name="price">售价（金币）</param>
        /// <param name="qualityColor">品质色，决定色块颜色</param>
        public void Setup(string goodsName, int price, Color qualityColor)
        {
            ensureBound();

            m_iconImage.color = qualityColor;
            m_nameText.text = goodsName;
            m_priceText.text = $"{price} 金币";
        }

        // ==================== Private Function ====================
        private void bindNodes()
        {
            m_iconImage = FindComponent<Image>(ICON_PATH);
            m_nameText = FindComponent<TMP_Text>(NAME_PATH);
            m_priceText = FindComponent<TMP_Text>(PRICE_PATH);
        }

        private void ensureBound()
        {
            if (m_iconImage == null || m_nameText == null || m_priceText == null)
                bindNodes();
        }
    }
}
