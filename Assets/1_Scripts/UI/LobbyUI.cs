using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class LobbyUI : UIBase
{
    [Header("버튼")]
    [SerializeField] private Button Button_StartGame;
    [SerializeField] private Button Button_QuitGame;
    [SerializeField] private Button Button_Setting;

    private bool _opening;

    private void Awake()
    {
        Button_StartGame.onClick.AddListener(OnClickStartGame);
        Button_QuitGame.onClick.AddListener(OnClickQuitGame);
        Button_Setting.onClick.AddListener(OnClickSetting);
    }

    private void OnEnable()
    {
        RefreshButtons();
    }

    private void OnDestroy()
    {
        Button_StartGame.onClick.RemoveListener(OnClickStartGame);
        Button_QuitGame.onClick.RemoveListener(OnClickQuitGame);
        Button_Setting.onClick.RemoveListener(OnClickSetting);
    }

    private void OnClickStartGame()
    {
        OpenNetworkSelectionAsync().Forget();
    }

    private async UniTask OpenNetworkSelectionAsync()
    {
        if (_opening || UIManager.Inst == null)
        {
            return;
        }

        _opening = true;
        RefreshButtons();
        
        UIManager uiManager = UIManager.Inst;

        try
        {
            UIBase view = await uiManager.OpenVeryFrontRootAsync(UIType.NetworkSelectUI);

            if (this == null || uiManager == null || view == null)
            {
                return;
            }

            uiManager.CloseLobbyUI();
        }
        finally
        {
            if (this != null)
            {
                _opening = false;
                RefreshButtons();
            }
        }
    }

    private void OnClickQuitGame()
    {
        Application.Quit();
    }

    private void OnClickSetting()
    {
        OpenSettingAsync().Forget();
    }

    private async UniTask OpenSettingAsync()
    {
        if (_opening || UIManager.Inst == null)
        {
            return;
        }

        _opening = true;
        RefreshButtons();

        try
        {
            await UIManager.Inst.OpenSettingUIAsync();
        }
        finally
        {
            if (this != null)
            {
                _opening = false;
                RefreshButtons();
            }
        }
    }

    private void RefreshButtons()
    {
        Button_StartGame.interactable = !_opening;
        Button_QuitGame.interactable = !_opening;
        Button_Setting.interactable = !_opening;
    }
}