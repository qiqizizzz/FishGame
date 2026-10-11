/*
 * ┌──────────────────────────────────┐
 * │  描    述: 竖屏运行时适配器，PC 强制 9:16 竖屏窗口，移动端锁定竖屏方向
 * │  类    名: ScreenAdapter.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 * 工程按 1080x1920 竖屏设计，PC 打包默认按桌面分辨率起横屏窗口，CanvasScaler 与正交
 * 相机的 aspect 同时错位，UI 与世界构图一起变形；移动端跟随系统旋转同样会崩
 * 这里在首个场景加载前接管显示参数：PC 开一个能完整放进工作区的最大 9:16 竖屏窗口，
 * 移动端强制竖屏，编辑器不接管 Game 视图
 */

using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using Utils.log;

namespace Game
{
    /// <summary>
    /// 竖屏运行时适配器：接管 PC 窗口尺寸与移动端屏幕方向
    /// </summary>
    public sealed class ScreenAdapter : MonoBehaviour
    {
        // ==================== 常量与静态字段 ====================
        /// <summary>设计分辨率宽，需与 UIRoot 上 CanvasScaler 的参考分辨率一致</summary>
        public const int DESIGN_WIDTH = 1080;

        /// <summary>设计分辨率高，需与 UIRoot 上 CanvasScaler 的参考分辨率一致</summary>
        public const int DESIGN_HEIGHT = 1920;

        private const int FALLBACK_CHROME_HEIGHT = 104;  // 取不到工作区时按标题栏加任务栏预留
        private const int FALLBACK_CHROME_WIDTH = 32;
        private const int TITLE_BAR_HEIGHT = 40;         // 100% 缩放下的标题栏与边框高度
        private const int MIN_WINDOW_HEIGHT = 420;
        private const int SHRINK_STEP_ON_FAIL = 96;      // 窗口被系统压缩时每次再让出的高度
        private const int MAX_FIT_ATTEMPTS = 4;
        private const float ASPECT_TOLERANCE = 0.02f;    // 允许的宽高比相对偏差
        private const int WINDOW_SIZE_TOLERANCE = 24;  // SetResolution 允许的尺寸偏差，超过视为窗口未真正放大
        private const float RECHECK_INTERVAL = 0.5f;

        private static ScreenAdapter m_instance;

#if UNITY_STANDALONE_WIN
        private const int SPI_GETWORKAREA = 0x0030;

        [StructLayout(LayoutKind.Sequential)]
        private struct WinRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern bool SystemParametersInfo(int action, int param, ref WinRect rect, int flags);

        [DllImport("user32.dll")]
        private static extern int GetDpiForSystem();
#endif

        // ==================== 属性 ====================
        /// <summary>设计宽高比（宽 / 高），竖屏为 0.5625</summary>
        public static float DesignAspect => (float)DESIGN_WIDTH / DESIGN_HEIGHT;

        // ==================== 字段[私有] ====================
        private Vector2Int m_usableSize;
        private int m_shrinkStep;
        private float m_nextCheckTime;
        private Coroutine m_fitCoroutine;

