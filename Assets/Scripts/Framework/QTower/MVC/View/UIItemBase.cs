/*
 * ┌──────────────────────────────────┐
 * │  描    述: UI 子项基类，提供节点缓存与显隐控制
 * │  类    名: UIItemBase.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 */

using System.Collections.Generic;
using UnityEngine;

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

        // 获取并缓存子节点组件
        protected T FindComponent<T>(string path) where T : Component
        {
            return FindNode(path).GetComponent<T>();
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
