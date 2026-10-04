/*
* ┌──────────────────────────────────┐
* │  描    述: 鱼实例工厂，屏蔽资源加载与对象池细节
* │  类    名: FishFactory.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 工厂模式：生成逻辑只认 FishDataSO，不关心 prefab 从哪来、实例是新建还是复用
* 尺寸按「实测模型长度」折算，让 FishDataSO.DisplayLengthMeters 成为屏幕大小的唯一真源
* （SizeRatio 假设模型按 1 单位=1 米导出，实测各 prefab 根缩放不统一，直接用会走样）
* 顺带实测模型高度，鱼串间距与防重叠判定要用真实的屏幕占位，不能拿体长凑
*/

using System.Collections.Generic;
using Common.Res;
using Cysharp.Threading.Tasks;
using Module.Fish.Data;
using UnityEngine;
using Utils.log;

namespace Module.Fish.Service
{
    public sealed class FishFactory
    {
        // 实测失败时按体长的三成估高度，鱼串间距不至于塌成一条线
        private const float FALLBACK_HEIGHT_RATIO = 0.3f;

        private readonly Dictionary<FishDataSO, GameObject> m_prefabs = new();
        private readonly Dictionary<FishDataSO, Vector2> m_modelSizes = new();

        /// <summary>
        /// 预加载鱼池全部 prefab 并实测模型长度，避免生成瞬间同步加载卡顿
        /// </summary>
        public async UniTask Prepare(IReadOnlyList<FishDataSO> fishes)
        {
            if (fishes == null)
                return;

            List<FishDataSO> pending = collectPending(fishes);

            if (pending.Count == 0)
                return;

            // 先把全部加载请求一次性发出去：句柄在调用时就已启动，
            // 逐条 await 会把 N 个 prefab 的耗时串成一条长队
            List<UniTask<GameObject>> loads = new(pending.Count);

            foreach (FishDataSO data in pending)
                loads.Add(ResManager.LoadAssetAsync<GameObject>(data.Address));

            for (int index = 0; index < pending.Count; index++)
            {
                FishDataSO data = pending[index];
                GameObject prefab = await loads[index];

                if (prefab == null)
                {
                    QLog.Error($"[{nameof(FishFactory)}] 鱼种 {data.Id} 的 prefab 加载失败：{data.Address}");
                    continue;
                }

                m_prefabs.Add(data, prefab);
                m_modelSizes.Add(data, measureSize(prefab));
            }
        }

        // 过滤掉已缓存与配置里重复的鱼种，保持原顺序
        private List<FishDataSO> collectPending(IReadOnlyList<FishDataSO> fishes)
        {
            List<FishDataSO> pending = new();

            foreach (FishDataSO data in fishes)
            {
                if (data == null || m_prefabs.ContainsKey(data) || pending.Contains(data))
                    continue;

                pending.Add(data);
            }

            return pending;
        }

        public bool CanSpawn(FishDataSO data)
        {
            return data != null && m_prefabs.ContainsKey(data);
        }

        /// <summary>
        /// 取一条鱼实例，放到父节点下的局部坐标并按显示体长缩放、按朝向转身
        /// </summary>
        public GameObject Spawn(FishDataSO data, Transform parent, Vector3 localPosition, int direction)
        {
            if (!CanSpawn(data))
                return null;

            GameObject prefab = m_prefabs[data];
            GameObject view = ResManager.InstantiateFromPool(prefab, parent);

            if (view == null)
                return null;

            Transform root = view.transform;
            root.localPosition = localPosition;
            root.localRotation = Quaternion.Euler(0f, direction >= 0 ? 90f : -90f, 0f);
            root.localScale = Vector3.one * displayScale(data);

            return view;
        }


        /// <summary>缩放后的屏幕显示高度（米），鱼串间距与防重叠按这个值留位</summary>
        public float DisplayHeight(FishDataSO data)
        {
            if (data == null)
                return 0f;

            Vector2 size = m_modelSizes.TryGetValue(data, out Vector2 measured) ? measured : Vector2.zero;

            if (size.x <= 0f)
                return data.DisplayLength * FALLBACK_HEIGHT_RATIO;

            return size.y * (data.DisplayLength / size.x);
        }

        /// <summary>
        /// 回收鱼实例到对象池
        /// </summary>
        public void Despawn(FishDataSO data, GameObject view)
        {
            if (data == null || view == null)
                return;

            if (m_prefabs.TryGetValue(data, out GameObject prefab))
                ResManager.ReleaseToPool(prefab, view);
            else
                Object.Destroy(view);
        }

        // 模型长轴在世界 Z 上（glTF 的 Rx(-90) 已烘进子节点），转身前实测一次
        private static Vector2 measureSize(GameObject prefab)
        {
            GameObject temp = Object.Instantiate(prefab);
            temp.transform.position = Vector3.zero;
            temp.transform.rotation = Quaternion.identity;

            Renderer renderer = temp.GetComponentInChildren<Renderer>();
            Vector3 bounds = renderer != null ? renderer.bounds.size : Vector3.zero;

            Object.Destroy(temp);

            return bounds.z > 0f ? new Vector2(bounds.z, bounds.y) : Vector2.one;
        }

        private float displayScale(FishDataSO data)
        {
            float modelLength = m_modelSizes.TryGetValue(data, out Vector2 size) ? size.x : 1f;

            return data.DisplayLength > 0f ? data.DisplayLength / modelLength : 1f;
        }
    }
}