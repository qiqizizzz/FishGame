/*
* ┌──────────────────────────────────┐
* │  描    述: 装备图标的自适应摆位算法，量出素材长轴后旋转缩放，把它完整塞进包围盒
* │  类    名: UIShopIconFit.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 素材画成什么斜度不该反过来迁就格子：这里只读一遍不透明像素，量出它自己的长短轴
* 包围盒竖着就把素材立起来，横着就放倒，方盒子没有长轴可对，原样摆着最合画师本意
* 转角定完还要按旋转之后的真实范围重新缩放，斜着画的东西转完会变瘦，只看贴图方框会把它算小一半
* 画面在盒子里还要重新对中，素材的重心常常并不在贴图正中央
* 读过的像素与算过的摆法都按素材留着，重铺背包时不再回头算第二遍
* 背包里的装备与拖拽时的幽灵走同一处，拖进来是什么朝向，松手落定就是什么朝向
*/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Utils.log;

namespace UI.Shop
{
    /// <summary>
    /// 一件图标在指定包围盒里的摆法
    /// </summary>
    public struct UIShopIconFitResult
    {
        /// <summary>
        /// 是否真读到了素材像素，读不到就退回等比铺满交给 Image 自己处理
        /// </summary>
        public bool analyzed;

        /// <summary>
        /// 图标矩形自身的宽高，旋转之前那一份
        /// </summary>
        public Vector2 size;

        /// <summary>
        /// 图标绕自身中心的 Z 轴转角
        /// </summary>
        public float angle;

        /// <summary>
        /// 图标矩形中心相对包围盒中心的偏移，把转完的画面重新拉回正中
        /// </summary>
        public Vector2 offset;
    }

    /// <summary>
    /// 量素材、定转角、算缩放，背包图标与幽灵预览共用这一套摆法
    /// </summary>
    public static class UIShopIconFit
    {
        // 透明度低于这个值就当没画东西，边缘那点抗锯齿不参与算长轴
        private const float ALPHA_PRESENT = 26f;

        // 长短轴能量比不到这个倍数就当素材没有朝向可言，硬转只会把立着的东西放倒
        private const float MIN_ANISOTROPY = 1.6f;

        // 缩放本身被包围盒卡着，这里只挡实心像素几乎没剩下时的除法溢出
        private const float MAX_SCALE = 32f;

        // 盒子长短边差不到这个比例就当它是方的，方的没有长轴可对齐
        private const float SQUARE_TOLERANCE = 1.02f;

        // 一个素材读一遍像素之后的家底：自身尺寸、自然朝向、长短轴比与全部实心点
        private sealed class Source
        {
            public int Width;
            public int Height;
            public bool Usable;
            public float Axis;
            public float Aniso;
            public Vector2[] Points;
            public readonly Dictionary<int, UIShopIconFitResult> Fits = new();
        }

        // ==================== 字段[私有] ====================
        private static readonly Dictionary<Sprite, Source> m_sources = new();

        // ==================== Public Function ====================
        /// <summary>
        /// 把图标按算出来的摆法直接写到它自己的矩形上，并把这份摆法回给调用方
        /// </summary>
        /// <param name="image">要摆的图标</param>
        /// <param name="icon">贴图，为空时只把画面清掉</param>
        /// <param name="columns">包围盒占几列</param>
        /// <param name="rows">包围盒占几行</param>
        /// <param name="pitch">单格边长加间距</param>
        public static UIShopIconFitResult Apply(Image image, Sprite icon, int columns, int rows, float pitch)
        {
            UIShopIconFitResult fit = Compute(icon, columns, rows, pitch);

            if (image == null)
                return fit;

            RectTransform rect = (RectTransform)image.transform;
            float boxWidth = columns * pitch;
            float boxHeight = rows * pitch;

            rect.sizeDelta = fit.size;
            rect.anchoredPosition = new Vector2(boxWidth * 0.5f, -boxHeight * 0.5f) + fit.offset;
            rect.localEulerAngles = new Vector3(0f, 0f, fit.angle);

            image.sprite = icon;
            // 算得出来就按这份尺寸精确铺，算不出来才交给 Image 自己等比
            image.preserveAspect = !fit.analyzed;
            return fit;
        }

        /// <summary>
        /// 只算不摆，量一件图标在该尺寸的包围盒里该转多少度、占多大一块
        /// </summary>
        /// <param name="icon">贴图</param>
        /// <param name="columns">包围盒占几列</param>
        /// <param name="rows">包围盒占几行</param>
        /// <param name="pitch">单格边长加间距</param>
        public static UIShopIconFitResult Compute(Sprite icon, int columns, int rows, float pitch)
        {
            float boxWidth = columns * pitch;
            float boxHeight = rows * pitch;

            // 读不了像素时的兜底：矩形铺满包围盒，画面由 Image 自己等比缩到盒内最大
            UIShopIconFitResult fallback = new UIShopIconFitResult
            {
                analyzed = false,
                size = new Vector2(boxWidth, boxHeight),
                angle = 0f,
                offset = Vector2.zero
            };

            Source source = read(icon);

            if (source == null || !source.Usable)
                return fallback;

            int key = columns * 64 + rows;

            if (source.Fits.TryGetValue(key, out UIShopIconFitResult cached))
                return cached;

            UIShopIconFitResult fit = solve(source, boxWidth, boxHeight);

            if (!fit.analyzed)
                return fallback;

            source.Fits[key] = fit;
            return fit;
        }

        // ==================== Private Function ====================
        // 每个素材只读一遍像素，读失败也照样记一笔，省得每件东西都重读一次
        private static Source read(Sprite icon)
        {
            if (icon == null)
                return null;

            if (m_sources.TryGetValue(icon, out Source cached))
                return cached;

            Source source = analyze(icon);
            m_sources[icon] = source;
            return source;
        }

