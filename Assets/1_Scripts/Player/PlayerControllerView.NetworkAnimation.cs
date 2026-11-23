using UnityEngine;

public partial class PlayerControllerView
{
    private PlayerMovementNetwork _movementNetwork;
    private bool _movementGrounded = true;
    private bool _movementJumping;
    private bool _movementIdleJump;
    private uint _movementJumpSequence;
    private float _movementVerticalSpeed;
    private bool UsesRemoteMovementPresentation => _movementNetwork != null &&
        _movementNetwork.HandlesRemotePresentation;

    public PlayerMovementAnimationState CaptureMovementAnimation(uint lifeVersion, bool alive)
    {
        if (!alive || IsMovementControlLocked || !isActiveAndEnabled)
            return PlayerMovementAnimationState.Neutral(lifeVersion, _movementJumpSequence, _lookRight);
        bool clinging = _wallCling != null && _wallCling.IsClinging;
        return new PlayerMovementAnimationState
        {
            HasSample = true,
            LifeVersion = lifeVersion,
            JumpSequence = _movementJumpSequence,
            FacingRight = _lookRight,
            Moving = IsMovingForAnimation,
            Grounded = _movementGrounded && !clinging,
            Crouching = _isCrouching && !_isSpectatorMode,
            WallClinging = clinging,
            LedgeHanging = clinging && _wallCling.IsLedgeHanging,
            Jumping = _movementJumping,
            IdleJump = _movementIdleJump,
            VerticalSpeed = _movementVerticalSpeed
        };
    }

    private void ResetMovementNetworkSample()
    {
        _movementGrounded = true;
        _movementJumping = false;
        _movementIdleJump = false;
        _movementVerticalSpeed = 0f;
    }

    public void ResetRemoteMovementPresentation()
    {
        _animationPresenter?.ResetRemoteMovement();
    }

    public void ApplyRemoteMovementPresentation(PlayerMovementAnimationState state, bool valid,
        PlayerAttackState attack, PlayerResourceAnimationState resource, double now)
    {
        if (!UsesRemoteMovementPresentation) return;
        int facing = valid && resource.PlaysAnimation ? resource.FacingDirection :
            valid && attack.Step != 0 ? attack.Direction :
            valid ? (state.FacingRight ? 1 : -1) : 0;
        if (facing != 0)
        {
            _lookRight = facing > 0;
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (facing > 0 ? 1f : -1f);
            transform.localScale = scale;
        }
        if (_animationPresenter == null) return;
        bool hasGeometry = _crouchCollider != null;
        float bodyRightEdge = hasGeometry ? _crouchCollider.offset.x + _crouchCollider.size.x * 0.5f : 0f;
        _animationPresenter.UpdateRemoteMovement(state, valid, attack, resource, now,
            hasGeometry, bodyRightEdge, _wallHandLocalX);
    }
}
