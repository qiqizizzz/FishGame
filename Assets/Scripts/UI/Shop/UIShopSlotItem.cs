/*
* ┌──────────────────────────────────┐
* │  描    述: 背包面板中的单个格子，区分空格与占用格
* │  类    名: UIShopSlotItem.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 格子的位置与尺寸交给 GridLayoutGroup，脚本只管底图、色块与名称
* 图标素材还没切，占用格先用品质色块顶着
*/

using Framework.QTower.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utils.log;

namespace UI.Shop
{
    public sealed class UIShopSlotItem : UIItemBase
    {
        private const string ICON_PATH = "Img_Icon";
        private const string LABEL_PATH = "Txt_Label";

        // ==================== 字段[外部设置] ====================
        [Tooltip("空格子底图")]
        [SerializeField] private Sprite EmptySprite;

        [Tooltip("占用格子底图")]
        [SerializeField] private Sprite FilledSprite;

        // ==================== 字段[私有] ====================
        private Image m_bgImage;
        private Image m_iconImage;
        private TMP_Text m_labelText;

        // ==================== 生命周期 ====================
        private void Awake()
        {
            bindNodes();
        }

        // ==================== Public Function ====================
        /// <summary>
        /// 清成一个空格子
        /// </summary>
        public void SetEmpty()
        {
            ensureBound();

            m_bgImage.sprite = EmptySprite;
            m_iconImage.gameObject.SetActive(false);
            m_labelText.gameObject.SetActive(false);
        }

        /// <summary>
        /// 放入一件物品
        /// </summary>
        /// <param name="itemName">物品名称</param>
        /// <param name="qualityColor">品质色，决定色块颜色</param>
        public void SetItem(string itemName, Color qualityColor)
        {
            ensureBound();

            m_bgImage.sprite = FilledSprite;
            m_iconImage.gameObject.SetActive(true);
            m_iconImage.color = qualityColor;
            m_labelText.gameObject.SetActive(true);
            m_labelText.text = itemName;
        }

        // ==================== Private Function ====================
        private void bindNodes()
        {
            m_bgImage = GetComponent<Image>();
            m_iconImage = FindComponent<Image>(ICON_PATH);
            m_labelText = FindComponent<TMP_Text>(LABEL_PATH);

#if UNITY_EDITOR
            if (m_bgImage == null)
                QLog.Error($"[{nameof(UIShopSlotItem)}] 格子上未挂载 Image：{gameObject.name}");
            if (EmptySprite == null || FilledSprite == null)
                QLog.Error($"[{nameof(UIShopSlotItem)}] 格子底图未指定：{gameObject.name}");
#endif
        }

        // 模板复制出来的实例可能还没走 Awake，兜一次绑引用
        private void ensureBound()
        {
            if (m_bgImage == null || m_iconImage == null || m_labelText == null)
                bindNodes();
        }
    }
}