        // 读贴图子块的不透明像素，按 alpha 加权做一次主成分分析，长轴与粗细程度都从这来
        private static Source analyze(Sprite icon)
        {
            Source source = new Source();
            Texture2D texture = icon.texture;

            if (texture == null || !texture.isReadable)
            {
                QLog.Warning($"图标贴图不可读，退回等比铺满：{icon.name}");
                return source;
            }

            Rect area = icon.textureRect;
            int originX = Mathf.RoundToInt(area.x);
            int originY = Mathf.RoundToInt(area.y);
            int width = Mathf.RoundToInt(area.width);
            int height = Mathf.RoundToInt(area.height);

            if (width <= 0 || height <= 0)
                return source;

            Color32[] pixels = texture.GetPixels32();
            int stride = texture.width;
            List<Vector2> points = new List<Vector2>(width * height / 4);

            double sum = 0;
            double sumX = 0, sumY = 0;
            double sumXX = 0, sumYY = 0, sumXY = 0;

            for (int y = 0; y < height; y++)
            {
                int rowBase = (originY + y) * stride + originX;

                for (int x = 0; x < width; x++)
                {
                    int index = rowBase + x;

                    if (index >= pixels.Length)
                        break;

                    Color32 pixel = pixels[index];

                    if (pixel.a < ALPHA_PRESENT)
                        continue;

                    double weight = pixel.a / 255d;
                    double fx = x + 0.5d, fy = y + 0.5d;

                    sum += weight;
                    sumX += fx * weight;
                    sumY += fy * weight;
                    sumXX += fx * fx * weight;
                    sumYY += fy * fy * weight;
                    sumXY += fx * fy * weight;

                    points.Add(new Vector2((float)(fx - width * 0.5d), (float)(fy - height * 0.5d)));
                }
            }

            if (sum <= 0d)
                return source;

            double meanX = sumX / sum, meanY = sumY / sum;
            double covXX = sumXX / sum - meanX * meanX;
            double covYY = sumYY / sum - meanY * meanY;
            double covXY = sumXY / sum - meanX * meanY;
            double trace = covXX + covYY;
            double spread = System.Math.Sqrt(System.Math.Max(0d, trace * trace * 0.25d - (covXX * covYY - covXY * covXY)));
            double major = trace * 0.5d + spread;
            double minor = trace * 0.5d - spread;

            source.Width = width;
            source.Height = height;
            source.Points = points.ToArray();
            source.Usable = points.Count > 0;
            source.Axis = (float)(0.5d * System.Math.Atan2(2d * covXY, covXX - covYY) * Mathf.Rad2Deg);
            source.Aniso = minor > 1e-6d ? (float)System.Math.Sqrt(major / minor) : float.MaxValue;
            return source;
        }

        // 定转角、按转完的真实范围缩放、再把画面拉回盒子正中，三步都在这
        private static UIShopIconFitResult solve(Source source, float boxWidth, float boxHeight)
        {
            float angle = 0f;

            // 素材本身分得出长短轴才谈对齐，一团圆滚滚的东西转了只会看着更奇怪
            if (source.Aniso >= MIN_ANISOTROPY)
            {
                if (boxHeight > boxWidth * SQUARE_TOLERANCE)
                    angle = 90f - source.Axis;
                else if (boxWidth > boxHeight * SQUARE_TOLERANCE)
                    angle = -source.Axis;

                angle = fold(angle);
            }

            extent(source.Points, angle, out float extentWidth, out float extentHeight, out float centerX, out float centerY);

            if (extentWidth <= 0f || extentHeight <= 0f)
                return new UIShopIconFitResult { analyzed = false, size = new Vector2(boxWidth, boxHeight) };

            float scale = Mathf.Min(boxWidth / extentWidth, boxHeight / extentHeight);
            scale = Mathf.Min(scale, MAX_SCALE);

            return new UIShopIconFitResult
            {
                analyzed = true,
                size = new Vector2(source.Width * scale, source.Height * scale),
                angle = angle,
                offset = new Vector2(-centerX * scale, -centerY * scale)
            };
        }

        // 把全部实心点转过去再取外接范围，斜着画的素材转完会变瘦，这一步才是它真正占多大
        private static void extent(Vector2[] points, float angle, out float width, out float height,
            out float centerX, out float centerY)
        {
            float cos = Mathf.Cos(angle * Mathf.Deg2Rad);
            float sin = Mathf.Sin(angle * Mathf.Deg2Rad);
            float minU = float.MaxValue, maxU = float.MinValue;
            float minV = float.MaxValue, maxV = float.MinValue;

            for (int i = 0; i < points.Length; i++)
            {
                Vector2 point = points[i];
                float u = point.x * cos - point.y * sin;
                float v = point.x * sin + point.y * cos;

                if (u < minU) minU = u;
                if (u > maxU) maxU = u;
                if (v < minV) minV = v;
                if (v > maxV) maxV = v;
            }

            // 每个实心像素是一格见方的方块，只按像素中心取范围会少算一圈边
            // 转正时补一个像素的厚度，斜着转则要补上这个像素方块在两个方向上的投影
            float pad = Mathf.Abs(cos) + Mathf.Abs(sin);

            width = maxU - minU + pad;
            height = maxV - minV + pad;
            centerX = (minU + maxU) * 0.5f;
            centerY = (minV + maxV) * 0.5f;
        }

        // 长轴没有头尾，转角折进 (-90, 90] 才不会把好好的东西倒过来插进格子里
        private static float fold(float angle)
        {
            while (angle > 90f) angle -= 180f;
            while (angle <= -90f) angle += 180f;
            return angle;
        }
    }
}
