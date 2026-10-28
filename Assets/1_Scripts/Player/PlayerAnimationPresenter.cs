using UnityEngine;

/// <summary>
/// Controller가 결정한 상태를 Animator와 그림 위치에 반영합니다.
/// 독립적인 Update나 물리 처리를 갖지 않아 호출 시점은 Controller가 유지합니다.
/// </summary>
public sealed class PlayerAnimationPresenter
{
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
    private static readonly int IsIdleJumpHash = Animator.StringToHash("IsIdleJump");
    private static readonly int RunningJumpHash = Animator.StringToHash("RunningJump");
    private static readonly int IdleJumpHash = Animator.StringToHash("IdleJump");
    private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
    private static readonly int IsWallClingingHash = Animator.StringToHash("IsWallClinging");
    private static readonly int IsLedgeHangingHash = Animator.StringToHash("IsLedgeHanging");

    private readonly Animator _animator;
    private readonly Vector3 _spriteRestLocalPosition;
    private bool _hasDiagnosticState;
    private AnimatorStateInfo _lastDiagnosticState;

    // 진단용 조회입니다. 실제 접지나 점프 가능 여부를 판정하는 데 사용하지 않습니다.
    public bool DisplayedGrounded => _animator != null && _animator.GetBool(IsGroundedHash);

    public PlayerAnimationPresenter(Animator animator)
    {
        _animator = animator;
        if (_animator != null)
            _spriteRestLocalPosition = _animator.transform.localPosition;
    }

    public void SetMoving(bool moving)
    {
        if (_animator != null)
            _animator.SetBool(IsMovingHash, moving);
    }

    public void SetCrouching(bool crouching)
    {
        if (_animator != null)
            _animator.SetBool(IsCrouchingHash, crouching);
    }

    public void SetWallClinging(bool clinging, bool ledgeHanging, bool hasBodyGeometry, float bodyRightEdge, float handLocalX)
    {
        if (_animator == null)
            return;

        _animator.SetBool(IsWallClingingHash, clinging);
        _animator.SetBool(IsLedgeHangingHash, clinging && ledgeHanging);
        Vector3 position = _spriteRestLocalPosition;
        if (clinging)
            _animator.SetBool(IsGroundedHash, false);
        if (clinging && !ledgeHanging && hasBodyGeometry)
        {
            // 몸과 카메라는 옮기지 않고 그림의 손끝만 벽면에 맞춥니다.
            position.x = bodyRightEdge - handLocalX * _animator.transform.localScale.x;
        }
        _animator.transform.localPosition = position;
    }

    public void ResetJump()
    {
        if (_animator == null)
            return;

        _animator.ResetTrigger(RunningJumpHash);
        _animator.ResetTrigger(IdleJumpHash);
        _animator.SetBool(IsIdleJumpHash, false);
        _animator.SetBool(IsGroundedHash, true);
        _animator.SetFloat(VerticalSpeedHash, 0f);
    }

    public void ClearIdleJump()
    {
        if (_animator != null)
            _animator.SetBool(IsIdleJumpHash, false);
    }

    public void SetGroundState(bool grounded, float verticalSpeed)
    {
        if (_animator == null)
            return;

        _animator.SetBool(IsGroundedHash, grounded);
        _animator.SetFloat(VerticalSpeedHash, verticalSpeed);
    }

    public void PlayJump(bool idleJump, bool moving, float verticalSpeed)
    {
        if (_animator == null)
            return;

        SetMoving(moving);
        _animator.SetBool(IsIdleJumpHash, idleJump);
        _animator.SetBool(IsGroundedHash, false);
        // 다음 물리 업데이트를 기다리지 않고 점프 직후 속도를 반영합니다.
        _animator.SetFloat(VerticalSpeedHash, verticalSpeed);
        _animator.ResetTrigger(RunningJumpHash);
        _animator.ResetTrigger(IdleJumpHash);
        _animator.SetTrigger(idleJump ? IdleJumpHash : RunningJumpHash);
    }

    public void ResetDiagnostics()
    {
        _hasDiagnosticState = false;
    }

    public bool TryGetStateDiagnostic(out string message)
    {
        message = null;
        if (_animator == null)
            return false;

        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
        if (!_hasDiagnosticState || state.fullPathHash != _lastDiagnosticState.fullPathHash)
        {
            string previous = _hasDiagnosticState
                ? $"{GetDiagnosticStateName(_lastDiagnosticState)} (직전 관측 진행률={_lastDiagnosticState.normalizedTime:F3})"
                : "기록 시작";
            message = $"[PlayerAnim][State] {previous} -> {GetDiagnosticStateName(state)} " +
                $"(진행률={state.normalizedTime:F3})";
        }
        // 전환 직전 진행률을 보존하되, 문자열은 상태가 바뀔 때만 생성합니다.
        _lastDiagnosticState = state;
        _hasDiagnosticState = true;
        return message != null;
    }

    private static string GetDiagnosticStateName(AnimatorStateInfo state)
    {
        if (state.IsName("Base Layer.RunningJumpStart")) return "RunningJumpStart";
        if (state.IsName("Base Layer.RunningJumpRise")) return "RunningJumpRise";
        if (state.IsName("Base Layer.RunningJumpDescent")) return "RunningJumpDescent";
        if (state.IsName("Base Layer.CrouchIdle")) return "CrouchIdle";
        if (state.IsName("Base Layer.CrouchWalk")) return "CrouchWalk";
        if (state.IsName("Base Layer.WallClingIdle")) return "WallClingIdle";
        if (state.IsName("Base Layer.LedgeHangIdle")) return "LedgeHangIdle";
        if (state.IsName("Base Layer.Fall")) return "Fall";
        if (state.IsName("Base Layer.Run")) return "Run";
        if (state.IsName("Base Layer.Idle")) return "Idle";
        if (state.IsName("Base Layer.IdleToRun")) return "IdleToRun";
        if (state.IsName("Base Layer.RunToIdle")) return "RunToIdle";
        if (state.IsName("Base Layer.IdleJumpStart")) return "IdleJumpStart";
        if (state.IsName("Base Layer.IdleJumpRise")) return "IdleJumpRise";
        if (state.IsName("Base Layer.IdleJumpDescent")) return "IdleJumpDescent";
        if (state.IsName("Base Layer.IdleJumpLand")) return "IdleJumpLand";
        return $"StateHash={state.fullPathHash}";
    }
}
