/*
 * ┌──────────────────────────────────┐
 * │  描    述: 编辑器 UI 视图基类，统一编辑器 UI 生命周期与事件订阅
 * │  类    名: UIBaseEditor.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 */

namespace Framework.QTower.Editor.View
{
    public abstract class UIBaseEditor
    {
        private bool m_isInitialized;

        public void Init()
        {
            if (m_isInitialized)
                return;

            OnEditorInit();
            SubscribeViewEvents();
            m_isInitialized = true;
        }

        public void Destroy()
        {
            if (!m_isInitialized)
                return;

            UnsubscribeViewEvents();
            OnEditorDispose();
            m_isInitialized = false;
        }

        protected virtual void OnEditorInit()
        {
        }

        protected virtual void OnEditorDispose()
        {
        }

        protected virtual void SubscribeViewEvents()
        {
        }

        protected virtual void UnsubscribeViewEvents()
        {
        }
    }
}
