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
* 另外按生成时真正生效的变换量出「真实渲染盒」，判定范围跟着像素走，
* 避免配置体长和屏幕上看到的鱼对不上，视觉上勾到了头尾却判成没碰到
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

        // 转身朝向，必须与 Spawn 写进 localRotation 的值一致，否则量出来的盒子是歪的
        private static readonly Quaternion ROOT_ORIENTATION = Quaternion.Euler(0f, 90f, 0f);

        private readonly Dictionary<FishDataSO, GameObject> m_prefabs = new();
        private readonly Dictionary<FishDataSO, Vector2> m_modelSizes = new();

        // 真实渲染盒：xy 为半长与半高，zw 为视觉中心相对根节点的偏移
        private readonly Dictionary<FishDataSO, Vector4> m_bodyBoxes = new();

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
                m_bodyBoxes.Add(data, measureBodyBox(prefab, displayScale(data)));
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
        /// 取鱼种生成后真正显示出来的盒子：xy 为半长与半高，zw 为视觉中心相对根节点的偏移
        /// x 沿鱼头方向为正、y 向上为正，单位米，捕获判定以此为准而不是拿配置体长凑
        /// </summary>
        public Vector4 GetBodyBox(FishDataSO data)
        {
            if (data == null)
                return fallbackBodyBox(data);

            if (m_bodyBoxes.TryGetValue(data, out Vector4 box) && box.x > 0f && box.y > 0f)
                return box;

            return fallbackBodyBox(data);
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

        // 量不出盒子时退回配置体长，判定范围宁可略窄也不能塌成一个点
        private Vector4 fallbackBodyBox(FishDataSO data)
        {
            float length = data != null ? data.DisplayLength : 0f;

            if (length <= 0f)
                return new Vector4(FALLBACK_HEIGHT_RATIO, FALLBACK_HEIGHT_RATIO, 0f, 0f);

            return new Vector4(length * 0.5f, DisplayHeight(data) * 0.5f, 0f, 0f);
        }

        // 沿层级累乘 TRS 取所有 mesh 角点的并集，不依赖 renderer.bounds 的惰性刷新
        // 根节点用 Spawn 实际写入的缩放与转身，量出来的就是屏幕上那条鱼的占位
        private static Vector4 measureBodyBox(GameObject prefab, float scale)
        {
            Matrix4x4 rootMatrix = Matrix4x4.TRS(Vector3.zero, ROOT_ORIENTATION, Vector3.one * scale);
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool found = false;

            accumulateBody(prefab.transform, rootMatrix, ref found, ref bounds);

            if (!found)
            {
#if UNITY_EDITOR
                Debug.LogError($"[{nameof(FishFactory)}] {prefab.name} 下没有可用的 MeshFilter，判定盒退回配置体长");
#endif
                return Vector4.zero;
            }

            return new Vector4(bounds.size.x * 0.5f, bounds.size.y * 0.5f, bounds.center.x, bounds.center.y);
        }

        // 根节点自身的变换已由 rootMatrix 代表，逐层只乘子节点的 local TRS
        private static void accumulateBody(Transform node, Matrix4x4 matrix, ref bool found, ref Bounds bounds)
        {
            if (node != node.root)
                matrix = matrix * Matrix4x4.TRS(node.localPosition, node.localRotation, node.localScale);

            MeshFilter meshFilter = node.GetComponent<MeshFilter>();

            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                Bounds meshBounds = meshFilter.sharedMesh.bounds;
                Vector3 min = meshBounds.min;
                Vector3 max = meshBounds.max;

                for (int ix = 0; ix < 2; ix++)
                {
                    for (int iy = 0; iy < 2; iy++)
                    {
                        for (int iz = 0; iz < 2; iz++)
                        {
                            Vector3 corner = new Vector3(
                                ix == 0 ? min.x : max.x,
                                iy == 0 ? min.y : max.y,
                                iz == 0 ? min.z : max.z);

                            encapsulate(matrix.MultiplyPoint3x4(corner), ref found, ref bounds);
                        }
                    }
                }
            }

            for (int i = 0; i < node.childCount; i++)
                accumulateBody(node.GetChild(i), matrix, ref found, ref bounds);
        }

        private static void encapsulate(Vector3 point, ref bool found, ref Bounds bounds)
        {
            if (!found)
            {
                bounds = new Bounds(point, Vector3.zero);
                found = true;
                return;
            }

            bounds.Encapsulate(point);
        }

        private float displayScale(FishDataSO data)
        {
            float modelLength = m_modelSizes.TryGetValue(data, out Vector2 size) ? size.x : 1f;

            return data.DisplayLength > 0f ? data.DisplayLength / modelLength : 1f;
        }
    }
}