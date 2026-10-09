/*
* ┌──────────────────────────────────┐
* │  描    述: 商店背包面板预览工具，绕开回合流程直接看版式
* │  类    名: UIShopPanelPreview.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* Play 模式走 UIManager 正常打开，Edit 模式只往场景里摆一份实例供目视
* 编辑模式生成的实例不写回场景文件，看完 Ctrl+Z 撤销即可
* 回合数值与背包都从工程里的配置资产现读，预览和实机不会再对不上号
*/

using Cysharp.Threading.Tasks;
using Game;
using Module.Backpack.Data;
using Module.Backpack.Model;
using Module.Fish.Data;
using Module.Fish.Model;
using UI.Shop;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class UIShopPanelPreview
{
    private const string PANEL_PATH = "Assets/Prefabs/UI/UIShopPanel.prefab";
    private const string UI_ROOT_NAME = "UIRoot";
    private const string SESSION_PATH = "Assets/Data/Config/FishingSession_Island1.asset";
    private const string BACKPACK_CONFIG_PATH = "Assets/Data/Config/BackpackConfig.asset";

    [MenuItem("Tools/钓鱼游戏/预览 商店背包面板")]
    private static void openPanel()
    {
        RunState run = createRunState();
        RunBackpack backpack = createBackpack();

        if (run == null || backpack == null)
            return;

        if (EditorApplication.isPlaying)
        {
            if (GameApp.UIManager == null)
            {
                Debug.LogError($"[{nameof(UIShopPanelPreview)}] GameApp 未初始化，请从 Game 场景进入 Play");
                return;
            }

            GameApp.UIManager.Open<UIShopPanel>(run, backpack).Forget();
            return;
        }

        previewInEditMode(run, backpack);
    }

    // 编辑模式手动跑一遍 Init 与 Open，让真实数据摆到版式上
    private static void previewInEditMode(RunState run, RunBackpack backpack)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PANEL_PATH);
        if (prefab == null)
        {
            Debug.LogError($"[{nameof(UIShopPanelPreview)}] 预制体未找到：{PANEL_PATH}");
            return;
        }

        Transform uiRoot = findUiRoot();
        if (uiRoot == null)
        {
            Debug.LogError($"[{nameof(UIShopPanelPreview)}] 当前场景没有根节点 {UI_ROOT_NAME}，请打开 Game 场景后再预览");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.SetParent(uiRoot, false);
        Undo.RegisterCreatedObjectUndo(instance, "Preview UIShopPanel");

        UIShopPanel panel = instance.GetComponent<UIShopPanel>();
        panel.Init();
        panel.Open(run, backpack);

        Selection.activeGameObject = instance;
        Debug.Log($"[{nameof(UIShopPanelPreview)}] 已生成预览实例，看完撤销即可，不要保存场景");
    }

    // 回合数值直接读会话配置，改了配置不用回来动预览
    private static RunState createRunState()
    {
        FishingSessionConfigSO session = AssetDatabase.LoadAssetAtPath<FishingSessionConfigSO>(SESSION_PATH);

        if (session == null)
        {
            Debug.LogError($"[{nameof(UIShopPanelPreview)}] 会话配置未找到：{SESSION_PATH}");
            return null;
        }

        return new RunState(session.DivesPerRound, session.BaseRoundTarget, session.RoundTargetGrowth);
    }

    private static RunBackpack createBackpack()
    {
        BackpackConfigSO config = AssetDatabase.LoadAssetAtPath<BackpackConfigSO>(BACKPACK_CONFIG_PATH);

        if (config == null)
        {
            Debug.LogError($"[{nameof(UIShopPanelPreview)}] 背包配置未找到：{BACKPACK_CONFIG_PATH}，先跑「生成默认形状库与背包配置」");
            return null;
        }

        RunBackpack backpack = new RunBackpack(config);
        backpack.SeedStartingCloths();
        backpack.SeedStartingItems();

        return backpack;
    }

    // 只认场景根层的 UIRoot，与 GameScene 的取法保持一致
    private static Transform findUiRoot()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;

            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                if (rootObject.name == UI_ROOT_NAME)
                    return rootObject.transform;
            }
        }

        return null;
    }
}
