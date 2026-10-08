/*
* ┌──────────────────────────────────┐
* │  描    述: 背包里的跨格物品视图，按形状格拼出俄罗斯方块式的整体轮廓
* │  类    名: UIShopItemView.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 挂在网格之上的 Layer_Items，位置与尺寸由形状格和网格单格尺寸算出
* 每个形状格画一块方块，方块尺寸含格间距，跨格后不留缝，读成一整件物品
* 物品图标素材还没切，先用暗边加亮面两层纯色顶替
*/

using System.Collections.Generic;
using Framework.QTower.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utils.log;

namespace UI.Shop
{
    public sealed class UIShopItemView : UIItemBase
    {
        private const string LABEL_PATH = "Txt_Label";
        private const string BLOCK_NAME = "Img_Block";
        private const string FACE_NAME = "Img_Face";

        // 方块四周留出的暗边厚度，相邻方块之间的凹槽是它的两倍宽
        private const float RIM_THICKNESS = 4f;

        // 暗边相对品质色压暗的比例
        private const float RIM_DARKEN = 0.28f;

        // ==================== 字段[私有] ====================
        private RectTransform m_rect;
        private TMP_Text m_labelText;

        // 方块只创建不销毁，换形状时多出来的先藏起来
        private readonly List<RectTransform> m_blocks = new();
        private int m_blockUsed;

        // ==================== 生命周期 ====================
        private void Awake()
        {
            bindNodes();
        }

        // ==================== Public Function ====================
        /// <summary>
        /// 按形状格摆放一件物品
        /// </summary>
        /// <param name="cells">物品占用的格子，坐标为（列, 行），左上角为原点</param>
        /// <param name="cellSize">单格边长，取自背包网格</param>
        /// <param name="cellSpacing">单格间距，取自背包网格</param>
        /// <param name="qualityColor">品质色，决定方块底色</param>
        /// <param name="itemName">物品名称，显示在物品正中</param>
        public void Setup(Vector2Int[] cells, float cellSize, float cellSpacing, Color qualityColor, string itemName)
        {
            ensureBound();

            if (cells == null || cells.Length == 0)
            {
#if UNITY_EDITOR
                QLog.Error($"形状格为空，无法摆放物品：{itemName}");
#endif
                return;
            }

            float pitch = cellSize + cellSpacing;
            int minColumn = int.MaxValue;
            int maxColumn = int.MinValue;
            int minRow = int.MaxValue;
            int maxRow = int.MinValue;

            for (int i = 0; i < cells.Length; i++)
            {
                minColumn = Mathf.Min(minColumn, cells[i].x);
                maxColumn = Mathf.Max(maxColumn, cells[i].x);
                minRow = Mathf.Min(minRow, cells[i].y);
                maxRow = Mathf.Max(maxRow, cells[i].y);
            }

            m_rect.anchorMin = new Vector2(0f, 1f);
            m_rect.anchorMax = new Vector2(0f, 1f);
            m_rect.pivot = new Vector2(0f, 1f);
            m_rect.anchoredPosition = new Vector2(minColumn * pitch, -minRow * pitch);
            m_rect.sizeDelta = new Vector2((maxColumn - minColumn + 1) * pitch, (maxRow - minRow + 1) * pitch);

            m_blockUsed = 0;

            for (int i = 0; i < cells.Length; i++)
                showBlock(cells[i].x - minColumn, cells[i].y - minRow, pitch, qualityColor);

            for (int i = m_blockUsed; i < m_blocks.Count; i++)
                m_blocks[i].gameObject.SetActive(false);

            m_labelText.text = itemName;
            // 名称压在色块上，跨格时会压到格子缝隙，描一圈深色边保证任何底色都看得清
            m_labelText.color = UIShopQuality.LabelColor(qualityColor);
            m_labelText.outlineColor = new Color32(0x1B, 0x12, 0x08, 0xB0);
            m_labelText.outlineWidth = 0.14f;
            // 方块是后建的同级节点，会把名称压在下面，每次摆完把名称提回最上层
            m_labelText.transform.SetAsLastSibling();
        }

        // ==================== Private Function ====================
        private void showBlock(int column, int row, float pitch, Color qualityColor)
        {
            RectTransform block = m_blockUsed < m_blocks.Count ? m_blocks[m_blockUsed] : createBlock();
            m_blockUsed++;

            block.gameObject.SetActive(true);
            block.anchoredPosition = new Vector2(column * pitch, -row * pitch);
            block.sizeDelta = new Vector2(pitch, pitch);
            block.GetComponent<Image>().color = Color.Lerp(qualityColor, Color.black, RIM_DARKEN);

            RectTransform face = (RectTransform)block.GetChild(0);
            face.sizeDelta = new Vector2(-RIM_THICKNESS * 2f, -RIM_THICKNESS * 2f);
            face.GetComponent<Image>().color = qualityColor;
        }

        // 一块形状格：暗边铺满整块，亮面四周内缩，方块之间的凹槽由两侧暗边拼出来
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
            face.GetComponent<Image>().raycastTarget = false;

            m_blocks.Add(block);
            return block;
        }

        private void bindNodes()
        {
            m_rect = (RectTransform)transform;
            m_labelText = FindComponent<TMP_Text>(LABEL_PATH);
        }

        private void ensureBound()
        {
            if (m_rect == null || m_labelText == null)
                bindNodes();
        }
    }
}
