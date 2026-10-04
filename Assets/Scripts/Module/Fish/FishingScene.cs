/*
* ┌──────────────────────────────────┐
* │  描    述: 钓鱼场景入口，汇集配置与场景引用并注册场景控制器
* │  类    名: FishingScene.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* 场景只负责交出引用，玩法判定全部留在 FishingController 与各 Service 内
*/

using Framework.QTower;
using Game;
using Module.Fish.Data;
using Module.Fish.View;
using UnityEngine;
using Utils.log;

namespace Module.Fish
{
    public sealed class FishingScene : MonoBehaviour
    {
        // ==================== 字段[外部设置] ====================
        [Header("配置")]
        [SerializeField] private RarityTableSO RarityTableAsset;
        [SerializeField] private HookConfigSO HookConfigAsset;
        [SerializeField] private FishingSessionConfigSO SessionConfigAsset;

        [Header("场景引用")]
        [SerializeField] private HookViewController HookView;
        [SerializeField] private FishingCameraRig CameraRigView;
        [SerializeField] private Transform FishRootNode;

        // ==================== 属性 ====================
        public RarityTableSO RarityTable => RarityTableAsset;
        public HookConfigSO HookConfig => HookConfigAsset;
        public FishingSessionConfigSO SessionConfig => SessionConfigAsset;
        public HookViewController Hook => HookView;
        public FishingCameraRig CameraRig => CameraRigView;
        public Transform FishRoot => FishRootNode;

        // ==================== 生命周期 ====================
        private void Start()
        {
            validateReferences();

            if (GameApp.ControllerManager == null)
                return;

            GameApp.ControllerManager.Register(ControllerType.Fishing, new FishingController(this));
        }

        private void OnDestroy()
        {
            if (GameApp.ControllerManager == null)
                return;

            GameApp.ControllerManager.Unregister(ControllerType.Fishing);
        }

        // 场景引用缺失属于配置事故，编辑器下立刻报错，不带病进玩法
        private void validateReferences()
        {
            if (RarityTableAsset == null)
                QLog.Error($"[{nameof(FishingScene)}] RarityTable 未挂，鱼群权重与深度带全部失准");

            if (HookConfigAsset == null)
                QLog.Error($"[{nameof(FishingScene)}] HookConfig 未挂，钩子手感取不到基准值");

            if (SessionConfigAsset == null)
                QLog.Error($"[{nameof(FishingScene)}] SessionConfig 未挂，本局不会生成鱼");

            if (HookView == null)
                QLog.Error($"[{nameof(FishingScene)}] Hook 未挂，状态机没有可驱动的钩子");

            if (CameraRigView == null)
                QLog.Error($"[{nameof(FishingScene)}] CameraRig 未挂，镜头不会跟随钩子");

            if (FishRootNode == null)
                QLog.Error($"[{nameof(FishingScene)}] FishRoot 未挂，生成的鱼没有父节点");
        }
    }
}