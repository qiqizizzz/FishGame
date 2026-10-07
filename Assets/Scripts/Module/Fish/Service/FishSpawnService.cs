/*
* ┌──────────────────────────────────┐
* │  描    述: 鱼群生成服务，按深度切片加权抽取并随钩子深度流式生成与回收
* │  类    名: FishSpawnService.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 每 5 米一个深度切片建一张加权表，表内每个档位的权重总和等于该档位基准权重，
* 这样同档位有多条鱼时不会把档位权重翻倍；稀有度放大系数按档位次数叠加，
* 于是钓竿属性只影响“抽到高档鱼的概率”，不改动深度带本身
* 落点要做避让：鱼是按体长/身高占屏的，位置撞在一起就变成一坨看不清的东西，
* 所以群游偏移按鱼自身尺寸放大，生成前再在附近小范围试探几个空位
*/

using System.Collections.Generic;
using Module.Fish.Data;
using Module.Fish.Model;
using Module.Fish.Type;
using UnityEngine;
using Utils.log;

namespace Module.Fish.Service
{
    /// <summary>
    /// 鱼群生成服务，维护在场鱼列表，按钩子深度流式补足与回收
    /// </summary>
    public sealed class FishSpawnService
    {
        private const float DEPTH_SLICE = 5f;
        private const float STREAM_AHEAD = 30f;
        private const float STREAM_BEHIND = 12f;
        private const float SURFACE_TOLERANCE = 2f;
        private const float EDGE_ENTRY_INSET = 0.15f;
        private const float SWIM_WIDTH_PADDING = 0.3f;
        private const float MIN_FISH_DEPTH = 1.2f;
        private const float SPEED_JITTER_MIN = 0.85f;
        private const float SPEED_JITTER_MAX = 1.15f;
        private const int MAX_ALIVE = 18;
        private const int MAX_BATCH_PER_SLICE = 4;
        private const int SCHOOL_MIN = 2;
        private const int SCHOOL_MAX = 4;

        // 群游编组的散布半径按鱼自身尺寸放大，小鱼成团紧凑、大鱼自动拉开
        private const float SCHOOL_SPREAD_X_RATIO = 0.9f;
        private const float SCHOOL_SPREAD_Y_RATIO = 1.6f;

        // 落点避让：以目标点为中心的小范围试探，宽度按两条鱼的合成尺寸判定
        private const int SPOT_JITTER_TRIES = 4;
        private const float SPOT_JITTER_X = 1.1f;
        private const float SPOT_JITTER_DEPTH = 1.2f;
        private const float MIN_GAP_X_RATIO = 0.62f;
        private const float MIN_GAP_Y_RATIO = 0.6f;

        private readonly List<FishInstance> m_alive = new();
        private readonly List<FishDataSO> m_hazards = new();
        private readonly List<FishDataSO> m_candidates = new();
        private readonly Dictionary<FishRarity, int> m_rarityCount = new();

        private WeightedTable<FishDataSO>[] m_slices;

        private FishingSessionConfigSO m_session;
        private RarityTableSO m_rarityTable;
        private IFishingStats m_stats;
        private FishFactory m_factory;
        private Transform m_root;

        private int m_sliceCount;
        private int m_schoolSerial;
        private float m_reachableDepth;

        public IReadOnlyList<FishInstance> Alive => m_alive;

        /// <summary>鱼可游动的横向半宽（米），取井道半宽与可视半宽的较大值</summary>
        public float SwimHalfWidth { get; private set; }

        public bool IsActive => m_slices != null;

        /// <summary>
        /// 开一局：建深度切片表并铺满鱼线可达范围内的初始鱼群
        /// </summary>
        /// <param name="viewHalfWidth">相机可视横向半宽（米）</param>
        public void BeginSession(FishingSessionConfigSO session, RarityTableSO rarityTable, IFishingStats stats,
            FishFactory factory, Transform root, float viewHalfWidth)
        {
            if (session == null || rarityTable == null || stats == null || factory == null || root == null)
            {
                QLog.Error($"[{nameof(FishSpawnService)}] 开局引用缺失，生成服务不会启动");
                return;
            }
            m_session = session;
            m_rarityTable = rarityTable;
            m_stats = stats;
            m_factory = factory;
            m_root = root;

            m_reachableDepth = Mathf.Min(session.ShaftDepth, stats.LineLength);
            m_sliceCount = Mathf.Max(1, Mathf.CeilToInt(session.ShaftDepth / DEPTH_SLICE));
            SwimHalfWidth = Mathf.Max(session.ShaftHalfWidth, viewHalfWidth) + SWIM_WIDTH_PADDING;

            collectHazards();

            m_slices = new WeightedTable<FishDataSO>[m_sliceCount];

            for (int slice = 0; slice < m_sliceCount; slice++)
                m_slices[slice] = buildSliceTable(slice);

            populateInitial();
        }

