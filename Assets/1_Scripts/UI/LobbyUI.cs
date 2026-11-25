using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class LobbyUI : UIBase
{
    [Header("버튼")]
    [SerializeField] private Button Button_StartGame;
    [SerializeField] private Button Button_ControlsHelp;
    [SerializeField] private Button Button_QuitGame;
    [SerializeField] private Button Button_Setting;

    [SerializeField] private AudioClip _lobbyBgm;
    [SerializeField] private AudioClip _clickSound;

    private bool _opening;
    private ControlsHelpUI _helpPopup;

    private void Awake()
    {
        Button_StartGame.onClick.AddListener(OnClickStartGame);
        Button_ControlsHelp.onClick.AddListener(OnClickControlsHelp);
        Button_QuitGame.onClick.AddListener(OnClickQuitGame);
        Button_Setting.onClick.AddListener(OnClickSetting);
    }

    private void OnEnable()
    {
        RefreshButtons();
        if (NetCodeNetworkManager.Inst == null || !NetCodeNetworkManager.Inst.IsReturningToLobby)
            PlayLobbyMusic();
    }

    public void PlayLobbyMusic()
    {
        if (_lobbyBgm != null && SoundManager.Inst != null)
        {
            SoundManager.Inst.PlayBgm(_lobbyBgm);
        }
    }

    private void OnDestroy()
    {
        Button_StartGame.onClick.RemoveListener(OnClickStartGame);
        Button_ControlsHelp.onClick.RemoveListener(OnClickControlsHelp);
        if (_helpPopup != null)
            _helpPopup.Closed -= OnHelpClosed;
        Button_QuitGame.onClick.RemoveListener(OnClickQuitGame);
        Button_Setting.onClick.RemoveListener(OnClickSetting);
    }

    private void OnClickStartGame()
    {
        PlayClickSound();
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
        OpenLobbyPopupAsync(UIType.SettingUI).Forget();
    }

    private void OnClickControlsHelp()
    {
        PlayClickSound();
        OpenLobbyPopupAsync(UIType.ControlsHelpUI).Forget();
    }

    private async UniTask OpenLobbyPopupAsync(UIType type)
    {
        UIManager uiManager = UIManager.Inst;
        if (_opening || _helpPopup != null || uiManager == null)
        {
            return;
        }

        _opening = true;
        RefreshButtons();

        try
        {
            if (type == UIType.SettingUI)
            {
                await uiManager.OpenSettingUIAsync();
                return;
            }

            ControlsHelpUI popup = await uiManager.OpenControlsHelpUIAsync();
            if (popup == null)
                return;
            if (this == null || !isActiveAndEnabled)
            {
                uiManager.Close(UIType.ControlsHelpUI);
                return;
            }

            _helpPopup = popup;
            _helpPopup.Closed += OnHelpClosed;
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

    private void PlayClickSound()
    {
        if (SoundManager.Inst != null)
        {
            SoundManager.Inst.PlaySfx(_clickSound);
        }
    }

    private void OnHelpClosed()
    {
        _helpPopup.Closed -= OnHelpClosed;
        _helpPopup = null;
        RefreshButtons();
        if (isActiveAndEnabled)
            Button_ControlsHelp.Select();
    }

    private void RefreshButtons()
    {
        bool available = !_opening && _helpPopup == null;
        Button_StartGame.interactable = available;
        Button_ControlsHelp.interactable = available;
        Button_QuitGame.interactable = available;
        Button_Setting.interactable = available;
    }
}
