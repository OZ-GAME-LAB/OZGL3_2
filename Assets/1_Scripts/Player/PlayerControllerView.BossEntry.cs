using UnityEngine;

public partial class PlayerControllerView
{
    private bool _bossEntryWaiting;
    private RigidbodyConstraints2D _bossEntryPreviousConstraints;

    public void SetBossEntryWaiting(bool waiting)
    {
        if (_bossEntryWaiting == waiting) return;
        if (waiting && (_networkObject == null || !_networkObject.IsOwner || IsDeathControlLocked || IsEncounterControlLocked)) return;
        _bossEntryWaiting = waiting;
        if (waiting)
        {
            _runeAttackController?.CancelAttackInput();
            SetMoveInput(0f);
            ResetAirborneStateAndAnimation();
            _wallCling?.ResetState();
            CancelScreenTransition();
            if (_rigidbody2D != null)
            {
                _bossEntryPreviousConstraints = _rigidbody2D.constraints;
                _rigidbody2D.linearVelocity = Vector2.zero;
                _rigidbody2D.constraints = RigidbodyConstraints2D.FreezeAll;
            }
        }
        else if (_rigidbody2D != null && !_encounterLocalLocked && _rigidbody2D.constraints == RigidbodyConstraints2D.FreezeAll)
            _rigidbody2D.constraints = _bossEntryPreviousConstraints;
    }
}
