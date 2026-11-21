using UnityEngine;

public partial class PlayerControllerView
{
    private SpriteRenderer _restSprite;
    private bool _restVisualHidden;
    private bool _restSpriteWasEnabled;
    private bool _restControlLocked;
    private RigidbodyConstraints2D _restPreviousConstraints;

    public void SetRestVisualState(bool resting)
    {
        if (_restVisualHidden == resting) return;
        if (_restSprite == null && _animator != null) _animator.TryGetComponent(out _restSprite);
        _restVisualHidden = resting;
        if (_restSprite == null) return;
        if (resting)
        {
            _restSpriteWasEnabled = _restSprite.enabled;
            _restSprite.enabled = false;
        }
        else _restSprite.enabled = _restSpriteWasEnabled;
    }

    public void SetRestLocalState(bool resting)
    {
        SetRestVisualState(resting);
        if (_restControlLocked == resting) return;
        if (resting && (_networkObject == null || !_networkObject.IsOwner || IsDeathControlLocked || IsEncounterControlLocked)) return;
        _restControlLocked = resting;
        if (resting)
        {
            _runeAttackController?.CancelAttackInput();
            SetMoveInput(0f);
            ResetAirborneStateAndAnimation();
            if (_rigidbody2D != null)
            {
                _restPreviousConstraints = _rigidbody2D.constraints;
                _rigidbody2D.linearVelocity = Vector2.zero;
                _rigidbody2D.constraints = RigidbodyConstraints2D.FreezeAll;
            }
        }
        // 서버의 잠금 통지만 도착한 상태는 아직 로컬 물리 제약을 소유하지 않습니다.
        else if (_rigidbody2D != null && !IsDeathControlLocked && !_encounterLocalLocked &&
            _rigidbody2D.constraints == RigidbodyConstraints2D.FreezeAll)
            _rigidbody2D.constraints = _restPreviousConstraints;
    }
}
