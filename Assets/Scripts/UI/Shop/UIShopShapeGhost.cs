/*
* ┌──────────────────────────────────┐
* │  描    述: 拖拽时浮出的幽灵形状，按实例当前朝向在网格层上摆出一块预览
* │  类    名: UIShopShapeGhost.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 暂存架里的一件只占一格，看不出它铺开是几格什么形，拖起来才照形状原样画一遍
* 画在背包的物品层上，坐标与真格子完全同源，预览落在哪格就是最终摆上网格的哪格
* 有图标就把图标摆在格子之上，格子只是垫在底下的叠加层，玩家手里始终看得见自己拿的是什么
* 图标的朝向与缩放同 UIShopIconFit 一处算出来，幽灵与落定的装备不会是两个样
* 每一格都问宿主放不放得下：盖得住的压成深色，缺格子的染成红，与背包里拖装备同一套脸
* 暗边加内缩亮面，与装备块、布块同一套画法，一眼认得出是同一件东西
* 整块都不吃射线，事件仍由起拖的那格接着，预览不会挡住真控件
* 只在一次拖拽里存在，松手就收掉，不进预制体也不参与任何布局
*/

using Module.Backpack.Model;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Shop
{
    public static class UIShopShapeGhost
    {
        private const string ROOT_NAME = "Ghost_Shape";
        private const string BLOCK_NAME = "Img_GhostCell";
        private const string FACE_NAME = "Img_GhostFace";
        private const string ICON_NAME = "Img_GhostIcon";

        // 暗边厚度与压暗比例抄装备块与布块，三处一个样才认得出是一路东西
        private const float RIM_THICKNESS = 4f;
        private const float RIM_DARKEN = 0.28f;

        // 幽灵整体压一点不透明度，和真正落在格子里的东西区分开
        private const float GHOST_ALPHA = 0.86f;

        // 图标再压一档透明度，预览浮在场上的东西不该比落在格子里的还实在
        private const float ICON_ALPHA = 0.8f;

        // 叠加格与两套染色的三个参数照抄 UIShopItemView，两处一个样才读得出同一套意思
        private const float OVERLAY_ALPHA = 0.6f;
        private const float COVER_DARKEN = 0.72f;
        private const float MISSING_DARKEN = 0.4f;
        private static readonly Color m_missingBase = new Color(0.86f, 0.16f, 0.12f, 1f);

        // ==================== Public Function ====================
        /// <summary>
        /// 按实例当前朝向摆出一块幽灵形状，父层左上角即网格第 0 格左上角
        /// </summary>
        /// <param name="item">要预览的存放物，只读它的定义、档位与朝向</param>
        /// <param name="color">这件东西在界面上的颜色，装备走品质色、布走布色</param>
        /// <param name="pitch">单格边长加间距</param>
        /// <param name="cellSprite">底层格子的底图，幽灵格抄它才和真格子一个样</param>
        /// <param name="layer">要挂到的层，一般是背包的物品层</param>
        public static RectTransform Build(BackpackItemInstance item, Color color, float pitch,
            Sprite cellSprite, RectTransform layer)
        {
            if (item?.Definition == null || layer == null)
                return null;

            ShapeMask shape = item.Shape;

            GameObject rootObject = new GameObject(ROOT_NAME, typeof(RectTransform));
            RectTransform root = (RectTransform)rootObject.transform;
            root.SetParent(layer, false);
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.sizeDelta = new Vector2(shape.Width * pitch, shape.Height * pitch);

            Color ghostColor = new Color(color.r, color.g, color.b, color.a * GHOST_ALPHA);

            foreach (Vector2Int cell in shape.Cells())
                showCell(root, cell.x, cell.y, pitch, ghostColor, cellSprite);

            // 图标最后建，天然压在全部格子之上，与背包里的叠法一致
            showIcon(root, item, shape, pitch);

            // 物品层排在布层与选中外框之后，幽灵再压到本层最后，拖起来才在最上面
            root.SetAsLastSibling();
            return root;
        }

        /// <summary>
        /// 把幽灵整块挪到一个落点格上并逐格重染，位置吸附到格子，看见在哪就是最终在哪
        /// </summary>
        /// <param name="ghost">要挪的幽灵</param>
        /// <param name="item">正在预览的存放物</param>
        /// <param name="column">落点锚列</param>
        /// <param name="row">落点锚行</param>
        /// <param name="pitch">单格边长加间距</param>
        /// <param name="host">逐格问放不放得下，传 null 就整块按缺格染</param>
        public static void PlaceAt(RectTransform ghost, BackpackItemInstance item, int column, int row,
            float pitch, IShopBackpackHost host)
        {
            if (ghost == null || item == null)
                return;

            ghost.anchoredPosition = new Vector2(column * pitch, -row * pitch);
            setCellsVisible(ghost, true);
            tintCells(ghost, item, column, row, host);
        }

        /// <summary>
        /// 指针落在网格外时让幽灵脱离格子，格子块收起来只留图标跟着手指，免得手里空无一物
        /// </summary>
        /// <param name="ghost">要挪的幽灵</param>
        /// <param name="item">正在预览的存放物</param>
        /// <param name="gridPoint">指针在网格平面里的坐标，左上角为原点</param>
        /// <param name="pitch">单格边长加间距</param>
        public static void FollowPointer(RectTransform ghost, BackpackItemInstance item, Vector2 gridPoint,
            float pitch)
        {
            if (ghost == null || item == null)
                return;

            ShapeMask shape = item.Shape;
            setCellsVisible(ghost, false);

            // 根节点轴心在包围盒左上，把图标中心对到指针上就是往右下让出去半个包围盒
            ghost.anchoredPosition = gridPoint - new Vector2(shape.Width * pitch * 0.5f,
                -shape.Height * pitch * 0.5f);
        }

        /// <summary>
        /// 收掉一块幽灵，没建起来的传 null 也安全
        /// </summary>
        /// <param name="ghost">要收的幽灵</param>
        public static void Recycle(RectTransform ghost)
        {
            if (ghost == null)
                return;

            GameObject root = ghost.gameObject;
            // 先当场藏掉，销毁要等到帧末才生效，编辑模式预览才不会留个空壳
            root.SetActive(false);
            if (Application.isPlaying)
                Object.Destroy(root);
            else
                Object.DestroyImmediate(root);
        }

        // ==================== Private Function ====================
        // 一格幽灵：暗边铺满整格，亮面四周内缩，底图与真格子同一张
        private static void showCell(RectTransform parent, int column, int row, float pitch,
            Color color, Sprite cellSprite)
        {
            GameObject cellObject = new GameObject(BLOCK_NAME, typeof(RectTransform), typeof(Image));
            RectTransform cell = (RectTransform)cellObject.transform;
            cell.SetParent(parent, false);
            cell.anchorMin = new Vector2(0f, 1f);
            cell.anchorMax = new Vector2(0f, 1f);
            cell.pivot = new Vector2(0f, 1f);
            cell.anchoredPosition = new Vector2(column * pitch, -row * pitch);
            cell.sizeDelta = new Vector2(pitch, pitch);

            Image rim = cell.GetComponent<Image>();
            rim.sprite = cellSprite;
            rim.color = Color.Lerp(color, Color.black, RIM_DARKEN);
            rim.raycastTarget = false;

            GameObject faceObject = new GameObject(FACE_NAME, typeof(RectTransform), typeof(Image));
            RectTransform face = (RectTransform)faceObject.transform;
            face.SetParent(cell, false);
            face.anchorMin = Vector2.zero;
            face.anchorMax = Vector2.one;
            face.pivot = new Vector2(0.5f, 0.5f);
            face.sizeDelta = new Vector2(-RIM_THICKNESS * 2f, -RIM_THICKNESS * 2f);

            Image faceImage = face.GetComponent<Image>();
            faceImage.sprite = cellSprite;
            faceImage.color = color;
            faceImage.raycastTarget = false;
        }

        // 图标的转角与缩放和背包里的装备共用 UIShopIconFit，拖进来看见什么朝向，松手落定就什么朝向
        private static void showIcon(RectTransform root, BackpackItemInstance item, ShapeMask shape, float pitch)
        {
            Sprite icon = item.Definition.Icon;
            if (icon == null)
                return;

            GameObject iconObject = new GameObject(ICON_NAME, typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)iconObject.transform;
            rect.SetParent(root, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            Image image = iconObject.GetComponent<Image>();
            UIShopIconFit.Apply(image, icon, shape.Width, shape.Height, pitch);
            image.raycastTarget = false;
            image.color = new Color(1f, 1f, 1f, ICON_ALPHA);
        }

        // 逐格重染：格子子节点的顺序与 ShapeMask.Cells() 完全一致，图标排在最后不掺进来
        private static void tintCells(RectTransform ghost, BackpackItemInstance item, int column, int row,
            IShopBackpackHost host)
        {
            Color qualityColor = UIShopQuality.ColorFor(item.Definition, item.Tier);
            Color coverColor = Color.Lerp(qualityColor, Color.black, COVER_DARKEN);
            coverColor.a = OVERLAY_ALPHA;

            Color missingColor = Color.Lerp(m_missingBase, Color.black, MISSING_DARKEN);
            missingColor.a = OVERLAY_ALPHA;

            int index = 0;

            foreach (Vector2Int cell in item.Shape.Cells())
            {
                if (index >= ghost.childCount)
                    return;

                bool valid = host != null && host.IsPreviewCellValid(item, column + cell.x, row + cell.y);
                paintCell((RectTransform)ghost.GetChild(index), valid ? coverColor : missingColor);
                index++;
            }
        }

        // 只切格子子节点，图标那一块任何时候都跟着幽灵走
        private static void setCellsVisible(RectTransform ghost, bool visible)
        {
            for (int i = 0; i < ghost.childCount; i++)
            {
                Transform child = ghost.GetChild(i);
                if (child.name == BLOCK_NAME)
                    child.gameObject.SetActive(visible);
            }
        }

        private static void paintCell(RectTransform cell, Color color)
        {
            cell.GetComponent<Image>().color = Color.Lerp(color, Color.black, RIM_DARKEN);

            RectTransform face = (RectTransform)cell.GetChild(0);
            face.GetComponent<Image>().color = color;
        }
    }
}
