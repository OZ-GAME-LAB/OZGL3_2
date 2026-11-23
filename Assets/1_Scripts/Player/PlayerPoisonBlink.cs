using UnityEngine;

/// <summary>공유된 중독 상태를 읽어 플레이어 몸체 색상만 변경합니다.</summary>
[DisallowMultipleComponent]
public sealed class PlayerPoisonBlink : MonoBehaviour
{
    [Header("플레이어 참조")]
    [SerializeField] private PlayerNetworkStats _stats;
    [SerializeField] private SpriteRenderer _spriteRenderer;

    [Header("중독 깜빡임")]
    [SerializeField] private Color _poisonColor = new Color(0.7f, 0.2f, 1f, 1f);
    [Tooltip("보라색으로 깜빡이기 시작하는 간격입니다. 1이면 초당 한 번입니다.")]
    [SerializeField, Min(0.02f)] private float _blinkInterval = 1f;
    [Tooltip("한 주기에서 보라색을 유지하는 시간입니다. 반복 간격보다 짧아야 합니다.")]
    [SerializeField, Min(0.01f)] private float _purpleDuration = 0.2f;

    private Color _originalColor;
    private double _blinkStartedAt;
    private bool _isBlinking;
    private bool _isPurple;
    [SerializeField] private AudioClip _hitSound;
    [SerializeField] private AudioClip _poisonTickSound;
    [SerializeField, Range(0f, 1f)] private float _poisonTickVolume = 0.10f;
    [SerializeField, Range(0f, 1f)] private float _hitVolume = 0.26f;
    private float _hitUntil;
    private bool _showingHit;

    private void OnEnable()
    {
        if (_stats != null) _stats.Damaged += OnHit;
        if (_stats != null) _stats.DamageSoundRequested += PlayDamageSound;
    }

    private void OnHit()
    {
        if (!_isBlinking && !_showingHit) _originalColor = _spriteRenderer.color;
        _hitUntil = Time.time + 0.13f;
        _showingHit = true;
        ApplyColor(new Color(1f, 0.4f, 0.4f));
    }

    private void PlayDamageSound(bool poisonTick)
    {
        AudioClip clip = poisonTick ? _poisonTickSound : _hitSound;
        float volume = poisonTick ? _poisonTickVolume : _hitVolume;
        var listener = _stats.NetworkManager?.LocalClient?.PlayerObject;
        if (listener == null || clip == null || SoundManager.Inst == null) return;
        float gain = Mathf.Clamp01(1f - Vector2.Distance(listener.transform.position, transform.position) / 20f);
        if (gain > 0f) SoundManager.Inst.PlaySfx(clip, gain * volume);
    }

    private void Awake()
    {
        if (_stats != null && _spriteRenderer != null) return;

        Debug.LogWarning("[PlayerPoisonBlink] PlayerNetworkStats와 몸체 SpriteRenderer를 연결해 주세요.", this);
        enabled = false;
    }

    private void Update()
    {
        if (_showingHit)
        {
            if (_stats.IsAlive && Time.time < _hitUntil) return;
            _showingHit = false;
            ApplyColor(_originalColor);
            _isPurple = false;
        }
        if (_stats == null || _spriteRenderer == null || !_stats.IsAlive ||
            (!_stats.IsPoisoned && !_stats.IsBossPoisoned))
        {
            StopBlinking();
            return;
        }

        if (!_isBlinking)
            StartBlinking();

        UpdateBlinkColor();
    }

    private void StartBlinking()
    {
        _originalColor = _spriteRenderer.color;
        _blinkStartedAt = Time.timeAsDouble;
        _isBlinking = true;
        _isPurple = false;
    }

    private void UpdateBlinkColor()
    {
        float interval = Mathf.Max(0.02f, _blinkInterval);
        float duration = Mathf.Clamp(_purpleDuration, 0.01f, interval - 0.01f);
        // 프레임 지연 시 지나간 깜빡임을 몰아서 재생하지 않고 현재 주기의 색을 표시합니다.
        double elapsedInCycle = (Time.timeAsDouble - _blinkStartedAt) % interval;
        bool showPurple = elapsedInCycle < duration;
        if (_isPurple == showPurple) return;

        _isPurple = showPurple;
        ApplyColor(showPurple ? _poisonColor : _originalColor);
    }

    private void ApplyColor(Color color)
    {
        // 다른 연출이 사용하는 투명도는 유지하고 RGB만 변경합니다.
        color.a = _spriteRenderer.color.a;
        _spriteRenderer.color = color;
    }

    private void StopBlinking()
    {
        if (!_isBlinking) return;

        if (_spriteRenderer != null)
            ApplyColor(_originalColor);

        _isBlinking = false;
        _isPurple = false;
    }

    private void OnDisable()
    {
        if (_stats != null) _stats.Damaged -= OnHit;
        if (_stats != null) _stats.DamageSoundRequested -= PlayDamageSound;
        if (_showingHit && _spriteRenderer != null) ApplyColor(_originalColor);
        _showingHit = false;
        StopBlinking();
    }
}