        /// <summary>
        /// 每帧随钩子深度流式维护鱼群：先回收窗口外的，再补足窗口内各切片的缺口
        /// </summary>
        public void Stream(float hookDepth)
        {
            if (m_slices == null)
                return;

            float minDepth = Mathf.Max(-SURFACE_TOLERANCE, hookDepth - STREAM_BEHIND);
            float maxDepth = Mathf.Min(m_reachableDepth, hookDepth + STREAM_AHEAD);

            recycleOutside(minDepth, maxDepth);

            int firstSlice = sliceIndexOf(minDepth);
            int lastSlice = sliceIndexOf(maxDepth);

            for (int slice = firstSlice; slice <= lastSlice && m_alive.Count < MAX_ALIVE; slice++)
                fillSliceToTarget(slice, true);
        }

        /// <summary>
        /// 把已经离场的鱼（被钓走）从在场列表摘掉，视图回收由调用方负责
        /// </summary>
        public void Remove(FishInstance fish)
        {
            if (fish != null)
                m_alive.Remove(fish);
        }

        /// <summary>
        /// 结束一局：全部鱼回池并清空切片表
        /// </summary>
        public void EndSession()
        {
            if (m_slices == null)
                return;

            for (int i = m_alive.Count - 1; i >= 0; i--)
                recycleAt(i);

            m_hazards.Clear();
            m_slices = null;
            m_session = null;
            m_rarityTable = null;
            m_stats = null;
            m_factory = null;
            m_root = null;
        }

        // 收集 Hazard 档鱼种，它们权重为 0 不进表，改由会话概率单独接管
        private void collectHazards()
        {
            m_hazards.Clear();

            IReadOnlyList<FishDataSO> pool = m_session.Fishes;

            for (int i = 0; i < pool.Count; i++)
            {
                FishDataSO data = pool[i];

                if (data == null || data.Rarity != FishRarity.Hazard || !m_factory.CanSpawn(data))
                    continue;

                m_hazards.Add(data);
            }
        }

        // 建单个深度切片的加权表，同档位内按鱼数均分档位权重
        private WeightedTable<FishDataSO> buildSliceTable(int slice)
        {
            WeightedTable<FishDataSO> table = new();
            DepthBand sliceBand = sliceBandOf(slice);

            m_candidates.Clear();
            m_rarityCount.Clear();

            IReadOnlyList<FishDataSO> pool = m_session.Fishes;

            for (int i = 0; i < pool.Count; i++)
            {
                FishDataSO data = pool[i];

                if (data == null || data.Rarity == FishRarity.Hazard || !m_factory.CanSpawn(data))
                    continue;

                if (!data.ResolveDepthBand(m_rarityTable).Intersect(sliceBand).IsValid)
                    continue;

                m_candidates.Add(data);
                m_rarityCount[data.Rarity] = m_rarityCount.TryGetValue(data.Rarity, out int counted) ? counted + 1 : 1;
            }

            float lurePull = Mathf.Max(0.01f, m_stats.LurePull);

            for (int i = 0; i < m_candidates.Count; i++)
            {
                FishDataSO data = m_candidates[i];
                RarityRow row = m_rarityTable.Get(data.Rarity);

                if (row == null)
                    continue;

                float weight = row.SpawnWeight / Mathf.Max(1, m_rarityCount[data.Rarity]);
                table.Add(data, weight * Mathf.Pow(lurePull, (int) data.Rarity));
            }

            return table;
        }

        // 开局铺满鱼线可达范围内的切片，初始鱼随机散布在可视宽度内
        private void populateInitial()
        {
            int lastSlice = sliceIndexOf(m_reachableDepth);

            for (int slice = 0; slice <= lastSlice && m_alive.Count < MAX_ALIVE; slice++)
                fillSliceToTarget(slice, false);
        }

