using Newtonsoft.Json.Linq;
using UnityEngine;

// 사운드 매니저 사용하실 때 사용하고 싶은 오디오 클립을 시리얼라이즈 필드에 등록하시고, SoundManager.Inst.PlayBgm(클립) 또는 SoundManager.Inst.PlaySfx(클립)으로 재생하시면 됩니다. 

public class SoundManager : MonoBehaviour
{
    public static SoundManager Inst { get; private set; }

    private const string MasterVolumeKey = "Sound.MasterVolume";
    private const string BgmVolumeKey = "Sound.BgmVolume";
    private const string SfxVolumeKey = "Sound.SfxVolume";

    private AudioSource _bgmSource;
    private AudioSource _sfxSource;

    private bool _settingDirty;

    public float MasterVolume { get; private set; } = 1f;
    public float BgmVolume { get; private set; } = 1f;
    public float SfxVolume { get; private set; } = 1f;

    private void Awake()
    {
        if (Inst != null && Inst != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Inst = this;
        DontDestroyOnLoad(gameObject);

        _bgmSource = CreateAudioSource("BGM", true);
        _sfxSource = CreateAudioSource("SFX", false);

        LoadSettings();
        ApplyVolumes();
    }

    private AudioSource CreateAudioSource(string objectName, bool loop)
    {
        GameObject sourceObject = new GameObject(objectName);
        sourceObject.transform.SetParent(transform, false);

        AudioSource source = sourceObject.AddComponent<AudioSource>();

        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;

        return source;
    }

    // 사운드 연결할 때 아래 메서드들을 사용하시면 됩니다. BGM : 배경음, SFX : 효과음
    public void PlayBgm(AudioClip clip)
    {
        if (clip == null || _bgmSource == null)
        {
            return;
        }

        if (_bgmSource.clip == clip && _bgmSource.isPlaying)
        {
            return;
        }

        _bgmSource.Stop();
        _bgmSource.clip = clip;
        _bgmSource.Play();
    }

    public void StopBgm()
    {
        if (_bgmSource == null)
        {
            return;
        }

        _bgmSource.Stop();
        _bgmSource.clip = null;
    }

    public void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || _sfxSource == null)
        {
            return;
        }

        if (float.IsNaN(volumeScale) || float.IsInfinity(volumeScale))
        {
            return;
        }

        _sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    public void StopAllsfx()
    {
        if (_sfxSource == null)
        {
            return;
        }

        _sfxSource.Stop();
    }

    public void SetMasterVolume(float value)
    {
        MasterVolume = SanitizeVolume(value, MasterVolume);
        ApplyVolumes();
        _settingDirty = true;
    }

    public void SetBgmVolume(float value)
    {
        BgmVolume = SanitizeVolume(value, BgmVolume);
        ApplyVolumes();
        _settingDirty = true;
    }

    public void SetSfxVolume(float value)
    {
        SfxVolume = SanitizeVolume(value, SfxVolume);
        ApplyVolumes();
        _settingDirty = true;
    }

    private void ApplyVolumes()
    {
        if (_bgmSource != null)
        {
            _bgmSource.volume = MasterVolume * BgmVolume;
        }

        if (_sfxSource != null)
        {
            _sfxSource.volume = MasterVolume * SfxVolume;
        }
    }

    // 볼륨 세팅 값들을 저장하는 메서드입니다.
    public void SaveSettings()
    {
        if (_settingDirty == false)
        {
            return;
        }

        PlayerPrefs.SetFloat(MasterVolumeKey, MasterVolume);
        PlayerPrefs.SetFloat(BgmVolumeKey, BgmVolume);
        PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume);
        PlayerPrefs.Save();

        _settingDirty = false;
    }

    private void LoadSettings()
    {
        MasterVolume = SanitizeVolume(PlayerPrefs.GetFloat(MasterVolumeKey, 1f), 1f);

        BgmVolume = SanitizeVolume(PlayerPrefs.GetFloat(BgmVolumeKey, 1f), 1f);

        SfxVolume = SanitizeVolume(PlayerPrefs.GetFloat(SfxVolumeKey, 1f), 1f);
    }

    private static float SanitizeVolume(float value, float fallback)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return fallback;
        }

        return Mathf.Clamp01(value);
    }

    private void OnApplicationPause(bool paused)
    {
        if (Inst == this && paused)
        {
            SaveSettings();
        }
    }

    private void OnApplicationQuit()
    {
        if (Inst == this)
        {
            SaveSettings();
        }
    }

    private void OnDestroy()
    {
        if (Inst != this)
        {
            return;
        }

        SaveSettings();
        Inst = null;
    }
}
