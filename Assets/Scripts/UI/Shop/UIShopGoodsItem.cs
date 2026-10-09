/*
* ┌──────────────────────────────────┐
* │  描    述: 商店货架上的一张商品卡，显示款式、加成与售价，点一下就走购买
* │  类    名: UIShopGoodsItem.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 卡面只照条目本来的样子画，扣钱与入手全部交给回调，卡片自己不改任何状态
* 买不起只把价钱染红，卡片照样能点，让服务去给「金币不够」的准信
* 加成一次问定义要一整串，一块布挂三条就写三条，不在这里猜它只有哪一条
* 图标有就贴图并把染色还原成白色，没图标的布与板块才退回底色色块
*/

using System;
using System.Collections.Generic;
using Framework.QTower.View;
using Module.Backpack.Data;
using Module.Shop.Model;
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
        private const string DETAIL_PATH = "Txt_Detail";
        private const string PRICE_PATH = "Txt_Price";

        // 买不起时的价钱色，压在木色底上够扎眼又不刺眼
        private static readonly Color m_priceUnaffordable = new Color32(0xB0, 0x3A, 0x2E, 0xFF);

        // 已售出时整张卡的压暗色
        private static readonly Color m_soldTint = new Color32(0x8C, 0x8C, 0x8C, 0xFF);

        // ==================== 字段[私有] ====================
        private Image m_iconImage;

        // 卡片从复用池里拉出来反复填商品，换货时要能还原预制体自带的底图
        private Sprite m_defaultIconSprite;
        private bool m_iconDefaultCached;

        private TMP_Text m_nameText;
        private TMP_Text m_detailText;
        private TMP_Text m_priceText;
        private Button m_button;

        // 价钱原本的颜色从预制体上读一次，染回时不写死，调色板改了不用改代码
        private Color m_priceDefaultColor;

        // 每次刷新卡片都清空重填，省得一件商品一条加成 new 一个列表
        private readonly List<BackpackBuff> m_buffBuffer = new();
        private Action<ShopStockEntry> m_onBuy;
        private ShopStockEntry m_entry;

        // ==================== 生命周期 ====================
        private void Awake()
        {
            bindNodes();
        }

        // ==================== Public Function ====================
        /// <summary>
        /// 填入一件商品
        /// </summary>
        /// <param name="entry">货架条目，款式档位售价都在里面</param>
        /// <param name="goodsColor">卡面色块，板块走阵营纸色、装备走档位色</param>
        /// <param name="affordable">当前金币买不买得起，只影响价钱颜色</param>
        /// <param name="onBuy">点击购买时的回调，条目为 null 时点了没反应</param>
        public void Setup(ShopStockEntry entry, Color goodsColor, bool affordable, Action<ShopStockEntry> onBuy)
        {
            ensureBound();

            if (entry == null)
            {
                QLog.Error("商品条目为空，卡片画不出内容");
                return;
            }

            m_entry = entry;
            m_onBuy = onBuy;

            applyIcon(entry.Definition.Icon, entry.IsSold, goodsColor);
            m_nameText.text = UIShopGoodsName.Of(entry.Definition, entry.Tier);
            m_nameText.color = UIShopQuality.LabelColor(goodsColor);
            m_priceText.text = entry.IsSold ? "已售出" : $"{entry.Price} 金币";
            m_priceText.color = entry.IsSold || !affordable ? m_priceUnaffordable : m_priceDefaultColor;

            if (m_detailText != null)
            {
                m_buffBuffer.Clear();
                entry.Definition.FillBuffs(entry.Tier, m_buffBuffer);
                m_detailText.text = UIShopBuffText.OfMany(m_buffBuffer);
                m_detailText.color = m_priceDefaultColor;
            }

            m_button.interactable = !entry.IsSold;
        }

        // ==================== Private Function ====================
        // 有图标就贴图并把颜色还原成白色，已售出靠压暗表达；没图标的布与板块沿用档位色块
        private void applyIcon(Sprite icon, bool isSold, Color goodsColor)
        {
            Color tintColor = isSold ? m_soldTint : (icon != null ? Color.white : goodsColor);

            m_iconImage.sprite = icon != null ? icon : m_defaultIconSprite;
            m_iconImage.color = tintColor;
        }

        private void handleClicked()
        {
            if (m_entry == null)
                return;

            m_onBuy?.Invoke(m_entry);
        }

        private void bindNodes()
        {
            m_iconImage = FindComponent<Image>(ICON_PATH);
            if (!m_iconDefaultCached)
            {
                m_defaultIconSprite = m_iconImage != null ? m_iconImage.sprite : null;
                m_iconDefaultCached = true;
            }

            m_nameText = FindComponent<TMP_Text>(NAME_PATH);
            m_priceText = FindComponent<TMP_Text>(PRICE_PATH);
            m_button = GetComponent<Button>();

            m_priceDefaultColor = m_priceText != null ? m_priceText.color : Color.white;

            Transform detail = FindNode(DETAIL_PATH);
            m_detailText = detail != null ? detail.GetComponent<TMP_Text>() : null;

            if (m_button != null)
                m_button.onClick.AddListener(handleClicked);
        }

        private void ensureBound()
        {
            if (m_iconImage == null || m_nameText == null || m_priceText == null)
                bindNodes();
        }
    }
}
