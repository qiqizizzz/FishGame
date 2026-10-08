/*
* ┌──────────────────────────────────┐
* │  描    述: 商店背包面板预览工具，绕开回合流程直接看版式
* │  类    名: UIShopPanelPreview.cs
* │  创    建: By qiqizizzz
* └──────────────────────────────────┘
* Play 模式走 UIManager 正常打开，Edit 模式只往场景里摆一份实例供目视
* 编辑模式生成的实例不写回场景文件，看完 Ctrl+Z 撤销即可
*/

using Cysharp.Threading.Tasks;
using Game;
using Module.Fish.Model;
using UI.Shop;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class UIShopPanelPreview
{
    private const string PANEL_PATH = "Assets/Prefabs/UI/UIShopPanel.prefab";
    private const string UI_ROOT_NAME = "UIRoot";

    // 预览用的假数据：3 次下潜、首回合目标 150、目标逐回合 1.8 倍
    private const int DEMO_DIVES = 3;
    private const int DEMO_TARGET = 150;
    private const float DEMO_GROWTH = 1.8f;

    [MenuItem("Tools/钓鱼游戏/预览 商店背包面板")]
    private static void openPanel()
    {
        if (EditorApplication.isPlaying)
        {
            if (GameApp.UIManager == null)
            {
                Debug.LogError($"[{nameof(UIShopPanelPreview)}] GameApp 未初始化，请从 Game 场景进入 Play");
                return;
            }

            GameApp.UIManager.Open<UIShopPanel>(createDemoState()).Forget();
            return;
        }

        previewInEditMode();
    }

    // 编辑模式手动跑一遍 Init 与 Open，让占位内容显示出来
    private static void previewInEditMode()
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
        panel.Open(createDemoState());

        Selection.activeGameObject = instance;
        Debug.Log($"[{nameof(UIShopPanelPreview)}] 已生成预览实例，看完撤销即可，不要保存场景");
    }

    private static RunState createDemoState()
    {
        return new RunState(DEMO_DIVES, DEMO_TARGET, DEMO_GROWTH);
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
