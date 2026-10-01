/*
 * ┌──────────────────────────────────┐
 * │  描    述: 游戏场景入口，负责跨场景驱动游戏应用生命周期
 * │  类    名: GameScene.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 */

using Framework.QTower;
using UnityEngine;
using Utils.Find;

namespace Game
{
    public sealed class GameScene : MonoBehaviour
    {
        private const string UI_ROOT_NAME = "UIRoot";

        private static GameScene S_instance;

        private Transform m_uiRoot;

        private void Awake()
        {
            if (S_instance != null && S_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            m_uiRoot = HierarchyFindTool.FindSceneRoot(UI_ROOT_NAME);
            if (m_uiRoot == null)
                return;

            S_instance = this;
            DontDestroyOnLoad(gameObject);
            DontDestroyOnLoad(m_uiRoot.gameObject);
            GameApp.Instance.Init();
            GameApp.UIManager.SetRoot(m_uiRoot);
        }

        private void Start()
        {
            if (S_instance != this)
                return;

            RegisterModules();
        }

        private void Update()
        {
            if (S_instance != this)
                return;

            GameApp.Instance.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (S_instance != this)
                return;

            GameApp.Instance.Destroy();
            S_instance = null;
        }

        // 注册游戏场景模块
        private void RegisterModules()
        {
            GameApp.ControllerManager.Register(ControllerType.Game, new GameController());
        }
    }
}
