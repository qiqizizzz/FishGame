/*
* ┌──────────────────────────────────┐
* │  描    述: 钓鱼相机跟随，竖屏正交镜头只沿 y 轴跟随钩子
* │  类    名: FishingCameraRig.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 上边界锁在水面加留白，保证待命时井口与钓竿不入画外；下边界锁在井道底，避免露出场景外
* 横向与纵深一律不动，正交比例恒定，玩家拖拽的像素-米换算才不会随镜头漂移
*/

using UnityEngine;
using Utils.log;

namespace Module.Fish.View
{
    public sealed class FishingCameraRig : MonoBehaviour
    {
        // ==================== 字段[外部设置] ====================
        [Header("引用")]
        [Tooltip("被驱动的相机，留空则取 Main Camera")]
        [SerializeField] private Camera Target;
        [Tooltip("跟随目标，即钩子")]
        [SerializeField] private Transform FollowTarget;

        [Header("构图")]
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

        // ==================== 生命周期 ====================
        private void Awake()
        {
            m_transform = transform;

            if (Target == null)
                Target = Camera.main;

            if (Target == null)
                QLog.Error($"[{nameof(FishingCameraRig)}] 取不到相机，镜头不会跟随钩子");

            if (FollowTarget == null)
                QLog.Error($"[{nameof(FishingCameraRig)}] FollowTarget 未挂载，镜头停在初始高度");
        }

        private void LateUpdate()
        {
            if (Target == null || FollowTarget == null)
                return;

            float halfHeight = Target.orthographicSize;
            float maxY = SurfaceY + LookAheadMeters;
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
    }
}