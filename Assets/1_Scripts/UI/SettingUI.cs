using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class SettingUI : UIBase
{
    [SerializeField] private Slider _masterSlider;
    [SerializeField] private Slider _bgmSlider;
    [SerializeField] private Slider _sfxSlider;
    [SerializeField] private Button Button_Close;
    [SerializeField] private Button Button_QuitGame;
    [SerializeField] private Button Button_ReturnToLobby;

    private SoundManager _boundManager;
    private bool _isQuitting;

    private void Awake()
    {
        Button_Close.onClick.AddListener(OnClose);
        Button_QuitGame.onClick.AddListener(OnQuitGame);
        Button_ReturnToLobby.onClick.AddListener(OnClickReturnToLobby);
    }
    
    private void OnEnable()
    {
        NetworkManager network = NetworkManager.Singleton;
        bool inGame = network != null && network.IsListening && GameStartSelection.HasSelection;

        Button_ReturnToLobby.gameObject.SetActive(inGame);
        Button_ReturnToLobby.interactable = inGame;

        _boundManager = SoundManager.Inst;
        
        if (_boundManager == null)
        {
            return;
        }

        BindSlider(_masterSlider, _boundManager.MasterVolume, OnMasterChanged);
        BindSlider(_bgmSlider, _boundManager.BgmVolume, OnBgmChanged);
        BindSlider(_sfxSlider, _boundManager.SfxVolume, OnSfxChanged);
    }

    private void OnDisable()
    {
        if (_masterSlider != null)
        {
            _masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
        }

        if (_bgmSlider != null)
        {
            _bgmSlider.onValueChanged.RemoveListener(OnBgmChanged);
        }

        if (_sfxSlider != null)
        {
            _sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
        }
    }

    private void OnDestroy()
    {
        if (Button_Close != null)
        {
            Button_Close.onClick.RemoveListener(OnClose);
            Button_QuitGame.onClick.RemoveListener(OnQuitGame);
        }

        if (Button_QuitGame != null)
        {
            Button_QuitGame.onClick.RemoveListener(OnQuitGame);
        }

        if (Button_ReturnToLobby != null)
        {
            Button_ReturnToLobby.onClick.RemoveListener(OnClickReturnToLobby);
        }
    }

    private void BindSlider(Slider slider, float value, UnityEngine.Events.UnityAction<float> callback)
    {
        if (slider == null)
        {
            return;
        }

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;

        slider.SetValueWithoutNotify(value);
        slider.onValueChanged.AddListener(callback);
    }

    private void OnMasterChanged(float value)
    {
        if (_boundManager != null)
        {
            _boundManager.SetMasterVolume(value);
        }
    }

    private void OnBgmChanged(float value)
    {
        if (_boundManager != null)
        {
            _boundManager.SetBgmVolume(value);
        }
    }

    private void OnSfxChanged(float value)
    {
        if (_boundManager != null)
        {
            _boundManager.SetSfxVolume(value);  
        }
    }

    private void OnClose()
    {
        if (UIManager.Inst != null)
        {
            UIManager.Inst.CloseSettingUI();
        }
    }

    private void OnQuitGame()
    {
        if (_isQuitting)
        {
            return;
        }

        _isQuitting = true;

        if (Button_QuitGame != null)
        {
            Button_QuitGame.interactable = false;
        }

        // 현재는 사운드 설정만 저장.
        if (SoundManager.Inst != null)
        {
            SoundManager.Inst.SaveSettings();
        }

        Application.Quit();
    }

    private void OnClickReturnToLobby()
    {
        ReturnToLobbyAsync().Forget();
    }

    private async UniTask ReturnToLobbyAsync()
    {
        NetCodeNetworkManager manager = NetCodeNetworkManager.Inst;

        if (manager == null || manager.IsReturningToLobby)
        {
            return;
        }

        Button_ReturnToLobby.interactable = false;

        try
        {
            await manager.TryReturnToLobbyAsync();
        }
        finally
        {
            if (this != null && Button_ReturnToLobby != null)
            {
                Button_ReturnToLobby.interactable = true;
            }
        }
    }
}
