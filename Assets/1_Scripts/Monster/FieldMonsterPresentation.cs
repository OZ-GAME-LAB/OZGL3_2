using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class FieldMonsterPresentation : MonoBehaviour
{
    [SerializeField] private Sprite[] _idle;
    [SerializeField] private Sprite[] _move;
    [SerializeField] private Sprite[] _attack;
    [SerializeField] private Sprite[] _death;
    [SerializeField, Min(0f)] private float _meleePreparation;
    [SerializeField, Min(0)] private int _contactFrame;
    [SerializeField] private AudioClip _hitSound;
    [SerializeField] private AudioClip _poisonTickSound;
    [SerializeField, Range(0f, 1f)] private float _poisonTickVolume = 0.10f;
    [SerializeField, Range(0f, 1f)] private float _hitVolume = 0.26f;
    public float MeleePreparation => _meleePreparation;
    [SerializeField, Min(1f)] private float _framesPerSecond = 10f;
    [Header("공격음")]
    [SerializeField] private AudioClip _attackSound;
    [SerializeField, Range(0, 1)] private float _volume;
    [SerializeField, Min(1f)] private float _audibleRange = 20f;
    [SerializeField] private AudioClip _deathSound;
    [SerializeField, Range(0, 1)] private float _deathVolume;

    public float GetDeathDuration()
    {
        if (_death == null || _framesPerSecond < 1f || !float.IsFinite(_framesPerSecond))
            return 0f;
        foreach (Sprite frame in _death)
            if (frame == null) return 0f;
        return _death.Length / _framesPerSecond;
    }

    private MonsterNetwork _monster;
    private SpriteRenderer _renderer;
    private Sprite[] _sequence;
    private float _lastX;
    private float _sequenceStart;
    private float _attackEnd;
    private float _movingUntil;
    private float _hitUntil;
    private Color _baseColor;

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _baseColor = _renderer.color;
        _monster = GetComponentInParent<MonsterNetwork>();
    }

    private void OnEnable()
    {
        _lastX = transform.position.x;
        if (_monster != null) _monster.AttackPresented += OnAttack;
        if (_monster != null) _monster.Damaged += OnHit;
        if (_monster != null) _monster.DamageSoundRequested += PlayDamageSound;
    }

    private void OnDisable()
    {
        if (_monster != null) _monster.AttackPresented -= OnAttack;
        if (_monster != null) _monster.Damaged -= OnHit;
        if (_monster != null) _monster.DamageSoundRequested -= PlayDamageSound;
        _renderer.color = _baseColor;
    }

    private void OnHit()
    {
        _hitUntil = Time.time + 0.13f;
    }

    private void PlayDamageSound(bool poisonTick)
    {
        PlaySound(poisonTick ? _poisonTickSound : _hitSound, poisonTick ? _poisonTickVolume : _hitVolume);
    }

    private void OnAttack(float direction)
    {
        if (_monster.DeathStartTime >= 0d) return;
        if (Mathf.Abs(direction) > 0.001f) _renderer.flipX = direction < 0f;
        if (_attack.Length > 0)
        {
            _attackEnd = Time.time + (_attack.Length - ContactFrame) / _framesPerSecond;
            _sequenceStart = Time.time;
        }

        PlaySound(_attackSound, _volume);
    }

    public void PlayDeathSound() => PlaySound(_deathSound, _deathVolume);
    private int ContactFrame => Mathf.Clamp(_contactFrame, 0, _attack.Length - 1);

    private void PlaySound(AudioClip clip, float volume)
    {
        var listener = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        if (clip == null || listener == null || SoundManager.Inst == null) return;
        float distance = Vector2.Distance(listener.transform.position, transform.position);
        if (distance < _audibleRange)
            SoundManager.Inst.PlaySfx(clip, volume * (1f - distance / _audibleRange));
    }

    private void LateUpdate()
    {
        _renderer.color = _baseColor;
        if (_monster != null && _monster.IsSpawned && _monster.DeathStartTime >= 0d)
        {
            if (_death != null && _death.Length > 0)
            {
                double elapsed = _monster.NetworkManager.ServerTime.Time - _monster.DeathStartTime;
                int frame = Mathf.Clamp(Mathf.FloorToInt((float)elapsed * _framesPerSecond), 0, _death.Length - 1);
                _renderer.sprite = _death[frame];
            }
            return;
        }
        float x = transform.position.x;
        float step = x - _lastX;
        _lastX = x;
        bool attacking = Time.time < _attackEnd;
        double untilContact = _monster != null && _monster.IsSpawned
            ? _monster.MeleeContactAt - _monster.NetworkManager.ServerTime.Time : 0d;
        bool preparing = _meleePreparation > 0f && untilContact > 0d;
        if (Time.time < _hitUntil) _renderer.color = new Color(1f, 0.4f, 0.4f, _baseColor.a);
        else if (preparing) _renderer.color = new Color(1f, 0.76f, 0.33f, _baseColor.a);
        if (preparing && _attack.Length > 0)
        {
            _renderer.flipX = _monster.MeleeFacing < 0f;
            float progress = Mathf.Clamp01(1f - (float)untilContact / _meleePreparation);
            _renderer.sprite = _attack[Mathf.FloorToInt(progress * ContactFrame)];
            return;
        }
        if (attacking && _attack.Length > 0)
        {
            int frame = ContactFrame + Mathf.FloorToInt((Time.time - _sequenceStart) * _framesPerSecond);
            _renderer.sprite = _attack[Mathf.Min(frame, _attack.Length - 1)];
            _sequence = null;
            return;
        }
        if (Mathf.Abs(step) > 0.001f)
        {
            _movingUntil = Time.time + 0.12f;
            if (!attacking) _renderer.flipX = step < 0f;
        }
        Sprite[] frames = attacking ? _attack : Time.time < _movingUntil ? _move : _idle;
        if (frames.Length == 0) return;
        if (_sequence != frames)
        {
            _sequence = frames;
            _sequenceStart = Time.time;
        }
        _renderer.sprite = frames[Mathf.FloorToInt((Time.time - _sequenceStart) * _framesPerSecond) % frames.Length];
    }
}
