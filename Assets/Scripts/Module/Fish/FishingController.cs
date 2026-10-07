/*
* ┌──────────────────────────────────┐
* │  描    述: 钓鱼玩法控制器，装配服务、裁决钩鱼接触并每帧驱动全局
* │  类    名: FishingController.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 每帧顺序固定：采样输入 → 重建空间索引 → 推进状态机 → 钩上鱼跟随 → 流式生成 → 批量驱动行为
* 状态迁移与判定规则全部收在控制器：Controller 是唯一可改 Model 的地方
*/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Framework.QTower;
using Framework.QTower.Common.Defines;
using Framework.QTower.Controller;
using Framework.QTower.Event;
using Game;
using Module.Fish.Data;
using Module.Fish.Model;
using Module.Fish.Service;
using Module.Fish.Type;
using Module.Fish.View;
using UI.RoundHUD;
using UI.Summary;
using UnityEngine;
using Utils.log;

namespace Module.Fish
{
    public sealed class FishingController : BaseController, IFishingEncounter
    {
        private const KeyCode EXIT_TO_MENU_KEY = KeyCode.Escape;

        // 低于结算面板（100），高于场景内一切表现
        private const int HUD_SORTING_ORDER = 10;
        private const int ROUND_HUD_SORTING_ORDER = 20;
        private const int RESULT_SORTING_ORDER = 100;

        // 回合系统还没接上，开场先按第一回合演出
        private const int FIRST_ROUND = 1;

        // ==================== 字段[私有] ====================
        private readonly FishingScene m_scene;
        private readonly MouseInputProvider m_input = new();
        private readonly FishSpawnService m_spawn = new();
        private readonly FishAiService m_ai = new();
        private readonly FishDetectionService m_detection = new();
        private readonly FishCarryService m_carry = new();
        private readonly List<CatchRecord> m_buffer = new();
        private readonly Func<FishInstance, bool> m_catchFilter;
        private readonly Func<FishInstance, bool> m_scareFilter;

        private FishFactory m_factory;
        private HookStateMachine m_machine;
        private IFishingStats m_stats;
        private HookViewController m_hook;
        private UIRoundHUD m_roundHud;
        private SessionResult m_result;
        private bool m_running;
        private float m_maxDepth;
        private bool m_phasePublished;
        private int m_totalCatch;
        private HookPhase m_lastPhase = HookPhase.Idle;
        private bool m_disposed;

        public FishingController(FishingScene scene)
        {
            m_scene = scene;
            m_catchFilter = canCatchFish;
            m_scareFilter = canScareFish;
        }

        /// <summary>本局已带回水面的鱼数，结算面板展示条数用，不再作为结束条件</summary>
        public int TotalCatch => m_totalCatch;

        /// <summary>本局累计渔获价值（金币），HUD 进度行与结束条件取这里；
        /// HUD 在预加载阶段就会刷新，此时 result 尚未创建，故允许判空</summary>
        public int TotalCoins => m_result != null ? m_result.TotalCoins : 0;

        /// <summary>当前钩上负载，HUD 画鱼串取这里</summary>
        public IReadOnlyList<CatchRecord> Carried => m_carry.Carried;

        /// <summary>当前放线深度（米），HUD 的鱼线行</summary>
        public float Depth => m_hook != null ? m_hook.Depth : 0f;

        /// <summary>钩上挂着的鱼数，HUD 负载行的分子</summary>
        public int CarriedCount => m_carry.Count;

        /// <summary>钩上可挂的鱼条数上限，HUD 负载行的分母</summary>
        public int MaxCarryCount => m_carry.Capacity;

        /// <summary>本局目标渔获价值（金币），HUD 进度行的分母</summary>
        public int TargetCoins => m_scene != null ? m_scene.SessionConfig.TargetCoins : 0;

        // ==================== 生命周期 ====================
        protected override void OnInit()
        {
            if (!validateReferences())
            {
                publishSceneReady();
                return;
            }

            registerViews();

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
            m_disposed = true;
            m_running = false;
            m_spawn.EndSession();
            GameApp.UIManager.UnregisterByController(this);
        }

        protected override void RegisterModuleEvent()
        {
            RegisterEvent(EventDefines.FishingSessionRestartRequested, restartSession);
        }

        public override void Tick(float deltaTime)
        {
            // 快捷退出入口，正式退出走结算面板的「回主菜单」
            if (Input.GetKeyDown(EXIT_TO_MENU_KEY))
                EventManager.PublishEvent(EventDefines.ReturnToMainMenuRequested);

            if (!m_running)
                return;

            m_input.Tick();

            // 同一帧内按下并松开也算按住，避免快点被采样吞掉
            m_hook.FeedInput(m_input.IsPressed || m_input.PressBegan, m_input.DragDeltaPixels.x);

            m_detection.Rebuild(m_spawn.Alive);
            m_machine.Update(m_hook, deltaTime);
            publishPhaseChanged();
            m_maxDepth = Mathf.Max(m_maxDepth, m_hook.Depth);

            m_carry.Follow(m_hook.Horizontal, m_hook.Depth);
            m_spawn.Stream(m_hook.Depth);
            m_ai.Advance(m_spawn.Alive, deltaTime, m_spawn.SwimHalfWidth);
        }

