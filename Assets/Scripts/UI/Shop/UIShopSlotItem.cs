/*
* ┌──────────────────────────────────┐
* │  描    述: 面板中的单个格子，负责空格、锁定与仓库暂存三种外观
* │  类    名: UIShopSlotItem.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 位置与尺寸交给 GridLayoutGroup，脚本只切底图与颜色
* 背包格只当底，压在上面的物品由 UIShopItemView 单独画一层
* 仓库格自己就是物品，整格染成品质色并标出名称
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
        // 仓库格用来标注物品名称，背包格没有这个子节点
        private const string LABEL_PATH = "Txt_Label";

        // ==================== 字段[外部设置] ====================
        [Tooltip("空格子的底图，锁定格与仓库格是纯色块，不用它")]
        [SerializeField] private Sprite CellSprite;

        [Tooltip("锁定格的颜色")]
        [SerializeField] private Color LockedColor = new Color32(0x6E, 0x5F, 0x45, 0xFF);

        // ==================== 字段[私有] ====================
        private Image m_bgImage;
        private TMP_Text m_labelText;

        // ==================== 生命周期 ====================
        private void Awake()
        {
            bindNodes();
        }

        // ==================== Public Function ====================
        /// <summary>
        /// 清成一个可放置的空格
        /// </summary>
        public void SetEmpty()
        {
            ensureBound();

            m_bgImage.sprite = CellSprite;
            m_bgImage.color = Color.white;
            hideLabel();
        }

        /// <summary>
        /// 变成一块锁定的暗色格子，背包解锁后才会展成空格
        /// </summary>
        public void SetLocked()
        {
            ensureBound();

            m_bgImage.sprite = null;
            m_bgImage.color = LockedColor;
            hideLabel();
        }

        /// <summary>
        /// 仓库格放入一件物品，整格染成品质色
        /// </summary>
        /// <param name="itemName">物品名称</param>
        /// <param name="qualityColor">品质色，决定整格颜色</param>
        public void SetStored(string itemName, Color qualityColor)
        {
            ensureBound();

            m_bgImage.sprite = null;
            m_bgImage.color = qualityColor;

            // 图标素材还没切，仓库格靠名称认物品，没有文字节点的格子只染色
            if (m_labelText != null)
            {
                m_labelText.gameObject.SetActive(true);
                m_labelText.text = itemName;

                m_labelText.color = UIShopQuality.LabelColor(qualityColor);
            }
        }

        // ==================== Private Function ====================
        private void bindNodes()
        {
            m_bgImage = GetComponent<Image>();

            Transform label = FindNode(LABEL_PATH);
            m_labelText = label != null ? label.GetComponent<TMP_Text>() : null;

#if UNITY_EDITOR
            if (m_bgImage == null)
                QLog.Error($"格子上未挂载 Image：{gameObject.name}");
            if (CellSprite == null)
                QLog.Error($"空格子底图未指定：{gameObject.name}");
#endif
        }

        private void hideLabel()
        {
            if (m_labelText != null)
                m_labelText.gameObject.SetActive(false);
        }

        // 模板复制出来的实例可能还没走 Awake，兜一次绑引用
        private void ensureBound()
        {
            if (m_bgImage == null)
                bindNodes();
        }
    }
}
