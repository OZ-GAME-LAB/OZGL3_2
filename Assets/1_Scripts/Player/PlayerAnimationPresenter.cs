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
    private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");
    private static readonly int AttackPlaybackRateHash = Animator.StringToHash("AttackPlaybackRate");
    private static readonly int Attack1Hash = Animator.StringToHash("Base Layer.Attack1");
    private static readonly int Attack2Hash = Animator.StringToHash("Base Layer.Attack2");
    private static readonly int Attack3Hash = Animator.StringToHash("Base Layer.Attack3");
    private static readonly int IdleStateHash = Animator.StringToHash("Base Layer.Idle");
    private static readonly int FallStateHash = Animator.StringToHash("Base Layer.Fall");
    private bool _playingAttack;
    private int _attackSequence;
    private static readonly int IsInteractingHash = Animator.StringToHash("IsInteracting");
    private static readonly int InteractionPlaybackRateHash = Animator.StringToHash("InteractionPlaybackRate");
    private static readonly int LoggingHash = Animator.StringToHash("Base Layer.Logging");
    private static readonly int MiningHash = Animator.StringToHash("Base Layer.Mining");
    private static readonly int FishingCastHash = Animator.StringToHash("Base Layer.FishingCast");
    private static readonly int FishingIdleHash = Animator.StringToHash("Base Layer.FishingIdle");
    private bool _playingResource;
    public bool IsPlayingResource => _playingResource;
    private PlayerResourceAnimationState _displayedResource;
    private float _loggingLength = 1f, _miningLength = 1f, _castLength = 1f, _fishingIdleLength = 1f;
    private bool _remoteInitialized;
    private bool _remoteWasValid;
    private bool _remoteWasActing;
    private uint _remoteLifeVersion;
    private uint _remoteJumpSequence;
    private static readonly int RunStateHash = Animator.StringToHash("Base Layer.Run");
    private static readonly int CrouchIdleStateHash = Animator.StringToHash("Base Layer.CrouchIdle");
    private static readonly int CrouchWalkStateHash = Animator.StringToHash("Base Layer.CrouchWalk");
    private static readonly int WallClingStateHash = Animator.StringToHash("Base Layer.WallClingIdle");
    private static readonly int LedgeHangStateHash = Animator.StringToHash("Base Layer.LedgeHangIdle");
    private static readonly int IdleJumpRiseStateHash = Animator.StringToHash("Base Layer.IdleJumpRise");
    private static readonly int IdleJumpDescentStateHash = Animator.StringToHash("Base Layer.IdleJumpDescent");
    private static readonly int RunningJumpRiseStateHash = Animator.StringToHash("Base Layer.RunningJumpRise");
    private static readonly int RunningJumpDescentStateHash = Animator.StringToHash("Base Layer.RunningJumpDescent");

    public void ResetRemoteMovement()
    {
        _remoteInitialized = false;
        _remoteWasValid = false;
        _remoteWasActing = false;
    }

    // 원격 표현은 한 번의 호출에서 채집 > 공격 > 이동 순으로 결정합니다.
    public void UpdateRemoteMovement(PlayerMovementAnimationState state, bool valid,
        PlayerAttackState attack, PlayerResourceAnimationState resource, double now,
        bool hasBodyGeometry, float bodyRightEdge, float handLocalX)
    {
        if (_animator == null) return;
        bool firstSample = !_remoteInitialized;
        bool restorePose = firstSample || !_remoteWasValid || _remoteLifeVersion != state.LifeVersion;
        bool newJump = _remoteInitialized && _remoteJumpSequence != state.JumpSequence;
        // 행동 중이거나 이미 착지한 점프도 소비합니다. 나중에 다시 재생하지 않습니다.
        _remoteJumpSequence = state.JumpSequence;
        _remoteLifeVersion = state.LifeVersion;
        _remoteInitialized = true;
        if (!valid)
        {
            UpdateResource(default, now, true);
            UpdateAttack(default, now, true);
            SetMoving(false);
            SetCrouching(false);
            SetWallClinging(false, false, hasBodyGeometry, bodyRightEdge, handLocalX);
            ResetJump();
            if (_remoteWasValid || firstSample) _animator.Play(IdleStateHash, 0, 0f);
            _remoteWasValid = false;
            _remoteWasActing = false;
            return;
        }

        _remoteWasValid = true;
        bool acting = resource.PlaysAnimation || attack.Step != 0;
        UpdateResource(resource, now, state.Grounded);
        UpdateAttack(attack, now, state.Grounded);
        if (acting)
        {
            SetMoving(false);
            SetCrouching(false);
            SetWallClinging(false, false, hasBodyGeometry, bodyRightEdge, handLocalX);
            _animator.ResetTrigger(RunningJumpHash);
            _animator.ResetTrigger(IdleJumpHash);
        }
        else
        {
            SetMoving(state.Moving);
            SetCrouching(state.Crouching);
            SetGroundState(state.Grounded, state.VerticalSpeed);
            _animator.SetBool(IsIdleJumpHash, state.IdleJump);
            SetWallClinging(state.WallClinging, state.LedgeHanging, hasBodyGeometry, bodyRightEdge, handLocalX);
            if (restorePose || _remoteWasActing)
            {
                _animator.ResetTrigger(RunningJumpHash);
                _animator.ResetTrigger(IdleJumpHash);
                _animator.Play(GetRemotePose(state), 0, 0f);
            }
            else if (newJump && state.Jumping && !state.Grounded && !state.WallClinging && state.VerticalSpeed >= -0.1f)
            {
                PlayJump(state.IdleJump, state.Moving, state.VerticalSpeed);
            }
            else if (state.Grounded || state.WallClinging)
            {
                _animator.ResetTrigger(RunningJumpHash);
                _animator.ResetTrigger(IdleJumpHash);
            }
        }
        _remoteWasActing = acting;
    }

    private static int GetRemotePose(PlayerMovementAnimationState state)
    {
        if (state.LedgeHanging) return LedgeHangStateHash;
        if (state.WallClinging) return WallClingStateHash;
        if (state.Crouching) return state.Moving ? CrouchWalkStateHash : CrouchIdleStateHash;
        if (state.Grounded) return state.Moving ? RunStateHash : IdleStateHash;
        if (!state.Jumping) return FallStateHash;
        if (state.VerticalSpeed < -0.1f)
            return state.IdleJump ? IdleJumpDescentStateHash : RunningJumpDescentStateHash;
        return state.IdleJump ? IdleJumpRiseStateHash : RunningJumpRiseStateHash;
    }

    public void UpdateResource(PlayerResourceAnimationState state, double now, bool grounded)
    {
        if (_animator == null) return;
        bool active = state.PlaysAnimation;
        if (!active)
        {
            if (!_playingResource) return;
            _playingResource = false;
            _animator.SetBool(IsInteractingHash, false);
            if (!_playingAttack) _animator.Play(grounded ? IdleStateHash : FallStateHash, 0, 0f);
            return;
        }
        if (_playingResource && _displayedResource.Equals(state)) return;
        _playingResource = true;
        _displayedResource = state;
        _playingAttack = false;
        _animator.SetBool(IsAttackingHash, false);
        _animator.SetBool(IsInteractingHash, true);
        _animator.SetBool(IsMovingHash, false);
        _animator.ResetTrigger(RunningJumpHash);
        _animator.ResetTrigger(IdleJumpHash);
        bool casting = state.Phase == ResourceAnimationPhase.Casting;
        bool loggingSwing = state.Kind == ResourceAnimationKind.Logging && state.Phase == ResourceAnimationPhase.Swing;
        int hash = state.Kind == ResourceAnimationKind.Logging ? LoggingHash :
            state.Kind == ResourceAnimationKind.Mining ? MiningHash : casting ? FishingCastHash : FishingIdleHash;
        float duration = loggingSwing ? state.MotionDuration : state.Kind == ResourceAnimationKind.Logging ? _loggingLength :
            state.Kind == ResourceAnimationKind.Mining ? _miningLength : casting ? state.CastDuration : _fishingIdleLength;
        duration = Mathf.Max(0.01f, duration);
        _animator.SetFloat(InteractionPlaybackRateHash, loggingSwing ? _loggingLength / duration : casting ? _castLength / duration : 1f);
        float normalized = Mathf.Max(0f, (float)((now - state.StartTime) / duration));
        _animator.Play(hash, 0, casting || loggingSwing ? Mathf.Min(normalized, 0.9999f) : normalized % 1f);
    }

    public void UpdateAttack(PlayerAttackState attack, double now, bool grounded)
    {
        if (_animator == null || _playingResource) return;
        if (attack.Step == 0)
        {
            if (!_playingAttack) return;
            _playingAttack = false;
            _animator.SetBool(IsAttackingHash, false);
            _animator.Play(grounded ? IdleStateHash : FallStateHash, 0, 0f);
            return;
        }
        if (_playingAttack && _attackSequence == attack.Sequence) return;
        _playingAttack = true;
        _attackSequence = attack.Sequence;
        float duration = Mathf.Max(0.01f, attack.AnimationDuration);
        _animator.SetBool(IsAttackingHash, true);
        _animator.SetBool(IsMovingHash, false);
        _animator.ResetTrigger(RunningJumpHash);
        _animator.ResetTrigger(IdleJumpHash);
        _animator.SetFloat(AttackPlaybackRateHash, 1f / duration);
        int stateHash = attack.Step == 1 ? Attack1Hash : attack.Step == 2 ? Attack2Hash : Attack3Hash;
        // 다단히트 후딜레이와 별개로, 타별 모션 재생 시간에 맞춥니다.
        _animator.Play(stateHash, 0, Mathf.Clamp01((float)((now - attack.StartTime) / duration)));
    }

    // 진단용 조회입니다. 실제 접지나 점프 가능 여부를 판정하는 데 사용하지 않습니다.
    public bool DisplayedGrounded => _animator != null && _animator.GetBool(IsGroundedHash);

    public PlayerAnimationPresenter(Animator animator)
    {
        _animator = animator;
        if (_animator != null)
        {
            _spriteRestLocalPosition = _animator.transform.localPosition;
            if (_animator.runtimeAnimatorController != null)
                foreach (AnimationClip clip in _animator.runtimeAnimatorController.animationClips)
                {
                    switch (clip.name)
                    {
                        case "Logging": _loggingLength = clip.length; break;
                        case "Mining": _miningLength = clip.length; break;
                        case "FishingCast": _castLength = clip.length; break;
                        case "FishingIdle": _fishingIdleLength = clip.length; break;
                    }
                }
        }
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
