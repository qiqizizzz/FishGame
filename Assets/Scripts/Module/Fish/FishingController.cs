/*
* ┌──────────────────────────────────┐
* │  描    述: 钓鱼玩法控制器，装配服务并每帧驱动输入、钩子状态机与鱼群
* │  类    名: FishingController.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 顺序固定为：采样输入 → 推进状态机 → 流式维护鱼群 → 批量驱动鱼行为
* 状态迁移只能发生在这里，View 只负责把坐标写成变换
*/

using Cysharp.Threading.Tasks;
using Framework.QTower.Common.Defines;
using Framework.QTower.Controller;
using Framework.QTower.Event;
using Module.Fish.Model;
using Module.Fish.Service;
using Module.Fish.Type;
using Module.Fish.View;
using UnityEngine;
using Utils.log;

namespace Module.Fish
{
    public sealed class FishingController : BaseController
    {
        private const KeyCode EXIT_TO_MENU_KEY = KeyCode.Escape;

        // ==================== 字段[私有] ====================
        private readonly FishingScene m_scene;
        private readonly MouseInputProvider m_input = new();
        private readonly FishSpawnService m_spawn = new();
        private readonly FishAiService m_ai = new();

        private FishFactory m_factory;
        private HookStateMachine m_machine;
        private IFishingStats m_stats;
        private HookViewController m_hook;
        private bool m_running;
        private bool m_phasePublished;
        private HookPhase m_lastPhase = HookPhase.Idle;

        public FishingController(FishingScene scene)
        {
            m_scene = scene;
        }

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
                new LandedHookState());

            m_hook.Initialize(m_scene.HookConfig, m_scene.SessionConfig, m_stats, m_machine);
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

            // 同一帧内按下并松开也算按住，避免快点被采样吞掉
            m_hook.FeedInput(m_input.IsPressed || m_input.PressBegan, m_input.DragDeltaPixels.x);

            m_machine.Update(m_hook, deltaTime);
            publishPhaseChanged();

            m_spawn.Stream(m_hook.Depth);
            m_ai.Advance(m_spawn.Alive, deltaTime, m_spawn.SwimHalfWidth);
        }

        // ==================== Private Function ====================
        // prefab 预加载完成后才开局，避免第一次下潜时同步加载卡顿
        private async UniTaskVoid startSession()
        {
            await m_factory.Prepare(m_scene.SessionConfig.Fishes);

            m_spawn.BeginSession(m_scene.SessionConfig, m_scene.RarityTable, m_stats, m_factory,
                m_scene.FishRoot, m_scene.CameraRig.ViewHalfWidth);

            m_machine.Start(m_hook);
            m_running = true;

            QLog.Info($"[{nameof(FishingController)}] 本局可下潜 {m_hook.MaxDepth:0} 米，" +
                      $"鱼群游动半宽 {m_spawn.SwimHalfWidth:0.00} 米，在场鱼 {m_spawn.Alive.Count} 条");
        }

        // 阶段变化才广播，HUD 与水体表现不用每帧轮询
        private void publishPhaseChanged()
        {
            HookPhase phase = m_machine.Phase;

            if (m_phasePublished && phase == m_lastPhase)
                return;

            m_lastPhase = phase;
            m_phasePublished = true;
            EventManager.PublishEvent(EventDefines.FishingPhaseChanged, phase);
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