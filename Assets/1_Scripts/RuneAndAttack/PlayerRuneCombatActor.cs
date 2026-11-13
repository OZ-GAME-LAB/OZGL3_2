using UnityEngine;

using System.Collections.Generic;
using Unity.Netcode;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerNetworkStats))]
public class PlayerRuneCombatActor : RuneCombatActor
{
    [SerializeField, Min(0f)] private float _hitInvulnerabilityDuration = 0.6f;
    [SerializeField, Min(0f)] private float _hitControlLockDuration = 0.1f;
    [SerializeField, Min(0f)] private float _knockbackImpulse = 3f;
    private readonly NetworkVariable<bool> _isHitInvulnerable = new(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private float _hitInvulnerabilityRemaining;
    private float _hitControlLockRemaining;
    private readonly HashSet<RuneCombatActor> _attackersThisPhysicsStep = new();
    private double _attackPhysicsStep = double.NaN;

    private PlayerNetworkStats _stats;
    private PlayerRuneAttackController _attackController;
    private PlayerControllerView _playerController;
    private PlayerInteractionController _interactionController;

    protected override NetworkUnit Unit => _stats;

    private void Awake()
    {
        _stats = GetComponent<PlayerNetworkStats>();
        _attackController = GetComponent<PlayerRuneAttackController>();
        _playerController = GetComponent<PlayerControllerView>();
        _interactionController = GetComponent<PlayerInteractionController>();
    }

    public override bool IsAlive => IsSpawned && _stats != null && _stats.IsAlive;
    public override bool CanAttack => IsAlive && isActiveAndEnabled;
    public override float AttackPower => _stats != null ? _stats.AttackPower : 0f;
    public bool IsHitInvulnerable => _isHitInvulnerable.Value;
    public bool IsHitControlLocked => _hitControlLockRemaining > 0f
        || (IsOwner && _playerController != null && _playerController.IsHitControlLocked);

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        _hitInvulnerabilityRemaining = 0f;
        _hitControlLockRemaining = 0f;
        _attackPhysicsStep = double.NaN;
        _attackersThisPhysicsStep.Clear();

        if (IsServer)
            _isHitInvulnerable.Value = false;

        _isHitInvulnerable.OnValueChanged += OnHitInvulnerabilityChanged;
        OnHitInvulnerabilityChanged(false, IsHitInvulnerable);
    }

    public override void OnNetworkDespawn()
    {
        _isHitInvulnerable.OnValueChanged -= OnHitInvulnerabilityChanged;
        _hitInvulnerabilityRemaining = 0f;
        _hitControlLockRemaining = 0f;
        if (IsOwner && _playerController != null)
            _playerController.EndHitControlLock();
        _attackPhysicsStep = double.NaN;
        _attackersThisPhysicsStep.Clear();
        OnHitInvulnerabilityChanged(IsHitInvulnerable, false);
        base.OnNetworkDespawn();
    }

    private void FixedUpdate()
    {
        if (!IsServer || !IsSpawned)
            return;

        _hitControlLockRemaining = Mathf.Max(0f, _hitControlLockRemaining - Time.fixedDeltaTime);

        if (!IsHitInvulnerable)
            return;

        _hitInvulnerabilityRemaining = Mathf.Max(0f,
            _hitInvulnerabilityRemaining - Time.fixedDeltaTime);

        if (_hitInvulnerabilityRemaining <= 0f)
            _isHitInvulnerable.Value = false;
    }

    private void OnHitInvulnerabilityChanged(bool previousValue, bool currentValue)
    {
        //TODO : currentValue가 true이면 피격 무적 표시를 켜고, false이면 끄기. 서버와 각 클라이언트에서 호출됨.
    }

    public override bool CanHit(RuneCombatActor target)
    {
        return IsAlive && target != null && target != this && target.IsAlive
            && target is MonsterRuneCombatActor;
    }

