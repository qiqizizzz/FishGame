/*
* ┌──────────────────────────────────┐
* │  描    述: 鱼群批量驱动，一条鱼不挂 MonoBehaviour.Update
* │  类    名: FishAiService.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 见 01 篇第九章：鱼数量大时 Update 峰值是性能风险，统一由本服务一帧遍历一次
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
        private readonly Dictionary<FishBehaviorType, IFishBehavior> m_behaviors = new();

        public FishAiService()
        {
            Register(new CruiseFishBehavior());
            Register(new SchoolFishBehavior());
            Register(new DriftFishBehavior());
            Register(new GuardFishBehavior());
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
        public void Advance(IReadOnlyList<FishInstance> fishes, float deltaTime, float swimHalfWidth)
        {
            if (deltaTime <= 0f)
                return;

            for (int i = 0; i < fishes.Count; i++)
            {
                FishInstance fish = fishes[i];

                if (fish == null || !fish.IsAlive)
                    continue;

                if (!m_behaviors.TryGetValue(fish.Behavior, out IFishBehavior behavior))
                {
                    QLog.Error($"[{nameof(FishAiService)}] 未注册行为 {fish.Behavior}，鱼 {fish.Data.Id} 回退为巡游");
                    behavior = m_behaviors[FishBehaviorType.Cruise];
                    fish.Behavior = FishBehaviorType.Cruise;
                }

                behavior.Advance(fish, deltaTime, swimHalfWidth);

                Transform root = fish.Root;
                root.localPosition = new Vector3(fish.X, -fish.Depth, root.localPosition.z);
                root.localRotation = Quaternion.Euler(0f, fish.Direction >= 0 ? 90f : -90f, 0f);
            }
        }
    }
}