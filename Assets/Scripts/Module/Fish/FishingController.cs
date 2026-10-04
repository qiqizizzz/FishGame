/*
* ┌──────────────────────────────────┐
* │  描    述: 钓鱼玩法控制器，装配服务、裁决钩鱼接触并每帧驱动全局
* │  类    名: FishingController.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 每帧顺序固定：采样输入 → 重建空间索引 → 推进状态机 → 钩上鱼跟随 → 流式生成 → 批量驱动行为
* 状态迁移与判定规则全部收在控制器（见 01 篇 4.3：Controller 是唯一可改 Model 的地方）
*/

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Framework.QTower.Common.Defines;
using Framework.QTower.Controller;
using Framework.QTower.Event;
using Module.Fish.Data;
using Module.Fish.Model;
using Module.Fish.Service;
using Module.Fish.Type;
using Module.Fish.View;
using UnityEngine;
using Utils.log;

namespace Module.Fish
{
    public sealed class FishingController : BaseController, IFishingEncounter
    {
        private const KeyCode EXIT_TO_MENU_KEY = KeyCode.Escape;

        // 一次接触裁决后的免疫窗口，防止同一条鱼在相邻帧反复触发缠绕
        private const float CONTACT_COOLDOWN = 0.3f;

        // ==================== 字段[私有] ====================
        private readonly FishingScene m_scene;
        private readonly MouseInputProvider m_input = new();
        private readonly FishSpawnService m_spawn = new();
        private readonly FishAiService m_ai = new();
        private readonly FishDetectionService m_detection = new();
        private readonly FishCarryService m_carry = new();
        private readonly List<CatchRecord> m_buffer = new();

        private FishFactory m_factory;
        private HookStateMachine m_machine;
        private IFishingStats m_stats;
        private HookViewController m_hook;
        private bool m_running;
        private bool m_phasePublished;
        private float m_contactCooldown;
        private int m_totalCatch;
        private HookPhase m_lastPhase = HookPhase.Idle;

        public FishingController(FishingScene scene)
        {
            m_scene = scene;
        }

        /// <summary>本局已带回水面的鱼数，M3 接 HUD 与结束条件</summary>
        public int TotalCatch => m_totalCatch;

        /// <summary>当前钩上负载，HUD 画鱼串与容量条取这里</summary>
        public IReadOnlyList<CatchRecord> Carried => m_carry.Carried;

        // ==================== 生命周期 ====================
        protected override void OnInit()
        {
            if (!validateReferences())
                return;

            m_hook = m_scene.Hook;
            m_stats = new DefaultFishingStats(m_scene.HookConfig);
            m_factory = new FishFactory();
            m_machine = new HookStateMachine(
                new IdleHookState(),
                new DescendingHookState(),
                new ReelingHookState(),
                new TangledHookState(),
                new LandedHookState());

            m_hook.Initialize(m_scene.HookConfig, m_scene.SessionConfig, m_stats, m_machine, this);
            m_scene.CameraRig.Configure(m_scene.SessionConfig.ShaftDepth);

            startSession().Forget();
        }

        protected override void OnDestroy()
        {
            m_running = false;
            m_spawn.EndSession();
        }

        public override void Tick(float deltaTime)
        {
            // 临时退出入口，M3 结算面板的「退出」按钮接上后改由按钮事件驱动
            if (Input.GetKeyDown(EXIT_TO_MENU_KEY))
                EventManager.PublishEvent(EventDefines.ReturnToMainMenuRequested);

            if (!m_running)
                return;

            m_input.Tick();
            m_contactCooldown = Mathf.Max(0f, m_contactCooldown - deltaTime);

            // 同一帧内按下并松开也算按住，避免快点被采样吞掉
            m_hook.FeedInput(m_input.IsPressed || m_input.PressBegan, m_input.DragDeltaPixels.x);

            m_detection.Rebuild(m_spawn.Alive);
            m_machine.Update(m_hook, deltaTime);
            publishPhaseChanged();

            m_carry.Follow(m_hook.Horizontal, m_hook.Depth);
            m_spawn.Stream(m_hook.Depth);
            m_ai.Advance(m_spawn.Alive, deltaTime, m_spawn.SwimHalfWidth);
        }

        // ==================== Public Function ====================
        /// <summary>
        /// 裁决钩子与周围鱼的一次接触，规则见 01 篇 3.3
        /// </summary>
        /// <param name="catching">true 为回收阶段的鱼头捕获，false 为下潜阶段的任意接触即失误</param>
        /// <returns>需要进入缠绕态时返回 true</returns>
        public bool ResolveContact(IHookRuntime hook, bool catching)
        {
            if (m_contactCooldown > 0f)
                return false;

            HookConfigSO config = hook.HookConfig;

            if (!m_detection.TryFindTouched(hook.Horizontal, hook.Depth, config.HookRadius, catching, out FishInstance fish))
                return false;

            m_contactCooldown = CONTACT_COOLDOWN;

            if (fish.Data != null && fish.Data.Rarity == FishRarity.Hazard)
            {
                penalizeHazard(fish);
                return true;
            }

            if (catching)
                attachFish(fish);
            else
                scareFish(fish);

            return false;
        }

