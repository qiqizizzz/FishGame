/*
 * ┌──────────────────────────────────┐
 * │  描    述: 主菜单界面                      
 * │  类    名: UIMainMenu.cs       
 * │  创    建: By qiqizizzz
 * └──────────────────────────────────┘
 */

using Cysharp.Threading.Tasks;
using Framework.QTower.View;
using Game;
using UI.Setting;
using UnityEngine;
using UnityEngine.UI;
using Utils.log;

namespace UI.MainMenu
{
    public class UIMainMenu : UIBase
    {
        private Button m_startButton;
        private Button m_settingButton;
        private Button m_exitButton;

        protected override void OnInit()
        {
            m_startButton = FindComponent<Button>("Btns_Main/Btn_Start");
            m_settingButton = FindComponent<Button>("Btns_Main/Btn_Setting");
            m_exitButton = FindComponent<Button>("Btns_Main/Btn_Exit");
        }
        
        protected override void OnOpen(params object[] args)
        {
            m_startButton.onClick.AddListener(OnStartButtonClicked);
            m_settingButton.onClick.AddListener(OnSettingButtonClicked);
            m_exitButton.onClick.AddListener(OnExitButtonClicked);
        }

        private void OnStartButtonClicked()
        {
            QLog.Info("Start button clicked");
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
