/*
* ┌──────────────────────────────────┐
* │  描    述: 钩子表现对象，充当状态机上下文并把运行时坐标写回变换
* │  类    名: HookViewController.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 深度以水面为 0、向下为正，与父节点局部空间的关系是 localPosition.y = -Depth
* 位移完全由 HookState 决定，本类不做任何玩法判定；配置与属性包统一由控制器注入
*/

using Module.Fish.Data;
using Module.Fish.Model;
using Module.Fish.Type;
using UnityEngine;
using Utils.log;

namespace Module.Fish.View
{
    public sealed class HookViewController : MonoBehaviour, IHookRuntime
    {
        // ==================== 字段[外部设置] ====================
        [Header("井口")]
        [Tooltip("井口锚点（父节点局部空间），待命与回收都落回这里，z 为钩子所在纵深平面")]
        [SerializeField] private Vector3 HoleAnchorPosition;

        [Header("输入标定")]
        [Tooltip("每屏幕像素折算的世界米数，大于 0 时覆盖自动换算")]
        [SerializeField, Min(0f)] private float PixelsToMetersOverride;

        [Header("捕获判定")]
        [Tooltip("实际参与捕获判定的钩尖节点，留空时自动查找名为 Lure 的子节点")]
        [SerializeField] private Transform HookTip;

        // ==================== 属性 ====================
        public HookConfigSO HookConfig => m_hookConfig;

        public IFishingStats Stats => m_stats;

        public IFishingEncounter Encounter => m_encounter;

        public float ShaftDepth => m_session != null ? m_session.ShaftDepth : 0f;

        public float ShaftHalfWidth => m_session != null ? m_session.ShaftHalfWidth : 0f;

        public float MaxDepth => Mathf.Min(ShaftDepth, m_stats != null ? m_stats.LineLength : 0f);

        public float Depth => -m_transform.localPosition.y;

        public float Horizontal => m_transform.localPosition.x;

        public float CaptureHorizontal => Horizontal + m_captureOffset.x;

        public float CaptureDepth => Depth - m_captureOffset.y;

        public Vector3 HolePosition => HoleAnchorPosition;

        public float LateralVelocity { get; set; }

        public float LateralInput { get; private set; }

        public bool IsPressing => m_isPressing;

        /// <summary>钩子当前世界坐标，鱼线与特效取这里</summary>
        public Vector3 WorldPosition => m_transform.position;

        // ==================== 字段[私有] ====================
        private Transform m_transform;
        private HookConfigSO m_hookConfig;
        private FishingSessionConfigSO m_session;
        private IFishingStats m_stats;
        private IFishingEncounter m_encounter;
        private HookStateMachine m_machine;
        private float m_pixelsToMeters = 1f;
        private Vector3 m_captureOffset;
        private bool m_isPressing;

        // ==================== 生命周期 ====================
        private void Awake()
        {
            m_transform = transform;
            m_pixelsToMeters = setupPixelsToMeters();
            m_captureOffset = setupCaptureOffset();
        }

        // ==================== Public Function ====================
        /// <summary>
        /// 注入配置、属性包与状态机，全部引用由 FishingScene 上的配置统一提供
        /// </summary>
        public void Initialize(HookConfigSO hookConfig, FishingSessionConfigSO session, IFishingStats stats, HookStateMachine machine, IFishingEncounter encounter)
        {
            m_hookConfig = hookConfig;
            m_session = session;
            m_stats = stats;
            m_machine = machine;
            m_encounter = encounter;

            if (hookConfig == null)
                QLog.Error($"[{nameof(HookViewController)}] 钓竿配置未注入，下潜与横向速度会取不到");

            if (session == null)
                QLog.Error($"[{nameof(HookViewController)}] 会话配置未注入，井道边界会退化为 0");

            if (stats == null)
                QLog.Error($"[{nameof(HookViewController)}] 属性包未注入，钩子无法取到线长上限");

            if (machine == null)
                QLog.Error($"[{nameof(HookViewController)}] 状态机未绑定，钩子不会响应输入");

            if (encounter == null)
                QLog.Error($"[{nameof(HookViewController)}] 接触裁决未注入，碰鱼不会有任何反馈");
        }

        /// <summary>
        /// 每帧喂入玩家意图：按住状态与本帧横向拖拽的屏幕像素位移
        /// </summary>
        public void FeedInput(bool isPressing, float dragDeltaPixels)
        {
            m_isPressing = isPressing;
            LateralInput = isPressing ? dragDeltaPixels * m_pixelsToMeters : 0f;
        }

        public void MoveTo(float x, float depth)
        {
            m_transform.localPosition = new Vector3(x, -depth, HoleAnchorPosition.z);
        }

        public void TransitionTo(HookPhase phase)
        {
            if (m_machine == null)
            {
                QLog.Error($"[{nameof(HookViewController)}] 状态机未绑定，忽略迁移到 {phase}");
                return;
            }

            m_machine.Change(phase, this);
        }

        // 竖屏正交镜头下，横向像素到世界米数与纵向同比例，按相机高度换算即可
        // 以钩尖模型最低点作为捕获点，避免根节点中心仍高于鱼身时视觉上已经钩中但判定未触发
        // 偏移必须换算到父级空间：Depth 与 Horizontal 取的是 localPosition，父级缩放与钩子缩放都不能漏掉
        private Vector3 setupCaptureOffset()
        {
            if (HookTip == null)
                HookTip = transform.Find("Lure");

            if (HookTip == null)
            {
#if UNITY_EDITOR
                Debug.LogError($"[{nameof(HookViewController)}] 未找到 Lure 子节点，捕获点退回钩子根节点");
#endif
                return Vector3.zero;
            }

            Renderer[] renderers = HookTip.GetComponentsInChildren<Renderer>(true);

            if (renderers.Length == 0)
            {
#if UNITY_EDITOR
                Debug.LogError($"[{nameof(HookViewController)}] Lure 下未找到 Renderer，捕获点退回钩子根节点");
#endif
                return Vector3.zero;
            }

            Bounds bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            Vector3 hookTipPoint = new Vector3(
                (bounds.min.x + bounds.max.x) * 0.5f,
                bounds.min.y,
                bounds.center.z);

            Vector3 tipInParent = transform.parent != null
                ? transform.parent.InverseTransformPoint(hookTipPoint)
                : hookTipPoint;

            return tipInParent - m_transform.localPosition;
        }

        private float setupPixelsToMeters()
        {
            if (PixelsToMetersOverride > 0f)
                return PixelsToMetersOverride;

            Camera viewCamera = Camera.main;

            if (viewCamera == null || viewCamera.pixelHeight <= 0)
            {
                QLog.Error($"[{nameof(HookViewController)}] 取不到 Main Camera，横向拖动退化为 1 像素 1 米");
                return 1f;
            }

            return 2f * viewCamera.orthographicSize / viewCamera.pixelHeight;
        }
    }
}