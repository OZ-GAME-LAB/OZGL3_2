using UnityEngine;
using UnityEngine.UI;

public class SettingUI : UIBase
{
    [SerializeField] private Slider _masterSlider;
    [SerializeField] private Slider _bgmSlider;
    [SerializeField] private Slider _sfxSlider;
    [SerializeField] private Button Button_Close;
    [SerializeField] private Button Button_QuitGame;

    private SoundManager _boundManager;
    private bool _isQuitting;

    private void Awake()
    {
        Button_Close.onClick.AddListener(OnClose);
        Button_QuitGame.onClick.AddListener(OnQuitGame);
    }
    
    private void OnEnable()
    {
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
}