        private void fillSliceToTarget(int slice, bool fromEdge)
        {
            int deficit = targetForSlice(slice) - countInSlice(slice);
            int batchUsed = 0;

            while (deficit > 0 && m_alive.Count < MAX_ALIVE && batchUsed < MAX_BATCH_PER_SLICE)
            {
                int spawned = spawnBatch(slice, fromEdge);

                if (spawned <= 0)
                    break;

                deficit -= spawned;
                batchUsed++;
            }
        }

        // 生成一批：群游鱼一次生成 3-6 条并共享方向、速度与基准深度
        private int spawnBatch(int slice, bool fromEdge)
        {
            if (!tryPickFish(slice, out FishDataSO data, out float depth))
                return 0;

            bool isSchool = data.Behavior == FishBehaviorType.School;
            int count = isSchool ? Random.Range(SCHOOL_MIN, SCHOOL_MAX + 1) : 1;
            count = Mathf.Min(count, MAX_ALIVE - m_alive.Count);

            if (count <= 0)
                return 0;

            int direction;
            float centerX;

            if (fromEdge)
            {
                bool fromLeft = Random.value < 0.5f;
                centerX = fromLeft ? -(SwimHalfWidth - EDGE_ENTRY_INSET) : SwimHalfWidth - EDGE_ENTRY_INSET;
                direction = fromLeft ? 1 : -1;
            }
            else
            {
                centerX = Random.Range(-SwimHalfWidth + EDGE_ENTRY_INSET, SwimHalfWidth - EDGE_ENTRY_INSET);
                direction = Random.value < 0.5f ? -1 : 1;
            }

            float speed = baseSpeed(data.Rarity) * Random.Range(SPEED_JITTER_MIN, SPEED_JITTER_MAX);
            int schoolId = isSchool ? ++m_schoolSerial : 0;
            int spawned = 0;

            float memberLength = data.DisplayLength;
            float memberHeight = m_factory.DisplayHeight(data);

            for (int i = 0; i < count; i++)
            {
                Vector2 offset = isSchool
                    ? new Vector2(Random.Range(-memberLength, memberLength) * SCHOOL_SPREAD_X_RATIO,
                        Random.Range(-memberHeight, memberHeight) * SCHOOL_SPREAD_Y_RATIO)
                    : Vector2.zero;

                if (spawnOne(data, centerX + offset.x, depth, offset, direction, speed, schoolId))
                    spawned++;
            }

            return spawned;
        }

        private bool spawnOne(FishDataSO data, float x, float depth, Vector2 offset, int direction, float speed, int schoolId)
        {
            float length = data.DisplayLength;
            float height = m_factory.DisplayHeight(data);
            Vector4 bodyBox = m_factory.GetBodyBox(data);

            if (!resolveFreeSpot(ref x, ref depth, offset.y, length, height))
                return false;

            GameObject view = m_factory.Spawn(data, m_root, new Vector3(x, -depth, 0f), direction);

            if (view == null)
                return false;

            FishInstance fish = new()
            {
                Data = data,
                View = view,
                Root = view.transform,
                Behavior = data.Behavior,
                BaseDepth = depth,
                DisplayHeight = height,
                BodyHalf = new Vector2(bodyBox.x, bodyBox.y),
                BodyOffset = new Vector2(bodyBox.z, bodyBox.w),
                X = x,
                Depth = depth,
                Direction = direction,
                Speed = speed,
                Phase = Random.value * Mathf.PI * 2f,
                SchoolId = schoolId,
                Offset = offset
            };

            m_alive.Add(fish);
            return true;
        }

        // 行为每帧把 Offset.y 叠加到 BaseDepth 上，所以判空位要用「基准深度 + 群游纵深偏移」
        private bool resolveFreeSpot(ref float x, ref float depth, float offsetY, float length, float height)
        {
            for (int attempt = 0; attempt < SPOT_JITTER_TRIES; attempt++)
            {
                if (attempt > 0)
                {
                    x = Mathf.Clamp(x + Random.Range(-SPOT_JITTER_X, SPOT_JITTER_X), -SwimHalfWidth, SwimHalfWidth);
                    depth = Mathf.Clamp(depth + Random.Range(-SPOT_JITTER_DEPTH, SPOT_JITTER_DEPTH),
                        MIN_FISH_DEPTH, m_reachableDepth);
                }

                if (isSpotFree(x, depth + offsetY, length, height))
                    return true;
            }

            return false;
        }

