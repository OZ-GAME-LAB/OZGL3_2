using UnityEngine;

/// <summary>Controller가 허용한 지상 이동의 발 접촉 시점에 효과음을 재생합니다.</summary>
[DisallowMultipleComponent]
public sealed class PlayerFootstepSound : MonoBehaviour
{
    [Header("공통 발소리")]
    [SerializeField] private AudioClip[] _clips;
    [SerializeField, Range(0f, 1f)] private float _volume = 0.2f;
    [SerializeField, Range(0f, 0.3f)] private float _volumeVariation = 0.1f;
    [SerializeField, Range(0f, 1f)] private float _crouchVolumeMultiplier = 0.6f;

    [Header("모션 한 바퀴의 발 접촉 시점 (0~1)")]
    [SerializeField] private Vector2 _runContacts = new Vector2(1f / 19f, 11f / 19f);
    [SerializeField] private Vector2 _crouchContacts = new Vector2(6f / 29f, 21f / 29f);

    private static readonly int RunHash = Animator.StringToHash("Base Layer.Run");
    private static readonly int CrouchWalkHash = Animator.StringToHash("Base Layer.CrouchWalk");
    private int _lastStateHash;
    private float _lastNormalizedTime;
    private int _lastClipIndex = -1;
    private float _nextAllowedTime;
    private bool _tracking;
    // 상태 전환이나 프레임 지연으로 두 발소리가 붙어 나오는 것을 방지합니다.
    private const float MinimumInterval = 0.15f;

    public void Tick(Animator animator, bool canStep, bool crouching)
    {
        if (!isActiveAndEnabled || !canStep || Time.deltaTime <= 0f ||
            animator == null || !animator.isActiveAndEnabled || SoundManager.Inst == null)
        {
            ResetTracking();
            return;
        }

        // 전환 중에는 화면에 새로 들어오는 상태 하나만 추적합니다.
        AnimatorStateInfo state = animator.IsInTransition(0)
            ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        if (state.fullPathHash != RunHash && state.fullPathHash != CrouchWalkHash)
        {
            ResetTracking();
            return;
        }

        float now = state.normalizedTime;
        if (!_tracking || _lastStateHash != state.fullPathHash || now < _lastNormalizedTime)
        {
            _tracking = true;
            _lastStateHash = state.fullPathHash;
            _lastNormalizedTime = now;
            return;
        }

        Vector2 contacts = state.fullPathHash == CrouchWalkHash ? _crouchContacts : _runContacts;
        bool crossed = CrossedContact(_lastNormalizedTime, now, contacts.x) ||
            CrossedContact(_lastNormalizedTime, now, contacts.y);
        // 프레임이 지연되어도 지나간 발소리를 한꺼번에 재생하지 않습니다.
        _lastNormalizedTime = now;
        if (crossed && Time.time >= _nextAllowedTime)
            PlayStep(crouching);
    }

    private static bool CrossedContact(float previous, float current, float contact)
    {
        contact = Mathf.Clamp(contact, 0f, 0.9999f);
        return Mathf.FloorToInt(current - contact) > Mathf.FloorToInt(previous - contact);
    }

    private void PlayStep(bool crouching)
    {
        if (_clips == null || _clips.Length == 0) return;
        // 빈 슬롯과 중복 등록을 제외하고 직전과 다른 클립을 선택합니다.
        AudioClip previous = _lastClipIndex >= 0 && _lastClipIndex < _clips.Length
            ? _clips[_lastClipIndex] : null;
        int candidateCount = 0;
        int fallback = -1;
        for (int i = 0; i < _clips.Length; i++)
        {
            if (_clips[i] == null) continue;
            fallback = i;
            if (_clips[i] != previous) candidateCount++;
        }
        if (fallback < 0) return;

        int selected = fallback;
        if (candidateCount > 0)
        {
            int choice = Random.Range(0, candidateCount);
            for (int i = 0; i < _clips.Length; i++)
            {
                if (_clips[i] == null || _clips[i] == previous) continue;
                if (choice-- == 0) { selected = i; break; }
            }
        }

        float volume = _volume * Random.Range(1f - _volumeVariation, 1f + _volumeVariation);
        if (crouching) volume *= _crouchVolumeMultiplier;
        SoundManager.Inst.PlaySfx(_clips[selected], volume);
        _lastClipIndex = selected;
        _nextAllowedTime = Time.time + MinimumInterval;
    }

    public void SuppressLandingStep()
    {
        ResetTracking();
        _nextAllowedTime = Time.time + MinimumInterval;
    }

    public void ResetTracking()
    {
        _tracking = false;
    }

    private void OnDisable()
    {
        ResetTracking();
    }
}