        // ==================== Private Function ====================
        // prefab 预加载完成后才开局，避免第一次下潜时同步加载卡顿
        private async UniTaskVoid startSession()
        {
            await m_factory.Prepare(m_scene.SessionConfig.Fishes);

            m_carry.BeginSession(m_stats, m_scene.RarityTable);
            m_spawn.BeginSession(m_scene.SessionConfig, m_scene.RarityTable, m_stats, m_factory,
                m_scene.FishRoot, m_scene.CameraRig.ViewHalfWidth);

            m_machine.Start(m_hook);
            m_running = true;

            QLog.Info($"[{nameof(FishingController)}] 本局可下潜 {m_hook.MaxDepth:0} 米，" +
                      $"鱼群游动半宽 {m_spawn.SwimHalfWidth:0.00} 米，在场鱼 {m_spawn.Alive.Count} 条，" +
                      $"携带容量 {m_stats.CarryCapacity} 点");
        }

        // 阶段变化才广播，并在出水那一刻结算钩上的鱼
        private void publishPhaseChanged()
        {
            HookPhase phase = m_machine.Phase;

            if (!m_phasePublished || phase != m_lastPhase)
            {
                m_lastPhase = phase;
                m_phasePublished = true;
                EventManager.PublishEvent(EventDefines.FishingPhaseChanged, phase);
            }

            if (phase == HookPhase.Landed)
                settleLanded();
        }

        private void attachFish(FishInstance fish)
        {
            CatchRecord record = m_carry.Attach(fish);

            if (record == null)
                return;

            EventManager.PublishEvent(EventDefines.FishingFishCaptured, captureInfo(fish, record.AttachedDepth));
        }

        private void scareFish(FishInstance fish)
        {
            m_ai.Scatter(fish, m_hook.Horizontal);
            EventManager.PublishEvent(EventDefines.FishingPenaltyApplied, PenaltyType.ScaredFish);
        }

        // 河豚：钩上鱼按概率甩掉，河豚自己也被惊散，避免退出缠绕后二次触发
        private void penalizeHazard(FishInstance fish)
        {
            m_ai.Scatter(fish, m_hook.Horizontal);
            m_carry.DetachByChance(m_scene.HookConfig.DetachChance, m_buffer);

            for (int i = m_buffer.Count - 1; i >= 0; i--)
            {
                FishInstance dropped = m_buffer[i].Fish;

                if (dropped == null)
                    continue;

                dropped.IsHooked = false;
                m_ai.Scatter(dropped, m_hook.Horizontal);
            }

            m_buffer.Clear();
            EventManager.PublishEvent(EventDefines.FishingPenaltyApplied, PenaltyType.Pufferfish);
        }

        // 出水：钩上的鱼计入本局收获并回池，脱钩的鱼已经自行游走
        private void settleLanded()
        {
            m_carry.Unload(m_buffer);

            if (m_buffer.Count == 0)
                return;

            for (int i = m_buffer.Count - 1; i >= 0; i--)
            {
                CatchRecord record = m_buffer[i];
                FishInstance fish = record.Fish;

                if (fish == null)
                    continue;

                if (fish.IsAlive)
                    m_factory.Despawn(fish.Data, fish.View);

                fish.IsHooked = false;
                m_spawn.Remove(fish);
            }

            m_totalCatch += m_buffer.Count;
            QLog.Info($"[{nameof(FishingController)}] 本次出水带回 {m_buffer.Count} 条，累计 {m_totalCatch} 条");
            m_buffer.Clear();
        }

        private static FishCaptureInfo captureInfo(FishInstance fish, float depth)
        {
            FishDataSO data = fish.Data;

            return new FishCaptureInfo(data.Id, data.DisplayName, data.Rarity, depth);
        }

        private bool validateReferences()
        {
            bool valid = m_scene != null
                && m_scene.RarityTable != null
                && m_scene.HookConfig != null
                && m_scene.SessionConfig != null
                && m_scene.Hook != null
                && m_scene.CameraRig != null
                && m_scene.FishRoot != null;

            if (!valid)
                QLog.Error($"[{nameof(FishingController)}] 场景引用不完整，玩法不会启动，请检查 FishingScene 上的挂载");

            return valid;
        }
    }
}