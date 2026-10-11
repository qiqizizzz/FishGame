/*
 * ┌──────────────────────────────────┐
 * │  描    述: 主菜单界面
 * │  类    名: UIMainMenu.cs
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 */

using Cysharp.Threading.Tasks;
using Framework.QTower.Common.Defines;
using Framework.QTower.Common.Sound;
using Framework.QTower.Event;
using Framework.QTower.View;
using Game;
using UI.Setting;
using UnityEngine;
using UnityEngine.UI;

namespace UI.MainMenu
{
    public class UIMainMenu : UIBase
    {
        private Button m_startButton;
        private Button m_settingButton;
        private Button m_exitButton;

        // 主菜单一开就接管 BGM，从商店退回时也由这里把场景音乐接回去
        protected override void OnOpen(params object[] args)
        {
            SoundManager.PlayBGM(SFXDefines.BGM_MAIN_MENU);
        }

        // 回调统一在 OnInit 绑定，界面关闭后复用缓存实例时 OnOpen 会重复触发
        protected override void OnInit()
        {
            m_startButton = FindComponent<Button>("Btns_Main/Btn_Start");
            m_settingButton = FindComponent<Button>("Btns_Main/Btn_Setting");
            m_exitButton = FindComponent<Button>("Btns_Main/Btn_Exit");

            m_startButton.onClick.AddListener(OnStartButtonClicked);
            m_settingButton.onClick.AddListener(OnSettingButtonClicked);
            m_exitButton.onClick.AddListener(OnExitButtonClicked);
        }

        private void OnStartButtonClicked()
        {
            EventManager.PublishEvent(EventDefines.EnterFishingRequested);
        }

        private void OnSettingButtonClicked()
        {
            GameApp.UIManager.Open<UISetting>().Forget();
        }

        private void OnExitButtonClicked()
        {
            Application.Quit();
        }
    }
}
