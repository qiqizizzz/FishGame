/*
* ┌──────────────────────────────────┐
* │  描    述: 资源加载/卸载管理器                      
* │  类    名: ResManager.cs       
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 对于特效、怪物、道具等重复使用的对象使用对象池,UI界面则由UI界面那边管理
*/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Utils.log;

namespace Common.Res
{
    public static class ResManager
    {
        private class CacheEntry
        {
            public AsyncOperationHandle handle;
            public int refCount;
        }

        #region 路径
        private const string RES_POOL_ROOT_NAME = "ResPoolRoot";
        private const string ADDRESSABLES_POOL_ROOT_NAME = "AddressablesPool";
        private const string PREFAB_POOL_ROOT_NAME = "PrefabPool";
        #endregion

        private static readonly Dictionary<string, CacheEntry> caches;
        private static readonly Dictionary<string, Queue<GameObject>> _pool;
        private static readonly Dictionary<GameObject, Queue<GameObject>> _prefabPool;
        private static Transform m_resPoolRoot;
        private static Transform m_addressablesPoolRoot;
        private static Transform m_prefabPoolRoot;

        static ResManager()
        {
            caches = new Dictionary<string, CacheEntry>();
            _pool = new Dictionary<string, Queue<GameObject>>();
            _prefabPool = new Dictionary<GameObject, Queue<GameObject>>();
        }

        // 清理资源管理器持有的全部资源
        public static void Destroy()
        {
            ClearAllPools();
            ClearAllPrefabPools();
            ReleaseAllAssets();
            DestroyPoolRoot();
        }

        //同步加载实例
        public static GameObject Instantiate(string keyName, Transform parent = null)
        {
            GameObject go = Addressables.InstantiateAsync(keyName, parent).WaitForCompletion();
            go.name = keyName;
            return go;
        }

        // 异步加载实例
        public static async UniTask<GameObject> InstantiateAsync(string path, Transform parent = null)
        {
            if (string.IsNullOrEmpty(path))
            {
                QLog.Error("异步实例化失败：资源地址为空");
                return null;
            }

            AsyncOperationHandle<GameObject> handle = Addressables.InstantiateAsync(path, parent);
            try
            {
                await handle.ToUniTask();

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    handle.Result.name = path;
                    return handle.Result;
                }

                if (handle.IsValid())
                    Addressables.Release(handle);

                QLog.Error($"异步实例化失败：{path}");
                return null;
            }
            catch (Exception exception)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);