        // 横向按体长留位、纵深按身高留位，两个方向同时挤在一起才算重叠
        private bool isSpotFree(float x, float depth, float length, float height)
        {
            for (int i = 0; i < m_alive.Count; i++)
            {
                FishInstance other = m_alive[i];

                if (other == null || !other.IsAlive || other.IsHooked || other.Data == null)
                    continue;

                float gapX = (length + other.Data.DisplayLength) * MIN_GAP_X_RATIO;
                float gapDepth = (height + other.DisplayHeight) * MIN_GAP_Y_RATIO;

                if (Mathf.Abs(x - other.X) < gapX && Mathf.Abs(depth - other.Depth) < gapDepth)
                    return false;
            }

            return true;
        }

        // 抽一条鱼并定下生成深度，先按会话概率试河豚，再回落到切片表
        private bool tryPickFish(int slice, out FishDataSO data, out float depth)
        {
            data = null;
            depth = 0f;

            if (m_hazards.Count > 0 && Random.value < m_session.HazardChance)
            {
                FishDataSO hazard = m_hazards[Random.Range(0, m_hazards.Count)];

                if (tryPickDepth(hazard, slice, out depth))
                {
                    data = hazard;
                    return true;
                }
            }

            return m_slices[slice].TrySample(Random.value, out data) && tryPickDepth(data, slice, out depth);
        }

        private bool tryPickDepth(FishDataSO data, int slice, out float depth)
        {
            DepthBand band = data.ResolveDepthBand(m_rarityTable).Intersect(sliceBandOf(slice));

            if (!band.IsValid)
            {
                depth = 0f;
                return false;
            }

            // 顶到水面的鱼会冒出冰面，最低生成深度给群游偏移与起伏留出余量
            depth = Random.Range(Mathf.Max(band.MinDepth, MIN_FISH_DEPTH), band.MaxDepth);
            return true;
        }

        private void recycleOutside(float minDepth, float maxDepth)
        {
            for (int i = m_alive.Count - 1; i >= 0; i--)
            {
                FishInstance fish = m_alive[i];

                // 钩上的鱼深度由携带服务决定，可能压到可达范围之外，不能按窗口回收
                if (fish.IsHooked)
                    continue;

                if (fish.View == null || fish.Depth < minDepth || fish.Depth > maxDepth)
                    recycleAt(i);
            }
        }

        private void recycleAt(int index)
        {
            FishInstance fish = m_alive[index];
            m_factory.Despawn(fish.Data, fish.View);
            fish.View = null;
            fish.Root = null;
            m_alive.RemoveAt(index);
        }

        private int countInSlice(int slice)
        {
            DepthBand band = sliceBandOf(slice);
            int count = 0;

            for (int i = 0; i < m_alive.Count; i++)
            {
                if (band.Contains(m_alive[i].BaseDepth))
                    count++;
            }

            return count;
        }

        private int targetForSlice(int slice)
        {
            // 鱼线够不到的切片不铺鱼，省下来的是实打实的实例与绘制
            if (slice * DEPTH_SLICE >= m_reachableDepth)
                return 0;

            return Mathf.RoundToInt(m_session.BaseFishPerSlice * m_session.SampleDensity((slice + 0.5f) * DEPTH_SLICE));
        }

        private int sliceIndexOf(float depth)
        {
            return Mathf.Clamp(Mathf.FloorToInt(depth / DEPTH_SLICE), 0, m_sliceCount - 1);
        }

        private static DepthBand sliceBandOf(int slice)
        {
            return new DepthBand
            {
                MinDepth = slice * DEPTH_SLICE,
                MaxDepth = (slice + 1) * DEPTH_SLICE
            };
        }

        // 档位巡游基准速度：浅层小鱼灵活，深层大鱼慢而显眼，河豚几乎不动
        private static float baseSpeed(FishRarity rarity)
        {
            return rarity switch
            {
                FishRarity.Common => 1.2f,
                FishRarity.Uncommon => 1.0f,
                FishRarity.Rare => 0.9f,
                FishRarity.Epic => 0.8f,
                FishRarity.Legendary => 1.6f,
                FishRarity.Hazard => 0.15f,
                _ => 1.0f
            };
        }
    }
}