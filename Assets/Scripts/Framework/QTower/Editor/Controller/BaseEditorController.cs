/*
 * ┌─────────────────────────────────────────────────────────────┐
 * │  描    述: 编辑器控制器基类，统一编辑器模块生命周期与更新驱动
 * │  类    名: BaseEditorController.cs
 * │  创    建: By qiqizizzz
 * └─────────────────────────────────────────────────────────────┘
 */

using System;
using System.Collections.Generic;
using Framework.QTower.Controller;
using UnityEditor;

namespace Framework.QTower.Editor.Controller
{
    public abstract class BaseEditorController : BaseController
    {
        private readonly List<Action> m_editorEvents = new List<Action>();
        private double m_lastEditorUpdateTime;

        protected sealed override void OnInit()
        {
            m_lastEditorUpdateTime = EditorApplication.timeSinceStartup;
            SubscribeEditorCallbacks();
            OnEditorInit();
        }

        protected sealed override void OnDestroy()
        {
            RemoveEditorEvents();
            OnEditorDispose();
            UnsubscribeEditorCallbacks();
        }

        protected virtual void OnEditorInit()
        {
        }

        protected virtual void OnEditorDispose()
        {
        }

        protected virtual void OnSelectionChanged()
        {
        }

        protected virtual void OnUndoRedo()
        {
        }

        protected virtual void OnSceneViewGUI(SceneView sceneView)
        {
        }

        protected virtual void OnBeforeReload()
        {
        }

        protected void RegisterEvent(Action<Action> registerEvent, Action<Action> unregisterEvent, Action callback)
        {
            registerEvent(callback);
            m_editorEvents.Add(() => unregisterEvent(callback));
        }

        protected void RegisterEvent<TEvent>(Action<Action<TEvent>> registerEvent,
            Action<Action<TEvent>> unregisterEvent, Action<TEvent> callback)
        {
            registerEvent(callback);
            m_editorEvents.Add(() => unregisterEvent(callback));
        }

        // 注册双参数 View 事件并统一记录反注册操作
        protected void RegisterEvent<TFirstEvent, TSecondEvent>(Action<Action<TFirstEvent, TSecondEvent>> registerEvent,
            Action<Action<TFirstEvent, TSecondEvent>> unregisterEvent, Action<TFirstEvent, TSecondEvent> callback)
        {
            registerEvent(callback);
            m_editorEvents.Add(() => unregisterEvent(callback));
        }

        private void SubscribeEditorCallbacks()
        {
            EditorApplication.update += UpdateEditorTick;
            Selection.selectionChanged += OnSelectionChanged;
            Undo.undoRedoPerformed += OnUndoRedo;
            SceneView.duringSceneGui += OnSceneViewGUI;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
        }

        private void UnsubscribeEditorCallbacks()
        {
            EditorApplication.update -= UpdateEditorTick;
            Selection.selectionChanged -= OnSelectionChanged;
            Undo.undoRedoPerformed -= OnUndoRedo;
            SceneView.duringSceneGui -= OnSceneViewGUI;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeReload;
        }

        private void UpdateEditorTick()
        {
            double currentTime = EditorApplication.timeSinceStartup;
            float deltaTime = (float)(currentTime - m_lastEditorUpdateTime);
            m_lastEditorUpdateTime = currentTime;
            Tick(deltaTime);
        }

        private void RemoveEditorEvents()
        {
            for (int i = m_editorEvents.Count - 1; i >= 0; i--)
                m_editorEvents[i].Invoke();

            m_editorEvents.Clear();
        }
    }
}
