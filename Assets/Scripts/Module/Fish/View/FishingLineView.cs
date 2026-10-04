/*
* ┌──────────────────────────────────┐
* │  描    述: 鱼线绘制，把钓竿挂载点与钩子挂载点连成一条线
* │  类    名: FishingLineView.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 用 LineRenderer 两点直连，线宽取钓竿配置的 LineWidth，松塌与抖动留给后续表现打磨
*/

using Module.Fish.Data;
using UnityEngine;
using UnityEngine.Rendering;
using Utils.log;

namespace Module.Fish.View
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class FishingLineView : MonoBehaviour
    {
        private const float DEFAULT_LINE_WIDTH = 0.05f;

        // ==================== 字段[外部设置] ====================
        [Header("端点")]
        [Tooltip("鱼线在钓竿上的挂载点")]
        [SerializeField] private Transform RodAnchor;
        [Tooltip("鱼线在钩子上的挂载点")]
        [SerializeField] private Transform HookAnchor;

        [Header("配置")]
        [SerializeField] private HookConfigSO HookConfigAsset;

        // ==================== 字段[私有] ====================
        private LineRenderer m_line;

        // ==================== 生命周期 ====================
        private void Awake()
        {
            m_line = GetComponent<LineRenderer>();

            if (RodAnchor == null)
                QLog.Error($"[{nameof(FishingLineView)}] RodAnchor 未挂载，鱼线不会绘制");

            if (HookAnchor == null)
                QLog.Error($"[{nameof(FishingLineView)}] HookAnchor 未挂载，鱼线不会绘制");

            if (HookConfigAsset == null)
                QLog.Error($"[{nameof(FishingLineView)}] 未配置钓竿参数 HookConfigSO，鱼线回退默认宽度");

            m_line.positionCount = 2;
            m_line.useWorldSpace = true;
            m_line.startWidth = 1f;
            m_line.endWidth = 1f;
            m_line.widthMultiplier = HookConfigAsset != null ? HookConfigAsset.LineWidth : DEFAULT_LINE_WIDTH;
            m_line.numCapVertices = 2;
            m_line.receiveShadows = false;
            m_line.shadowCastingMode = ShadowCastingMode.Off;
            m_line.lightProbeUsage = LightProbeUsage.Off;
            m_line.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private void LateUpdate()
        {
            // 端点缺失已在 Awake 报过，这里静默跳过避免每帧刷屏
            if (RodAnchor == null || HookAnchor == null)
                return;

            m_line.SetPosition(0, RodAnchor.position);
            m_line.SetPosition(1, HookAnchor.position);
        }
    }
}