        // ==================== Public Function ====================
        /// <summary>
        /// 裁决钩子与周围鱼的一次接触
        /// 判定取钩尖捕获点而非钩子根节点，回收阶段碰到整条鱼身即捕获
        /// </summary>
        /// <param name="catching">true 为回收阶段的捕获，false 为下潜阶段的任意接触即失误</param>
        /// <returns>需要进入缠绕态时返回 true</returns>
        public bool ResolveContact(IHookRuntime hook, bool catching)
        {
            HookConfigSO config = hook.HookConfig;

            FishInstance fish;

            bool found = catching
                ? m_detection.TryFindTouched(
                    hook.CaptureHorizontal, hook.CaptureDepth, config.HookRadius, true, m_catchFilter, out fish)
                : m_detection.TryFindTouched(
                    hook.CaptureHorizontal, hook.CaptureDepth, config.HookRadius, false, m_scareFilter, out fish);

            if (!found)
                return false;

            if (fish.Data != null && fish.Data.Rarity == FishRarity.Hazard)
            {
                penalizeHazard(fish);
                return true;
            }

            if (catching)
            {
                // 没挂上就继续找，钩子不受任何免疫影响
                attachFish(fish);
                return false;
            }

            scareFish(fish);

            return false;
        }

        // ==================== Private Function ====================
        // 加载期间玩家可能已经 ESC 回主菜单，这时补一次注销，别把面板悬在主菜单上
        private async UniTask<UIFishingHUD> showHud()
        {
            UIFishingHUD hud = await GameApp.UIManager.Open<UIFishingHUD>();

            if (hud != null && m_disposed)
                GameApp.UIManager.UnregisterByController(this);

            return hud;
        }

        private async UniTask<UIRoundHUD> showRoundHud()
        {
            UIRoundHUD roundHud = await GameApp.UIManager.Open<UIRoundHUD>();

            if (roundHud != null && m_disposed)
                GameApp.UIManager.UnregisterByController(this);

            return roundHud;
        }

        // 视图跟着玩法控制器注册与注销，回主菜单时不会留下悬空面板
        private void registerViews()
        {
            GameApp.UIManager.Register<UIFishingHUD>(new UIData
            {
                Address = UIDefines.UIFishingHUD,
                Parent = GameApp.UIManager.UIRoot,
                Controller = this,
                SortingOrder = HUD_SORTING_ORDER
            });

            GameApp.UIManager.Register<UIRoundHUD>(new UIData
            {
                Address = UIDefines.UIRoundHUD,
                Parent = GameApp.UIManager.UIRoot,
                Controller = this,
                SortingOrder = ROUND_HUD_SORTING_ORDER
            });

            GameApp.UIManager.Register<UIResultPanel>(new UIData
            {
                Address = UIDefines.UIResultPanel,
                Parent = GameApp.UIManager.UIRoot,
                Controller = this,
                SortingOrder = RESULT_SORTING_ORDER
            });
        }

        // HUD 与鱼 prefab 同时发起，两条都完成才向遮罩放行，避免遮罩收了 HUD 才蹦出来
        private async UniTaskVoid startSession()
        {
            UniTask<UIFishingHUD> hudTask = showHud();
            UniTask<UIRoundHUD> roundHudTask = showRoundHud();

            await m_factory.Prepare(m_scene.SessionConfig.Fishes);

            if (m_disposed)
            {
                // 预加载途中被 ESC 打断也要放行遮罩，否则黑屏会一直等到超时
                publishSceneReady();
                return;
            }

            m_carry.BeginSession(m_stats, m_scene.RarityTable);
            m_result = new SessionResult(TargetCoins);
            m_spawn.BeginSession(m_scene.SessionConfig, m_scene.RarityTable, m_stats, m_factory,
                m_scene.FishRoot, m_scene.CameraRig.ViewHalfWidth);

            m_machine.Start(m_hook);
            m_running = true;

            await hudTask;
            m_roundHud = await roundHudTask;
            publishSceneReady();

            m_roundHud.StartRound(FIRST_ROUND, GamePhase.Fishing);

            QLog.Info($"[{nameof(FishingController)}] 本局可下潜 {m_hook.MaxDepth:0} 米，" +
                      $"鱼群游动半宽 {m_spawn.SwimHalfWidth:0.00} 米，在场鱼 {m_spawn.Alive.Count} 条");
        }

