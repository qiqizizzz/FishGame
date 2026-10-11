/*
 * ┌──────────────────────────────────┐
 * │  描    述: 钓鱼相机跟随，竖屏正交镜头只沿 y 轴跟随钩子
 * │  类    名: FishingCameraRig.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 * 上边界锁在水面加留白，保证待命时井口与钓竿不入画外；下边界锁在井道底，避免露出场景外
 * 横向与纵深一律不动，正交比例恒定，玩家拖拽的像素-米换算才不会随镜头漂移
 * 竖屏适配：屏幕比设计比例更窄时等比抬高正交尺寸，横向可视宽度不被裁切，多出来的高度往下探井道，
 * 水面一侧保持顶边锚定，鱼群生成范围与拖拽手感在 9:16 与 9:20 之间保持一致
 */

using UnityEngine;
using Utils.log;

namespace Module.Fish.View
{
    public sealed class FishingCameraRig : MonoBehaviour
    {
        // ==================== 常量与静态字段 ====================
        // 正交尺寸低于该差值就不重设，避免浮点抖动每帧改相机
        private const float ORTHO_SIZE_EPSILON = 0.0001f;

        // ==================== 字段[外部设置] ====================
        [Header("引用")]
        [Tooltip("被驱动的相机，留空则取 Main Camera")]
        [SerializeField] private Camera Target;
        [Tooltip("跟随目标，即钩子")]
        [SerializeField] private Transform FollowTarget;

        [Header("构图")]
        [Tooltip("设计分辨率（宽 x 高），需与 UIRoot 的 CanvasScaler 参考分辨率一致；比它更窄的屏等比增高，保证横向可视宽度不被裁切")]
        [SerializeField] private Vector2 DesignResolution = new Vector2(1080f, 1920f);
        [Tooltip("目标上方留白（米），决定钩子落在屏幕偏下位置")]
        [SerializeField] private float LookAheadMeters = 2.05f;
        [Tooltip("水面高度（世界 y）")]
        [SerializeField] private float SurfaceY;
        [Tooltip("垂直跟随的收敛系数，越大越跟手")]
        [SerializeField] private float FollowDamping = 5f;

        // ==================== 属性 ====================
        /// <summary>相机可视横向半宽（米），鱼群游动范围据此标定</summary>
        public float ViewHalfWidth => Target != null ? Target.orthographicSize * Target.aspect : 0f;

        // ==================== 字段[私有] ====================
        private Transform m_transform;
        private float m_shaftDepth;
        private float m_baseOrthoSize;

        // ==================== 生命周期 ====================
        private void Awake()
        {
            m_transform = transform;

            if (Target == null)
                Target = Camera.main;

            if (Target == null)
            {
                QLog.Error($"[{nameof(FishingCameraRig)}] 取不到相机，镜头不会跟随钩子");
                return;
            }

            if (!Target.orthographic)
            {
                QLog.Error($"[{nameof(FishingCameraRig)}] 相机不是正交模式，竖屏构图适配已跳过");
                return;
            }

            // 以场景里配好的正交尺寸为基准，之后的比例补偿全部围绕它算
            m_baseOrthoSize = Target.orthographicSize;

            if (FollowTarget == null)
                QLog.Error($"[{nameof(FishingCameraRig)}] FollowTarget 未挂载，镜头停在初始高度");

            // 开局先把比例补到位，保证第一帧读到的 ViewHalfWidth 已经是适配后的值
            fitOrthographicSize();
        }

        private void LateUpdate()
        {
            if (Target == null || FollowTarget == null)
                return;

            fitOrthographicSize();

            float halfHeight = Target.orthographicSize;

            // 屏幕变窄时正交尺寸会被抬高，这里反向扣掉多出的半高，让画面上沿仍锁在同一条线
            float maxY = SurfaceY + LookAheadMeters + m_baseOrthoSize - halfHeight;
            float minY = m_shaftDepth > 0f ? -m_shaftDepth + halfHeight : maxY;
            float wantedY = Mathf.Clamp(FollowTarget.position.y + LookAheadMeters, Mathf.Min(minY, maxY), maxY);

            Vector3 position = m_transform.position;
            position.y = Mathf.Lerp(position.y, wantedY, 1f - Mathf.Exp(-FollowDamping * Time.deltaTime));
            m_transform.position = position;
        }

        // ==================== Public Function ====================
        /// <summary>
        /// 注入井道总深度以确定镜头下边界，由控制器开局时调用
        /// </summary>
        public void Configure(float shaftDepth)
        {
            m_shaftDepth = shaftDepth;
        }

        // ==================== Private Function ====================
        // 按设计宽高比补正交尺寸：更窄的屏等比增高，宽屏保持原尺寸不额外拉伸
        private void fitOrthographicSize()
        {
            if (m_baseOrthoSize <= 0f || Target == null || !Target.orthographic)
                return;

            if (DesignResolution.y <= 0f || DesignResolution.x <= 0f)
                return;

            float designAspect = DesignResolution.x / DesignResolution.y;

            // aspect 小于设计值说明横向被挤窄，抬高正交尺寸把可视半宽还原成设计基准
            float fit = Target.aspect < designAspect ? designAspect / Target.aspect : 1f;
            float wantedSize = m_baseOrthoSize * fit;

            if (Mathf.Abs(wantedSize - Target.orthographicSize) > ORTHO_SIZE_EPSILON)
                Target.orthographicSize = wantedSize;
        }
    }
}