    // TO USE : 서버에서 플레이어에게 직접 타격을 적용할 때 사용. 실제 HP가 감소하면 공격 중단도 요청.
    // 독의 주기적 피해는 NetworkUnit이 처리하므로 이 함수를 호출하지 않음.
    public override RuneDamageResult ReceiveDirectDamageOnServer(RuneDamageInfo damageInfo)
    {
        if (!IsServer || !IsAlive || float.IsNaN(damageInfo.RawDamage)
            || float.IsInfinity(damageInfo.RawDamage) || damageInfo.RawDamage <= 0f)
            return default;

        RefreshAttackPhysicsStep();
        if (damageInfo.Attacker != null)
            _attackersThisPhysicsStep.Add(damageInfo.Attacker);

        return ApplyDirectHitOnServer(damageInfo);
    }

    public RuneDamageResult ReceiveContactDamageOnServer(MonsterRuneCombatActor attacker, Vector2 hitDirection = default)
    {
        if (!IsServer || !IsAlive || attacker == null || !attacker.IsServer || !attacker.CanHit(this))
            return default;

        RefreshAttackPhysicsStep();
        if (_attackersThisPhysicsStep.Contains(attacker))
            return default;

        return ApplyDirectHitOnServer(new RuneDamageInfo
        {
            Attacker = attacker,
            RawDamage = attacker.AttackPower * 0.5f,
            HitDirection = hitDirection,
            Element = DamageType.Physical
        });
    }

    private void RefreshAttackPhysicsStep()
    {
        if (_attackPhysicsStep == Time.fixedTimeAsDouble)
            return;

        _attackPhysicsStep = Time.fixedTimeAsDouble;
        _attackersThisPhysicsStep.Clear();
    }

    private RuneDamageResult ApplyDirectHitOnServer(RuneDamageInfo damageInfo)
    {
        if (IsHitInvulnerable)
            return default;

        RuneDamageResult result = base.ReceiveDirectDamageOnServer(damageInfo);
        if (result.Accepted && result.ActualDamage > 0f)
        {
            if (IsSpawned && IsAlive)
            {
                _hitInvulnerabilityRemaining = float.IsNaN(_hitInvulnerabilityDuration)
                    || float.IsInfinity(_hitInvulnerabilityDuration)
                    ? 0.6f : Mathf.Max(0f, _hitInvulnerabilityDuration);
                _isHitInvulnerable.Value = _hitInvulnerabilityRemaining > 0f;
                _hitControlLockRemaining = float.IsNaN(_hitControlLockDuration)
                    || float.IsInfinity(_hitControlLockDuration)
                    ? 0.1f : Mathf.Max(0f, _hitControlLockDuration);

                _interactionController?.CancelInteractionOnServer();

                float impulse = float.IsNaN(_knockbackImpulse) || float.IsInfinity(_knockbackImpulse)
                    ? 3f : Mathf.Max(0f, _knockbackImpulse);
                ClientRpcParams rpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
                };
                ApplyHitReactionClientRpc(GetKnockbackDirection(damageInfo), impulse,
                    _hitControlLockRemaining, rpcParams);
            }

            if (_attackController != null)
                _attackController.TryInterruptOnServer();
        }

        return result;
    }

    private Vector2 GetKnockbackDirection(RuneDamageInfo damageInfo)
    {
        float directionX = damageInfo.HitDirection.x;
        if (float.IsNaN(directionX) || float.IsInfinity(directionX) || Mathf.Abs(directionX) < 0.001f)
        {
            directionX = damageInfo.Attacker != null
                ? transform.position.x - damageInfo.Attacker.transform.position.x
                : -transform.lossyScale.x;
        }

        return new Vector2(directionX < 0f ? -1f : 1f, 1f).normalized;
    }

    [ClientRpc]
    private void ApplyHitReactionClientRpc(Vector2 direction, float impulse, float controlLockDuration,
        ClientRpcParams rpcParams = default)
    {
        if (!IsOwner || !IsSpawned || !IsAlive || _playerController == null)
            return;

        _playerController.ApplyHitReaction(direction, impulse, controlLockDuration);
    }
}
