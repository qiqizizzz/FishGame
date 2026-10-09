/*
* ┌──────────────────────────────────┐
* │  描    述: 拖拽时浮出的幽灵形状，按实例当前朝向在网格层上摆出一块预览
* │  类    名: UIShopShapeGhost.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 暂存架里的一件只占一格，看不出它铺开是几格什么形，拖起来才照形状原样画一遍
* 画在背包的物品层上，坐标与真格子完全同源，预览落在哪格就是最终摆上网格的哪格
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

        // 暗边厚度与压暗比例抄装备块与布块，三处一个样才认得出是一路东西
        private const float RIM_THICKNESS = 4f;
        private const float RIM_DARKEN = 0.28f;

        // 幽灵整体压一点不透明度，和真正落在格子里的东西区分开
        private const float GHOST_ALPHA = 0.86f;

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

            // 物品层排在布层与选中外框之后，幽灵再压到本层最后，拖起来才在最上面
            root.SetAsLastSibling();
            return root;
        }

        /// <summary>
        /// 把幽灵整块挪到一个落点格上，位置吸附到格子，看见在哪就是最终在哪
        /// </summary>
        /// <param name="ghost">要挪的幽灵</param>
        /// <param name="column">落点锚列</param>
        /// <param name="row">落点锚行</param>
        /// <param name="pitch">单格边长加间距</param>
        public static void PlaceAt(RectTransform ghost, int column, int row, float pitch)
        {
            if (ghost == null)
                return;

            ghost.anchoredPosition = new Vector2(column * pitch, -row * pitch);
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
    }
}