        // 遮罩只认这一个信号：场景侧初始化结束，失败与中途中断同样要发
        private void publishSceneReady()
        {
            EventManager.PublishEvent(EventDefines.FishingSceneReady);
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

        // 所有鱼都钓得起来，检测阶段只排除已挂在钩上的；钩上名额是否已满由 FishCarryService 判定
        private bool canCatchFish(FishInstance fish)
        {
            return fish != null && !fish.IsHooked;
        }

        // 误触与河豚受罚的去重：正在惊散逃逸的鱼不重复裁决
        // 取代原先的全局免疫计时器，那玩意儿会把整片水域一起屏蔽约 1.35 米，
        // 钩子从别的鱼身上穿过去也不计数
        private bool canScareFish(FishInstance fish)
        {
            return fish != null && fish.FleeTimer <= 0f;
        }

        private bool attachFish(FishInstance fish)
        {
            CatchRecord record = m_carry.Attach(fish);

            if (record == null)
                return false;

            EventManager.PublishEvent(EventDefines.FishingFishCaptured, captureInfo(fish, record.AttachedDepth));

            return true;
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

        // 出水：先算收益再回池，钩上的鱼计入本局收获，脱钩的鱼已经自行游走
        private void settleLanded()
        {
            m_carry.Unload(m_buffer);

            if (m_buffer.Count == 0)
                return;

            scoreLandedFish();
            recycleLandedFish();

            m_totalCatch += m_buffer.Count;
            QLog.Info($"[{nameof(FishingController)}] 本次出水带回 {m_buffer.Count} 条，累计 {m_totalCatch} 条");
            m_buffer.Clear();

            if (m_result.TotalCoins >= TargetCoins)
                finishSession();
        }

        // 收益公式：鱼种标价 × 深度加成 × 同一次回收的连击倍率
        // Unload 是从最外侧往回塞的，倒着遍历才拿到真实的入钩顺序
        private void scoreLandedFish()
        {
            FishingSessionConfigSO session = m_scene.SessionConfig;
            float depthNorm = session.ShaftDepth > 0f ? 1f / session.ShaftDepth : 0f;

            for (int i = m_buffer.Count - 1; i >= 0; i--)
            {
                CatchRecord record = m_buffer[i];
                FishInstance fish = record.Fish;
                FishDataSO data = fish != null ? fish.Data : null;

                if (data == null)
                    continue;

                RarityRow row = m_scene.RarityTable.Get(data.Rarity);
                int comboIndex = m_buffer.Count - 1 - i;
                float combo = 1f + comboIndex * session.ComboStep;
                float depthBonus = 1f + Mathf.Clamp01(record.AttachedDepth * depthNorm) * session.DepthBonusScale;

                m_result.Add(new FishScoreItem
                {
                    Id = data.Id,
                    DisplayName = data.DisplayName,
                    Rarity = data.Rarity,
                    Stars = row != null ? row.StarCount : 0,
                    Coins = Mathf.RoundToInt(data.CoinValue * depthBonus * combo),
                    CatchDepth = record.AttachedDepth,
                    PrimaryColor = row != null ? row.PrimaryColor : Color.white
                });
            }
        }

        private void recycleLandedFish()
        {
            for (int i = m_buffer.Count - 1; i >= 0; i--)
            {
                FishInstance fish = m_buffer[i].Fish;

                if (fish == null)
                    continue;

                if (fish.IsAlive)
                    m_factory.Despawn(fish.Data, fish.View);

                fish.IsHooked = false;
                m_spawn.Remove(fish);
            }
        }

        // 封盘：捕获数达标就停掉玩法驱动，把结果交给结算面板
        private void finishSession()
        {
            m_running = false;
            m_result.Finish(m_totalCatch, m_maxDepth);

            EventManager.PublishEvent(EventDefines.FishingSessionEnded, m_result);
            GameApp.UIManager.Open<UIResultPanel>(m_result).Forget();

            QLog.Info($"[{nameof(FishingController)}] 本局渔获 {m_result.TotalCoins} 金币，" +
                      $"目标 {TargetCoins}，带回 {m_totalCatch} 条，最大下潜 {m_maxDepth:0} 米");
        }

        // 再来一局：清空计数与鱼群重新开局，视图与控制器都不必重建
        private void restartSession()
        {
            GameApp.UIManager.Close<UIResultPanel>();

            m_totalCatch = 0;
            m_maxDepth = 0f;
            m_buffer.Clear();
            m_result = new SessionResult(TargetCoins);

            m_spawn.EndSession();
            m_carry.BeginSession(m_stats, m_scene.RarityTable);
            m_spawn.BeginSession(m_scene.SessionConfig, m_scene.RarityTable, m_stats, m_factory,
                m_scene.FishRoot, m_scene.CameraRig.ViewHalfWidth);

            m_machine.Start(m_hook);
            m_running = true;

            QLog.Info($"[{nameof(FishingController)}] 开新一局，目标 {TargetCoins} 金币，在场鱼 {m_spawn.Alive.Count} 条");
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