        // ==================== 生命周期 ====================
        private void Awake()
        {
            if (m_instance != null && m_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            m_instance = this;
            DontDestroyOnLoad(gameObject);
            applyPlatformLayout();
        }

        private void Update()
        {
#if UNITY_STANDALONE
            if (m_fitCoroutine != null || Time.unscaledTime < m_nextCheckTime)
                return;

            m_nextCheckTime = Time.unscaledTime + RECHECK_INTERVAL;

            // 换显示器、改分辨率或收放任务栏就重新贴合，其余情况只在窗口比例失守时补救
            Vector2Int usable = usableDesktopSize();

            if (usable == m_usableSize && isAspectRatioOk())
                return;

            m_usableSize = usable;
            m_shrinkStep = 0;
            m_fitCoroutine = StartCoroutine(fitDesktopWindow());
#endif
        }

        // ==================== Public Function ====================
        /// <summary>
        /// 在可用区域内算出能完整放下的最大 9:16 竖屏窗口尺寸
        /// </summary>
        /// <param name="usableWidth">可用宽度（像素，已扣除窗口边框）</param>
        /// <param name="usableHeight">可用高度（像素，已扣除标题栏与任务栏）</param>
        /// <param name="shrinkHeight">额外让出的高度，用于逃离系统对窗口的压缩</param>
        /// <returns>窗口客户区宽高（像素）</returns>
        public static Vector2Int ComputeWindowSize(int usableWidth, int usableHeight, int shrinkHeight = 0)
        {
            int height = Mathf.Max(MIN_WINDOW_HEIGHT, usableHeight - shrinkHeight);
            int width = Mathf.RoundToInt(height * DesignAspect);

            if (width > usableWidth)
            {
                width = usableWidth;
                height = Mathf.RoundToInt(width / DesignAspect);
            }

            return new Vector2Int(Mathf.Max(1, width), Mathf.Max(1, height));
        }

        // ==================== Private Function ====================
        // 启动时挂到隐藏节点，先于首个场景执行，避免横屏窗口闪一帧
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void bootstrap()
        {
#if !UNITY_EDITOR
            GameObject root = new GameObject(nameof(ScreenAdapter));
            root.AddComponent<ScreenAdapter>();
#endif
        }

        // 取主显示器桌面分辨率，个别平台启动早期取不到时退回当前画面尺寸
        private static Vector2Int desktopResolution()
        {
            Display display = Display.main;

            if (display != null && display.systemWidth > 0 && display.systemHeight > 0)
                return new Vector2Int(display.systemWidth, display.systemHeight);

            return new Vector2Int(Screen.width, Screen.height);
        }

        // 取真正能放窗口的区域：优先 Windows 工作区（已含任务栏扣除），异常时退回估算
        private static Vector2Int usableDesktopSize()
        {
            Vector2Int desktop = desktopResolution();

#if UNITY_STANDALONE_WIN
            if (tryGetWorkArea(out int workWidth, out int workHeight))
                return new Vector2Int(Mathf.Max(MIN_WINDOW_HEIGHT, workWidth),
                                      Mathf.Max(MIN_WINDOW_HEIGHT, workHeight - scaledTitleBarHeight()));
#endif

            return new Vector2Int(Mathf.Max(MIN_WINDOW_HEIGHT, desktop.x - FALLBACK_CHROME_WIDTH),
                                  Mathf.Max(MIN_WINDOW_HEIGHT, desktop.y - FALLBACK_CHROME_HEIGHT));
        }

        // 分平台接管显示参数：编辑器交给 Game 视图，移动端锁方向，PC 锁窗口比例
        private void applyPlatformLayout()
        {
#if UNITY_ANDROID || UNITY_IOS
            Screen.orientation = ScreenOrientation.Portrait;
#elif UNITY_STANDALONE
            m_usableSize = usableDesktopSize();
            m_fitCoroutine = StartCoroutine(fitDesktopWindow());
#endif
        }

        // 申请竖屏窗口并校验结果，被系统压缩时逐级让高重试
        private IEnumerator fitDesktopWindow()
        {
            Vector2Int target = Vector2Int.zero;

            for (int attempt = 0; attempt < MAX_FIT_ATTEMPTS; attempt++)
            {
                target = ComputeWindowSize(m_usableSize.x, m_usableSize.y, m_shrinkStep);
                Screen.fullScreenMode = FullScreenMode.Windowed;
                Screen.SetResolution(target.x, target.y, FullScreenMode.Windowed);

                // SetResolution 要下一帧才反映到 Screen 上，多等一帧再判定，避免误判重试
                yield return null;
                yield return null;

                if (matchesRequest(target))
                {
                    m_fitCoroutine = null;
                    yield break;
                }

                m_shrinkStep += SHRINK_STEP_ON_FAIL;
            }

            // 比例失守才是真故障；比例正确但尺寸没跟上，是系统拒绝改分辨率，属可接受降级
            if (!isAspectRatioOk())
                QLog.Error($"[{nameof(ScreenAdapter)}] 竖屏窗口贴合失败，当前 {Screen.width}x{Screen.height}，请检查 Player Settings 的分辨率设置");
            else
                QLog.Warning($"[{nameof(ScreenAdapter)}] 窗口未能放大到 {target.x}x{target.y}，保持当前 {Screen.width}x{Screen.height}，画面比例仍正确");

            m_fitCoroutine = null;
        }

        // 校验请求是否真正生效：比例对且高度接近申请值，避免停在启动时的默认小窗口
        private bool matchesRequest(Vector2Int requested)
        {
            return isAspectRatioOk() && Mathf.Abs(Screen.height - requested.y) <= WINDOW_SIZE_TOLERANCE;
        }

        // 当前画面宽高比是否与设计竖屏一致
        private bool isAspectRatioOk()
        {
            if (Screen.height <= 0)
                return false;

            float aspect = (float)Screen.width / Screen.height;
            return Mathf.Abs(aspect - DesignAspect) <= DesignAspect * ASPECT_TOLERANCE;
        }

#if UNITY_STANDALONE_WIN
        // 标题栏按系统缩放放大，高 DPI 下才不会低估占用
        private static int scaledTitleBarHeight()
        {
            int dpi = safeSystemDpi();
            return dpi > 96 ? Mathf.CeilToInt(TITLE_BAR_HEIGHT * dpi / 96f) : TITLE_BAR_HEIGHT;
        }

        // 读主显示器工作区，任何异常都返回 false 让调用方走估算分支
        private static bool tryGetWorkArea(out int width, out int height)
        {
            width = 0;
            height = 0;

            try
            {
                WinRect area = new WinRect();
                if (!SystemParametersInfo(SPI_GETWORKAREA, 0, ref area, 0))
                    return false;

                width = area.Right - area.Left;
                height = area.Bottom - area.Top;
                return width > 0 && height > 0;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }

        // GetDpiForSystem 要 Win10 1607 才有，取不到就按 100% 缩放处理
        private static int safeSystemDpi()
        {
            try
            {
                int dpi = GetDpiForSystem();
                return dpi > 0 ? dpi : 96;
            }
            catch (DllNotFoundException)
            {
                return 96;
            }
            catch (EntryPointNotFoundException)
            {
                return 96;
            }
        }
#endif
    }
}
