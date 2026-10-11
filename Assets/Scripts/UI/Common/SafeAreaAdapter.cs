/*
 * ┌──────────────────────────────────┐
 * │  描    述: 安全区适配器，把全屏拉伸的界面内缩到系统安全区内
 * │  类    名: SafeAreaAdapter.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 * 刘海、挖孔与圆角屏会把 Screen.safeArea 从整屏里抠掉一条，铺满的贴顶控件会被压住
 * 这里把四边内缩量按「Canvas 单位 / 屏幕像素」比例折算回 offsetMin 与 offsetMax，只动挂点自身
 * 安全区等于整屏时原样还原挂载时的偏移，PC 与编辑器里零影响
 */

using UnityEngine;
using Utils.log;

namespace UI.Common
{
    /// <summary>
    /// 安全区适配器：把挂在 Canvas 直接子节点上的全屏 RectTransform 内缩到系统安全区
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaAdapter : MonoBehaviour
    {
        // ==================== 常量与静态字段 ====================
        // 判定「安全区等于整屏」的容差（屏幕像素），避免亚像素抖动引起反复重算
        private const float FULL_SCREEN_TOLERANCE = 1f;

        // ==================== 字段[外部设置] ====================
        [Header("内缩方向")]
        [Tooltip("顶部是否避开状态栏与刘海")]
        [SerializeField] private bool AdaptTop = true;

        [Tooltip("底部是否避开手势导航条")]
        [SerializeField] private bool AdaptBottom = true;

        [Tooltip("左侧是否避开横屏刘海")]
        [SerializeField] private bool AdaptLeft = true;

        [Tooltip("右侧是否避开横屏刘海")]
        [SerializeField] private bool AdaptRight = true;

        [Header("额外留白")]
        [Tooltip("在安全区基础上再多内缩的屏幕像素，只叠加到已启用的方向")]
        [SerializeField, Min(0f)] private float ExtraPadding = 0f;

        // ==================== 字段[私有] ====================
        private RectTransform m_rect;
        private RectTransform m_canvasRect;
        private Vector2 m_nativeOffsetMin;
        private Vector2 m_nativeOffsetMax;
        private Rect m_lastSafeArea;
        private Vector2Int m_lastScreenSize;
        private bool m_canAdapt;

        // ==================== 生命周期 ====================
        private void Awake()
        {
            m_rect = GetComponent<RectTransform>();

            // 记住挂载时的偏移而不是假定为零，预制体本身可能就带 offset
            m_nativeOffsetMin = m_rect.offsetMin;
            m_nativeOffsetMax = m_rect.offsetMax;

            Canvas parentCanvas = GetComponentInParent<Canvas>();
            m_canvasRect = parentCanvas != null ? parentCanvas.rootCanvas.transform as RectTransform : null;
            m_canAdapt = m_canvasRect != null && m_rect.parent == m_canvasRect;

#if UNITY_EDITOR
            if (m_canvasRect == null)
                QLog.Error($"[{nameof(SafeAreaAdapter)}] 取不到上级 Canvas，安全区适配已关闭：{gameObject.name}");
            else if (m_rect.parent != m_canvasRect)
                QLog.Error($"[{nameof(SafeAreaAdapter)}] 必须挂在 Canvas 的直接子节点上，安全区适配已关闭：{gameObject.name}");
#endif
        }

        private void Update()
        {
            if (!m_canAdapt)
                return;

            Rect safeArea = Screen.safeArea;
            Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);

            if (safeArea == m_lastSafeArea && screenSize == m_lastScreenSize)
                return;

            applySafeArea(safeArea, screenSize);
        }

        // ==================== Public Function ====================
        /// <summary>
        /// 丢弃缓存并立即按当前安全区重算，切屏或改挂载后可由外部主动调用
        /// </summary>
        public void Refresh()
        {
            m_lastSafeArea = default;
            m_lastScreenSize = default;

            if (!m_canAdapt)
                return;

            applySafeArea(Screen.safeArea, new Vector2Int(Screen.width, Screen.height));
        }

        // ==================== Private Function ====================
        // 把安全区四边内缩量换算成 Canvas 单位后写回偏移
        private void applySafeArea(Rect safeArea, Vector2Int screenSize)
        {
            float canvasWidth = m_canvasRect.rect.width;

            // CanvasScaler 还没刷新或屏幕尺寸还没就绪时直接跳过，且不记录缓存，交给下一帧重试
            if (canvasWidth <= 0f || screenSize.x <= 0 || screenSize.y <= 0
                || safeArea.width <= 0f || safeArea.height <= 0f)
                return;

            m_lastSafeArea = safeArea;
            m_lastScreenSize = screenSize;

            // 屏幕坐标原点在左下角，所以顶边内缩量是安全区上沿到屏幕顶的距离
            float top = Mathf.Max(0f, screenSize.y - (safeArea.y + safeArea.height));
            float bottom = Mathf.Max(0f, safeArea.y);
            float left = Mathf.Max(0f, safeArea.x);
            float right = Mathf.Max(0f, screenSize.x - (safeArea.x + safeArea.width));

            // 整屏都可用（PC、无刘海的机器）就还原，不产生任何额外偏移
            if (top <= FULL_SCREEN_TOLERANCE && bottom <= FULL_SCREEN_TOLERANCE
                && left <= FULL_SCREEN_TOLERANCE && right <= FULL_SCREEN_TOLERANCE)
            {
                restore();
                return;
            }

            float unitsPerPixel = canvasWidth / screenSize.x;

            Vector2 minInset = new Vector2(AdaptLeft ? left + ExtraPadding : 0f,
                                           AdaptBottom ? bottom + ExtraPadding : 0f);
            Vector2 maxInset = new Vector2(AdaptRight ? right + ExtraPadding : 0f,
                                           AdaptTop ? top + ExtraPadding : 0f);

            m_rect.offsetMin = m_nativeOffsetMin + minInset * unitsPerPixel;
            m_rect.offsetMax = m_nativeOffsetMax - maxInset * unitsPerPixel;
        }

        // 还原挂载时的原生偏移
        private void restore()
        {
            m_rect.offsetMin = m_nativeOffsetMin;
            m_rect.offsetMax = m_nativeOffsetMax;
        }
    }
}