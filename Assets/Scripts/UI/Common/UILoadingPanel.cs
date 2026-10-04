/*
* ┌──────────────────────────────────┐
* │  描    述: 切场景加载遮罩，纯黑底加转圈圈与加载中文案
* │  类    名: UILoadingPanel.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* Single 模式切场景时，旧场景已销毁而新场景相机还没出画，中间会闪一帧默认天空盒
* 遮罩由 GameController 在 LoadSceneAsync 之前打开、加载完再等两帧关闭，正好盖住这段空窗
*/

using Framework.QTower.View;
using UnityEngine;
using Utils.log;

namespace UI.Common
{
    public sealed class UILoadingPanel : UIBase
    {
        private const string RING_PATH = "Img_Ring";
        private const float SPIN_DEGREES_PER_SECOND = 240f;

        private RectTransform m_ringRect;
        private float m_rotation;

        protected override void OnInit()
        {
            m_ringRect = FindNode(RING_PATH) as RectTransform;
            m_rotation = 0f;

#if UNITY_EDITOR
            if (m_ringRect == null)
                QLog.Error($"[{nameof(UILoadingPanel)}] {RING_PATH} 未找到，转圈不会动");
#endif
        }

        protected override void OnOpen(params object[] args)
        {
            m_rotation = 0f;
        }

        private void Update()
        {
            // 转圈只是装饰，缺精灵也不该挡住建挡黑屏
            if (m_ringRect == null)
                return;

            m_rotation -= SPIN_DEGREES_PER_SECOND * Time.unscaledDeltaTime;
            m_ringRect.localEulerAngles = new Vector3(0f, 0f, m_rotation);
        }
    }
}