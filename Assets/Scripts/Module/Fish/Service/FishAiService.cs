/*
* ┌──────────────────────────────────┐
* │  描    述: 鱼群批量驱动，一条鱼不挂 MonoBehaviour.Update
* │  类    名: FishAiService.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 鱼数量大时 Update 峰值是性能风险，统一由本服务一帧遍历一次
*/

using System.Collections.Generic;
using Module.Fish.Model;
using Module.Fish.Type;
using UnityEngine;
using Utils.log;

namespace Module.Fish.Service
{
    public sealed class FishAiService
    {
        private const float FLEE_SPEED_MULTIPLIER = 3.5f;
        private const float FLEE_DURATION = 1.1f;
        // 自由鱼所在的局部纵深，与 FishFactory.Spawn 生成时写入的 z 保持一致
        private const float FREE_LOCAL_Z = 0f;

        private readonly Dictionary<FishBehaviorType, IFishBehavior> m_behaviors = new();

        public FishAiService()
        {
            Register(new CruiseFishBehavior());
            Register(new SchoolFishBehavior());
            Register(new DriftFishBehavior());
            Register(new GuardFishBehavior());
            Register(new EvasiveFishBehavior());
        }

        /// <summary>
        /// 注册行为实现，同行为重复注册会覆盖并报错
        /// </summary>
        public void Register(IFishBehavior behavior)
        {
            if (behavior == null)
                return;

            if (m_behaviors.ContainsKey(behavior.Behavior))
                QLog.Error($"[{nameof(FishAiService)}] 行为重复注册：{behavior.Behavior}");

            m_behaviors[behavior.Behavior] = behavior;
        }

        /// <summary>
        /// 推进全部在场鱼并写回变换，未注册的行为回落到巡游并报错一次
        /// </summary>
        /// <param name="hook">本帧钩子探针，原样转交给各行为实现</param>
        public void Advance(IReadOnlyList<FishInstance> fishes, float deltaTime, float swimHalfWidth,
            in HookProbe hook)
        {
            if (deltaTime <= 0f)
                return;

            for (int i = 0; i < fishes.Count; i++)
            {
                FishInstance fish = fishes[i];

                // 钩上的鱼由 FishCarryService 定位，这里不能再去驱动它
                if (fish == null || !fish.IsAlive || fish.IsHooked)
                    continue;

                if (fish.FleeTimer > 0f)
                {
                    advanceFlee(fish, deltaTime, swimHalfWidth);
                }
                else if (!m_behaviors.TryGetValue(fish.Behavior, out IFishBehavior behavior))
                {
                    QLog.Error($"[{nameof(FishAiService)}] 未注册行为 {fish.Behavior}，鱼 {fish.Data.Id} 回退为巡游");
                    behavior = m_behaviors[FishBehaviorType.Cruise];
                    fish.Behavior = FishBehaviorType.Cruise;
                    behavior.Advance(fish, deltaTime, swimHalfWidth, hook);
                }
                else
                {
                    behavior.Advance(fish, deltaTime, swimHalfWidth, hook);
                }

                Transform root = fish.Root;
                // 自由鱼固定待在自己那一层，脱钩交还的鱼下一帧就被摆回原层，不会留在鱼串那一层
                root.localPosition = new Vector3(fish.X, -fish.Depth, FREE_LOCAL_Z);
                root.localRotation = Quaternion.Euler(0f, fish.Direction >= 0 ? 90f : -90f, 0f);
            }
        }

        /// <summary>
        /// 惊散一条鱼：朝背离钩子的方向高速窜出，时长结束后自动恢复原行为
        /// </summary>
        public void Scatter(FishInstance fish, float fromX)
        {
            if (fish == null || fish.IsHooked)
                return;

            fish.Direction = fish.X >= fromX ? 1 : -1;
            fish.FleeTimer = FLEE_DURATION;
        }

        // 惊散只改横向，深度仍由原行为决定，避免逃完瞬间弹回原深度带
        private static void advanceFlee(FishInstance fish, float deltaTime, float swimHalfWidth)
        {
            fish.FleeTimer = Mathf.Max(0f, fish.FleeTimer - deltaTime);
            fish.X += fish.Direction * fish.Speed * FLEE_SPEED_MULTIPLIER * deltaTime;

            if (Mathf.Abs(fish.X) < swimHalfWidth)
                return;

            fish.X = Mathf.Clamp(fish.X, -swimHalfWidth, swimHalfWidth);
            fish.Direction = -fish.Direction;
        }
    }
}