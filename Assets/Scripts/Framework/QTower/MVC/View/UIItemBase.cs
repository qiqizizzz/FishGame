/*
 * ┌──────────────────────────────────┐
 * │  描    述: UI 子项基类，提供节点缓存与显隐控制
 * │  类    名: UIItemBase.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 */

using System.Collections.Generic;
using UnityEngine;
using Utils.log;

namespace Framework.QTower.View
{
    public abstract class UIItemBase : MonoBehaviour
    {
        private readonly Dictionary<string, Transform> m_nodes = new();

        // 获取并缓存子节点
        protected Transform FindNode(string path)
        {
            if (m_nodes.TryGetValue(path, out Transform node))
                return node;

            node = transform.Find(path);
            m_nodes.Add(path, node);
            return node;
        }

        // 获取并缓存子节点组件，节点或组件缺失时在编辑器下主动报错
        protected T FindComponent<T>(string path) where T : Component
        {
            Transform node = FindNode(path);
            if (node == null)
            {
                QLog.Error($"[{GetType().Name}] 子节点未找到：{path}，请检查层级路径");
                return null;
            }

            T component = node.GetComponent<T>();
            if (component == null)
                QLog.Error($"[{GetType().Name}] 节点 {path} 上未挂载 {typeof(T).Name}");
            return component;
        }

        // 设置子项显示状态
        public virtual void SetVisible(bool isVisible)
        {
            if (gameObject.activeSelf == isVisible)
                return;

            gameObject.SetActive(isVisible);
        }

        protected virtual void OnDestroy()
        {
            m_nodes.Clear();
        }
    }
}
