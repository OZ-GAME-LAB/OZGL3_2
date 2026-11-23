using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerActionSound : MonoBehaviour
{
    [Header("생활 동작")]
    [SerializeField] private AudioClip _chop, _mine, _harvest, _splash;
    [SerializeField, Range(0, 1)] private float _chopVolume = 0.24f, _mineVolume = 0.26f,
        _harvestVolume = 0.32f, _splashVolume = 0.18f;
    [SerializeField, Range(0, 1)] private float _miningContact = 0.34f / 1.93f;
    [SerializeField, Range(0, 1)] private float _waterContact = 2.45f / 3.33f;
    [Header("착지")]
    [SerializeField] private AudioClip _landing;
    [SerializeField, Range(0, 1)] private float _landingVolume = 0.32f;
    [SerializeField, Min(0.1f)] private float _landingHeight = 9f;

    private static readonly int Logging = Animator.StringToHash("Base Layer.Logging");
    private static readonly int Mining = Animator.StringToHash("Base Layer.Mining");
    private static readonly int Casting = Animator.StringToHash("Base Layer.FishingCast");
    private PlayerResourceAnimationState _lastAction;
    private float _lastTime;
    private bool _sampled, _hasGround, _airborne;
    private float _peak;

    public bool ObserveMotionStart(PlayerResourceAnimationState action, float normalized)
    {
        if (_sampled && action.Equals(_lastAction)) return false;
        _lastAction = action;
        _lastTime = normalized;
        _sampled = true;
        return true;
    }

    public void TickMotion(Animator animator, PlayerResourceAnimationState action, float loggingContact)
    {
        int hash;
        float contact, volume;
        AudioClip clip;
        if (action.Kind == ResourceAnimationKind.Logging && action.Phase == ResourceAnimationPhase.Swing)
            (hash, contact, clip, volume) = (Logging, loggingContact, _chop, _chopVolume);
        else if (action.Kind == ResourceAnimationKind.Mining && action.Phase == ResourceAnimationPhase.Active)
            (hash, contact, clip, volume) = (Mining, _miningContact, _mine, _mineVolume);
        else if (action.Kind == ResourceAnimationKind.Fishing && action.Phase == ResourceAnimationPhase.Casting)
            (hash, contact, clip, volume) = (Casting, _waterContact, _splash, _splashVolume);
        else { _sampled = false; return; }

        if (animator == null || !animator.isActiveAndEnabled) { _sampled = false; return; }
        AnimatorStateInfo pose = animator.GetCurrentAnimatorStateInfo(0);
        if (pose.fullPathHash != hash) return;
        float now = hash == Mining ? pose.normalizedTime : Mathf.Clamp01(pose.normalizedTime);
        if (_sampled && action.Equals(_lastAction) && now >= _lastTime &&
            Mathf.FloorToInt(now - contact) > Mathf.FloorToInt(_lastTime - contact))
            Play(clip, volume);
        _lastAction = action;
        _lastTime = now;
        _sampled = true;
    }

    public void PlayHarvest() => Play(_harvest, _harvestVolume);

    public bool TickGround(bool grounded, float y)
    {
        if (!grounded)
        {
            _peak = Mathf.Max(_peak, y);
            _airborne = _hasGround;
            return false;
        }
        bool landed = _airborne && _peak - y >= _landingHeight;
        SetFallOrigin(y);
        if (landed) Play(_landing, _landingVolume);
        return landed;
    }

    public void SetFallOrigin(float y)
    {
        _hasGround = true;
        _airborne = false;
        _peak = y;
    }

    public void ResetTracking()
    {
        _sampled = false;
        ResetGround();
    }

    public void ResetGround()
    {
        _hasGround = _airborne = false;
        _peak = float.NegativeInfinity;
    }

    private static void Play(AudioClip clip, float volume)
    {
        if (clip != null && SoundManager.Inst != null) SoundManager.Inst.PlaySfx(clip, volume);
    }
}