                QLog.Error($"异步实例化失败：{path}，{exception.Message}");
                return null;
            }
        }

        //卸载实例
        public static bool UnLoadInstance(GameObject go)
        {
            return Addressables.ReleaseInstance(go);
        }

        #region Asset 缓存
        // 同步获取资源句柄并增加引用计数
        private static AsyncOperationHandle AcquireAssetSync<T>(string path) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(path))
            {
                QLog.Error("同步加载资源失败：资源地址为空");
                return default;
            }

            if (caches.TryGetValue(path, out CacheEntry cache))
            {
                cache.refCount++;
                cache.handle.WaitForCompletion();

                if (cache.handle.Status == AsyncOperationStatus.Succeeded)
                    return cache.handle;

                ReleaseFailedCache(path, cache);
                QLog.Error($"同步加载资源失败：{path}");
                return default;
            }

            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(path);
            cache = new CacheEntry
            {
                handle = handle,
                refCount = 1
            };
            caches.Add(path, cache);
            handle.WaitForCompletion();

            if (handle.Status == AsyncOperationStatus.Succeeded)
                return handle;

            ReleaseFailedCache(path, cache);
            QLog.Error($"同步加载资源失败：{path}");
            return default;
        }

        // 异步获取资源句柄并增加引用计数
        private static async UniTask<AsyncOperationHandle> AcquireAssetAsync<T>(string path) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(path))
            {
                QLog.Error("异步加载资源失败：资源地址为空");
                return default;
            }

            if (caches.TryGetValue(path, out CacheEntry cache))
            {
                cache.refCount++;
                try
                {
                    await cache.handle.ToUniTask();

                    if (cache.handle.Status == AsyncOperationStatus.Succeeded)
                        return cache.handle;

                    ReleaseFailedCache(path, cache);
                    QLog.Error($"异步加载资源失败：{path}");
                    return default;
                }
                catch (Exception exception)
                {
                    ReleaseFailedCache(path, cache);
                    QLog.Error($"异步加载资源失败：{path}，{exception.Message}");
                    return default;
                }
            }

            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(path);
            cache = new CacheEntry
            {
                handle = handle,
                refCount = 1
            };
            caches.Add(path, cache);

            try
            {
                await handle.ToUniTask();

                if (handle.Status == AsyncOperationStatus.Succeeded)
                    return handle;

                ReleaseFailedCache(path, cache);
                QLog.Error($"异步加载资源失败：{path}");
                return default;
            }
            catch (Exception exception)
            {
                ReleaseFailedCache(path, cache);
                QLog.Error($"异步加载资源失败：{path}，{exception.Message}");
                return default;
            }
        }

        // 同步加载指定地址的资源
        public static T LoadAsset<T>(string path) where T : UnityEngine.Object
        {
            AsyncOperationHandle handle = AcquireAssetSync<T>(path);
            return handle.IsValid() ? handle.Result as T : null;
        }

        // 异步加载指定地址的资源
        public static async UniTask<T> LoadAssetAsync<T>(string path) where T : UnityEngine.Object
        {
            AsyncOperationHandle handle = await AcquireAssetAsync<T>(path);
            return handle.IsValid() ? handle.Result as T : null;
        }

        // 预加载指定地址的资源
        public static async UniTask PreLoadAssets(List<string> paths)
        {
            foreach (string path in paths)
                await AcquireAssetAsync<UnityEngine.Object>(path);
        }

        // 释放一次指定地址的资源引用
        public static void ReleaseAsset(string path)
        {
            if (!caches.TryGetValue(path, out CacheEntry cache))
            {
                QLog.Warning($"释放资源失败：未找到资源缓存 {path}");
                return;
            }

            cache.refCount--;
            if (cache.refCount > 0)
                return;

            if (cache.handle.IsValid())
                Addressables.Release(cache.handle);

            caches.Remove(path);
        }

        // 回滚一次失败的资源获取
        private static void ReleaseFailedCache(string path, CacheEntry cache)
        {
            if (!caches.TryGetValue(path, out CacheEntry cachedEntry) || cachedEntry != cache)
                return;

            cache.refCount--;
            if (cache.refCount > 0)
                return;

            if (cache.handle.IsValid())
                Addressables.Release(cache.handle);

            caches.Remove(path);
        }

        // 释放全部已缓存的资源
        private static void ReleaseAllAssets()
        {
            foreach (CacheEntry cache in caches.Values)
            {
                if (cache.handle.IsValid())
                    Addressables.Release(cache.handle);
            }

            caches.Clear();
        }
        #endregion

        #region 对象池
        //同步从对象池加载实例
        public static GameObject InstantiateFromPool(string keyName, Transform parent = null)
        {
            if (_pool.ContainsKey(keyName))
            {
                while (_pool[keyName].Count > 0)
                {
                    GameObject obj = _pool[keyName].Dequeue();
                    if (obj == null) continue;

                    obj.transform.SetParent(parent);
                    obj.SetActive(true);
                    return obj;
                }
            }

            return Instantiate(keyName, parent);
        }
        
        // 异步从对象池加载实例
        public static async UniTask<GameObject> InstantiateFromPoolAsync(string path, Transform parent = null)
        {
            if (_pool.TryGetValue(path, out Queue<GameObject> pool))
            {
                while (pool.Count > 0)
                {
                    GameObject obj = pool.Dequeue();
                    if (obj == null) continue;

                    obj.transform.SetParent(parent);
                    obj.SetActive(true);
                    return obj;
                }
            }

            return await InstantiateAsync(path, parent);
        }
        
        //释放实例到对象池
        public static void ReleaseToPool(string keyName, GameObject obj, int maxPoolSize = 20)
        {
            if (obj == null) return;

            obj.SetActive(false);

            if (!_pool.ContainsKey(keyName))
                _pool[keyName] = new Queue<GameObject>();

            if (_pool[keyName].Count < maxPoolSize)
            {
                EnsurePoolRoots();
                obj.transform.SetParent(m_addressablesPoolRoot);
                _pool[keyName].Enqueue(obj);
            }
            else
            {
                // 超出池容量，直接释放
                Addressables.ReleaseInstance(obj);
            }
        }
        
        //清理单个对象池
        public static void ClearPool(string keyName)
        {
            if (_pool.TryGetValue(keyName, out var queue))
            {
                while (queue.Count > 0)
                {
                    var obj = queue.Dequeue();
                    if (obj != null)
                        Addressables.ReleaseInstance(obj);
                }
                _pool.Remove(keyName);
            }
        }
        
        //清理所有对象池
        public static void ClearAllPools()
        {
            foreach (var key in new List<string>(_pool.Keys))
                ClearPool(key);
        }
        #endregion

        #region 预制体对象池
        //同步从预制体对象池加载实例
        public static GameObject InstantiateFromPool(GameObject prefab, Transform parent = null)
        {
            if (prefab == null) return null;

            if (_prefabPool.ContainsKey(prefab))
            {
                while (_prefabPool[prefab].Count > 0)
                {
                    GameObject obj = _prefabPool[prefab].Dequeue();
                    if (obj == null) continue;

                    obj.transform.SetParent(parent);
                    obj.SetActive(true);
                    return obj;
                }
            }

            GameObject go = UnityEngine.Object.Instantiate(prefab, parent);
            go.name = prefab.name;
            return go;
        }

        //释放实例到预制体对象池
        public static void ReleaseToPool(GameObject prefab, GameObject obj, int maxPoolSize = 20)
        {
            if (prefab == null || obj == null) return;

            obj.SetActive(false);

            if (!_prefabPool.ContainsKey(prefab))
                _prefabPool[prefab] = new Queue<GameObject>();

            if (_prefabPool[prefab].Count < maxPoolSize)
            {
                EnsurePoolRoots();
                obj.transform.SetParent(m_prefabPoolRoot);
                _prefabPool[prefab].Enqueue(obj);
            }
            else
            {
                UnityEngine.Object.Destroy(obj);
            }
        }

        //清理单个预制体对象池
        public static void ClearPrefabPool(GameObject prefab)
        {
            if (prefab == null) return;
            if (_prefabPool.TryGetValue(prefab, out var queue))
            {
                while (queue.Count > 0)
                {
                    var obj = queue.Dequeue();
                    if (obj != null)
                        UnityEngine.Object.Destroy(obj);
                }
                _prefabPool.Remove(prefab);
            }
        }

        //清理所有预制体对象池
        public static void ClearAllPrefabPools()
        {
            foreach (var key in new List<GameObject>(_prefabPool.Keys))
                ClearPrefabPool(key);
        }
        #endregion

        #region 跨场景根节点

        // 创建并缓存对象池的跨场景根节点
        private static void EnsurePoolRoots()
        {
            if (m_resPoolRoot != null)
                return;

            GameObject rootObject = new GameObject(RES_POOL_ROOT_NAME);
            UnityEngine.Object.DontDestroyOnLoad(rootObject);
            m_resPoolRoot = rootObject.transform;

            GameObject addressablesPoolObject = new GameObject(ADDRESSABLES_POOL_ROOT_NAME);
            addressablesPoolObject.transform.SetParent(m_resPoolRoot);
            m_addressablesPoolRoot = addressablesPoolObject.transform;

            GameObject prefabPoolObject = new GameObject(PREFAB_POOL_ROOT_NAME);
            prefabPoolObject.transform.SetParent(m_resPoolRoot);
            m_prefabPoolRoot = prefabPoolObject.transform;
        }

        // 销毁对象池的跨场景根节点
        private static void DestroyPoolRoot()
        {
            if (m_resPoolRoot == null)
                return;

            UnityEngine.Object.Destroy(m_resPoolRoot.gameObject);
            m_resPoolRoot = null;
            m_addressablesPoolRoot = null;
            m_prefabPoolRoot = null;
        }
        #endregion
    }
}
