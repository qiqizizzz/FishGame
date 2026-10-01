/*
 * ┌──────────────────────────────────┐
 * │  描    述: UI 管理器，负责 UI 视图注册、加载、缓存、显示、隐藏与关闭
 * │  类    名: UIManager.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 */

using System;
using System.Collections.Generic;
using Common.Res;
using Cysharp.Threading.Tasks;
using Framework.QTower.Controller;
using Framework.QTower.View;
using UnityEngine;
using Utils.log;

namespace Framework.QTower
{
    public sealed class UIManager
    {
        private readonly Dictionary<Type, UIData> m_viewDatas = new();
        private readonly Dictionary<Type, UIBase> m_viewCache = new();

        private bool m_isDestroyed;

        public Transform UIRoot { get; private set; }

        public bool Register<TView>(UIData uiData)
            where TView : UIBase
        {
            if (uiData == null)
            {
                QLog.Error("注册 UI 失败：UIData 为空");
                return false;
            }

            if (string.IsNullOrEmpty(uiData.Address))
            {
                QLog.Error("注册 UI 失败：Address 不能为空");
                return false;
            }

            if (uiData.Parent == null)
            {
                QLog.Error($"注册 UI 失败：{uiData.Address} 未设置 Parent");
                return false;
            }

            if (uiData.Controller == null)
            {
                QLog.Error($"注册 UI 失败：{uiData.Address} 未设置 Controller");
                return false;
            }

            Type viewClass = typeof(TView);
            if (m_viewDatas.ContainsKey(viewClass))
            {
                QLog.Error($"注册 UI 失败：{viewClass.Name} 已注册");
                return false;
            }

            foreach (UIData registeredData in m_viewDatas.Values)
            {
                if (registeredData.Address != uiData.Address)
                    continue;

                QLog.Error($"注册 UI 失败：{uiData.Address} 已绑定到其他 UI 类型");
                return false;
            }

            m_viewDatas.Add(viewClass, uiData);
            return true;
        }

        public async UniTask<TView> Open<TView>(params object[] args) where TView : UIBase
        {
            if (m_isDestroyed)
            {
                QLog.Error($"打开 UI 失败：UIManager 已销毁，目标 {typeof(TView).Name}");
                return null;
            }

            Type viewClass = typeof(TView);
            if (!m_viewDatas.TryGetValue(viewClass, out UIData viewData))
            {
                QLog.Error($"打开 UI 失败：{viewClass.Name} 未注册");
                return null;
            }

            if (m_viewCache.TryGetValue(viewClass, out UIBase cachedView))
            {
                TView view = cachedView as TView;
                if (view == null)
                {
                    QLog.Error($"打开 UI 失败：{viewClass.Name} 缓存类型不匹配");
                    return null;
                }

                if (!view.gameObject.activeSelf)
                {
                    view.Open(args);
                    view.Controller?.OnViewOpened(view);
                }

                return view;
            }

            GameObject viewObject = await ResManager.InstantiateAsync(viewData.Address, viewData.Parent);
            if (m_isDestroyed)
            {
                if (viewObject != null)
                    ResManager.UnLoadInstance(viewObject);

                return null;
            }

            if (viewObject == null)
            {
                QLog.Error($"打开 UI 失败：{viewData.Address} 资源加载失败");
                return null;
            }

            TView loadedView = viewObject.GetComponent<TView>();
            if (loadedView == null)
                loadedView = viewObject.AddComponent<TView>();

            Canvas canvas = viewObject.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = viewData.SortingOrder;
            }

            loadedView.Controller = viewData.Controller;
            loadedView.Init();
            m_viewCache.Add(viewClass, loadedView);
            loadedView.Controller?.OnViewLoaded(loadedView);
            loadedView.Open(args);
            loadedView.Controller?.OnViewOpened(loadedView);
            return loadedView;
        }

        public void Hide<TView>() where TView : UIBase
        {
            if (!m_viewCache.TryGetValue(typeof(TView), out UIBase view))
                return;

            view.Hide();
        }

        public void Close<TView>(params object[] args) where TView : UIBase
        {
            if (!m_viewCache.TryGetValue(typeof(TView), out UIBase view))
                return;

            view.Close(args);
            view.Controller?.OnViewClosed(view);
        }

        public void UnregisterByController(BaseController controller)
        {
            List<Type> viewClasses = new();
            foreach (KeyValuePair<Type, UIData> pair in m_viewDatas)
            {
                if (pair.Value.Controller == controller)
                    viewClasses.Add(pair.Key);
            }

            foreach (Type viewClass in viewClasses)
            {
                if (m_viewCache.TryGetValue(viewClass, out UIBase view))
                {
                    m_viewCache.Remove(viewClass);
                    ReleaseViewInstance(view);
                }

                m_viewDatas.Remove(viewClass);
            }
        }

        public void Destroy()
        {
            m_isDestroyed = true;
            foreach (UIBase view in m_viewCache.Values)
                ReleaseViewInstance(view);

            m_viewCache.Clear();
            m_viewDatas.Clear();
        }

        // 释放仍然有效的 UI 实例，跳过已被 Unity 外部销毁的对象
        private void ReleaseViewInstance(UIBase view)
        {
            if (view == null)
                return;

            GameObject viewObject = view.gameObject;
            if (viewObject == null)
                return;

            ResManager.UnLoadInstance(viewObject);
        }

        public void SetRoot(Transform uiRoot)
        {
            if (uiRoot == null)
            {
                QLog.Error("设置 UI 根节点失败：UIRoot 为空");
                return;
            }

            UIRoot = uiRoot;
        }
    }
}
