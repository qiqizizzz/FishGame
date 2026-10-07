/*
* ┌──────────────────────────────────┐
* │  描    述: 结算面板中的单条鱼行，显示名称/星级/收益与档位色标
* │  类    名: UIResultFishItem.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 行由面板用模板复制出来，实例会被反复创建，所以引用在 Awake 一次绑好，避免每次复制都 Find
*/

using Framework.QTower.View;
using Module.Fish.Model;
using Module.Fish.Type;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Summary
{
    public sealed class UIResultFishItem : UIItemBase
    {
        private const string MARK_PATH = "Img_Mark";
        private const string NAME_PATH = "Txt_Name";
        private const string STARS_PATH = "Txt_Stars";
        private const string COINS_PATH = "Txt_Coins";

        private Image m_markImage;
        private TMP_Text m_nameText;
        private TMP_Text m_starsText;
        private TMP_Text m_coinsText;

        private void Awake()
        {
            bindNodes();
        }

        /// <summary>
        /// 填入一条结算明细
        /// </summary>
        private void bindNodes()
        {
            m_markImage = FindComponent<Image>(MARK_PATH);
            m_nameText = FindComponent<TMP_Text>(NAME_PATH);
            m_starsText = FindComponent<TMP_Text>(STARS_PATH);
            m_coinsText = FindComponent<TMP_Text>(COINS_PATH);
        }

        public void Setup(FishScoreItem item)
        {
            if (m_nameText == null)
                bindNodes();

            m_nameText.text = item.DisplayName;
            m_starsText.text = item.Rarity == FishRarity.Hazard ? "危险" : new string('★', Mathf.Max(1, item.Stars));
            m_coinsText.text = $"+{item.Coins}";
            m_markImage.color = item.PrimaryColor;
        }
    }
}