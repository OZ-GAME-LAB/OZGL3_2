using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public struct PlayerAttackState : INetworkSerializable, System.IEquatable<PlayerAttackState>
{
    public int Sequence;
    public int Step;
    public int Direction;
    public double StartTime;
    public double EndTime;
    public float AnimationDuration;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Sequence);
        serializer.SerializeValue(ref Step);
        serializer.SerializeValue(ref Direction);
        serializer.SerializeValue(ref StartTime);
        serializer.SerializeValue(ref EndTime);
        serializer.SerializeValue(ref AnimationDuration);
    }

    public bool Equals(PlayerAttackState other) => Sequence == other.Sequence && Step == other.Step &&
        Direction == other.Direction && StartTime == other.StartTime && EndTime == other.EndTime &&
        AnimationDuration == other.AnimationDuration;
}

public class PlayerRuneAttackController : NetworkBehaviour
{
    [System.Serializable]
    private sealed class AttackTiming
    {
        [Tooltip("기본 공격 간격에 곱할 모션 재생 시간 비율입니다. 실제 시간에는 룬 공격속도도 적용합니다.")]
        [SerializeField, Min(0.01f)] private float _durationMultiplier;
        [Tooltip("모션 진행률 기준 첫 타격 시점입니다. 0은 시작, 1은 끝입니다.")]
        [SerializeField, Range(0f, 1f)] private float _hitNormalizedTime;
        [Tooltip("입력이 예약되어 있으면 다음 타로 넘어갈 수 있는 진행률입니다. 남은 다단히트와 최소 후딜레이는 보장합니다.")]
        [SerializeField, Range(0f, 1f)] private float _comboNormalizedTime;

        public AttackTiming(float durationMultiplier, float hitNormalizedTime, float comboNormalizedTime)
        {
            _durationMultiplier = durationMultiplier;
            _hitNormalizedTime = hitNormalizedTime;
            _comboNormalizedTime = comboNormalizedTime;
        }

        public float DurationMultiplier => ValidTiming(_durationMultiplier, 1f, 0.01f);
        public float HitNormalizedTime => Mathf.Clamp01(ValidTiming(_hitNormalizedTime, 0.25f, 0f));
        public float ComboNormalizedTime => Mathf.Clamp01(ValidTiming(_comboNormalizedTime, 1f, 0f));

        private static float ValidTiming(float value, float fallback, float minimum)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Max(minimum, value);
        }
    }

    [Header("캐릭터 연결")]
    [SerializeField] private RuneCombatActor _actor;

    [Header("장착 룬 연결")]
    [SerializeField] private PlayerRuneEquipmentController _runeEquipmentController;

    [Header("룬 다단히트와 후딜레이")]
    [SerializeField, Min(0.01f)]
    private float _tripleHitInterval = 0.12f;

    [SerializeField, Min(0.01f)]
    [Tooltip("마지막 타격 뒤 최소 대기 시간입니다. 룬 공격속도 배율로 나눕니다.")]
    private float _recoverySeconds = 0.15f;

    [Header("기본 3타 연계")]
    [SerializeField, Min(0f)] private float _comboResetSeconds = 0.4f;
    [SerializeField] private AttackTiming _attack1Timing = new(0.8f, 1f / 6f, 0.65f);
    [SerializeField] private AttackTiming _attack2Timing = new(2f / 3f, 1f / 6f, 0.6f);
    [SerializeField] private AttackTiming _attack3Timing = new(1.2f, 4f / 15f, 0.9f);

    [Header("근접 공격")]
    [SerializeField] private LayerMask _targetLayer;    
    [SerializeField] private Vector2 _attackOffset = new Vector2(0.8f, 0.3f);
    [SerializeField, Min(0.01f)] private float _meleeRadius = 0.7f;

    [Header("투사체")]
    [SerializeField] private PlayerAttackProjectile _projectilePrefab;
    [SerializeField] private LayerMask _wallLayer;
    [SerializeField, Min(0.01f)] private float _projectileSpeed = 10f;
    [SerializeField, Min(0.01f)] private float _projectileDistance = 8f;

    private readonly HashSet<RuneCombatActor> _hitTargets = new();

    private PlayerNetworkStats _stats;
    private PlayerControllerView _view;
    private readonly NetworkVariable<PlayerAttackState> _attackState = new(default,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private bool _attackHeld;
    private bool _queuedAttack;
    private bool _requestedFaceRight;
    private bool _localInputActive;
    private int _comboStep;
    private double _lastAttackEndTime = double.NegativeInfinity;

    public PlayerAttackState AttackState => IsSpawned && isActiveAndEnabled ? _attackState.Value : default;
    public bool IsAttackActive => AttackState.Step != 0;
    public double AttackClock => IsSpawned ? NetworkManager.ServerTime.Time : 0;
    private AttackProfile _activeProfile;
    private float _attackPowerSnapshot;

    private int _remainingHits;
    private int _attackDirection;

    private double _nextHitTime;
    private double _attackEndTime;
    private double _comboAvailableTime;
    private double _nextAttackTime;

    private float _activeHitInterval;

    public bool IsAttackingOnServer => _activeProfile != null;
    private PlayerInteractionController _interaction;

    private void Awake()
    {
        _stats = GetComponent<PlayerNetworkStats>();
        _view = GetComponent<PlayerControllerView>();
        _interaction = GetComponent<PlayerInteractionController>();

        if (_actor == null)
        {
            _actor = GetComponent<RuneCombatActor>();
        }

        if (_runeEquipmentController == null)
        {
            _runeEquipmentController = GetComponent<PlayerRuneEquipmentController>();
        }
    }

    public override void OnNetworkSpawn()
    {
        _activeProfile = null;
        _remainingHits = 0;
        _nextAttackTime = 0;
        _comboStep = 0;
        _lastAttackEndTime = double.NegativeInfinity;
        _attackHeld = false;
        _queuedAttack = false;
        _localInputActive = false;
        if (IsServer) _attackState.Value = default;
    }

    public override void OnNetworkDespawn()
    {
        ClearActiveAttack();
    }

    private void OnDisable()
    {
        ClearActiveAttack();
        _view?.UpdateAttackPresentation();
    }

    // 로컬 플레이어 입력에서 호출.
    public void RequestAttack(bool faceRight)
    {
        Debug.Log($"[공격 요청] 대상: {name}, " + $"Spawned: {IsSpawned}, " + $"Owner: {IsOwner}, " + $"Active: {isActiveAndEnabled}", this);

        if (IsSpawned == false || IsOwner == false || isActiveAndEnabled == false)
        {
            Debug.LogWarning("[공격 중단] 스폰·소유권·활성 상태를 확인하세요.", this);

            return;
        }

        _localInputActive = true;
        SetAttackInputServerRpc(true, true, false, faceRight);
    }

    public void ReleaseAttack()
    {
        if (!IsSpawned || !IsOwner || !_localInputActive) return;
        SetAttackInputServerRpc(false, false, false, true);
    }

    public void CancelAttackInput()
    {
        if (!IsSpawned || !IsOwner || !_localInputActive) return;
        _localInputActive = false;
        SetAttackInputServerRpc(false, false, true, true);
    }

    [ServerRpc]
    private void SetAttackInputServerRpc(bool held, bool pressed, bool cancel, bool faceRight,
        ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        if (_interaction != null && _interaction.HasServerRequest)
        {
            CancelPendingInputOnServer();
            return;
        }
        _attackHeld = held && !cancel;
        if (cancel) _queuedAttack = false;
        if (!pressed) return;
        _requestedFaceRight = faceRight;
        if (_activeProfile != null || NetworkManager.ServerTime.Time < _nextAttackTime)
            _queuedAttack = true;
        else
            TryStartAttack(faceRight);
    }

    private void TryStartAttack(bool faceRight)
    {
        if (_interaction != null && _interaction.HasServerRequest)
        {
            CancelPendingInputOnServer();
            return;
        }
        if (isActiveAndEnabled == false || IsSpawned == false)
        {
            Debug.LogWarning("[공격 중단] 공격 컴포넌트가 비활성입니다.", this);
            return;
        }

        if (_actor == null)
        {
            Debug.LogError("[공격 중단] Actor가 없습니다. " + "같은 플레이어의 PlayerRuneCombatActor를 연결하세요.", this);
            return;
        }

        Debug.Log($"[서버 Actor] 종류: {_actor.GetType().Name}, " + $"Spawned: {_actor.IsSpawned}, " + $"Alive: {_actor.IsAlive}, " + $"CanAttack: {_actor.CanAttack}, " + $"공격력: {_actor.AttackPower}", this);

        if (_actor.IsSpawned == false || _actor.IsAlive == false || _actor.CanAttack == false
            || (_actor is PlayerRuneCombatActor playerActor && playerActor.IsHitControlLocked))
        {
            Debug.LogWarning("[공격 중단] Actor가 공격 가능한 상태가 아닙니다.", this);
            return;
        }

        double now = NetworkManager.ServerTime.Time;

        if (_view == null || !_view.CanStartGroundAttack)
        {
            _attackHeld = false;
            _queuedAttack = false;
            return;
        }

        if (_activeProfile != null || now < _nextAttackTime)
        {
            Debug.Log($"[공격 대기] 진행 중: {_activeProfile != null}, " + $"남은 대기시간: {System.Math.Max(0, _nextAttackTime - now):F2}", this);
            return;
        }

        if (TryGetProfile(out AttackProfile profile) == false)
        {
            Debug.LogError("[공격 중단] 룬 조합 계산에 실패했습니다.", this);
            return;
        }

        Debug.Log($"[실제 공격 설정] 투사체: {profile._useProjectile}, 타격 수: {profile._hitCount}", this);

        if (profile._useProjectile && _projectilePrefab == null)
        {
            Debug.LogError("투사체 프리팹을 연결해 주세요.", this);
            return;
        }

        if (_stats == null || !_stats.IsSpawned)
        {
            Debug.LogError("공격 간격을 읽을 PlayerNetworkStats를 확인해 주세요.", this);
            return;
        }

        float attackPower = _actor.AttackPower;

        if (attackPower < 0f || float.IsNaN(attackPower) || float.IsInfinity(attackPower))
        {
            Debug.LogError("플레이어 공격력이 올바르지 않습니다.", this);
            return;
        }

        // 공격 시작 시 값을 보관.
        // 도중에 장비를 바꿔도 진행 중인 공격은 바뀌지 않음.

        _activeProfile = profile;
        _attackPowerSnapshot = attackPower;
        _remainingHits = profile._hitCount;
        _attackDirection = faceRight ? 1 : -1;

        int nextStep = now > _lastAttackEndTime + _comboResetSeconds ? 1 : _comboStep % 3 + 1;
        AttackTiming timing = GetAttackTiming(nextStep);
        float speed = profile._attackSpeedMultiple;
        float animationDuration = Mathf.Max(0.01f, _stats.BaseAttackInterval * timing.DurationMultiplier / speed);
        float windup = animationDuration * timing.HitNormalizedTime;

        _activeHitInterval = _tripleHitInterval / speed;
        _nextHitTime = now + windup;

        double sequenceDuration = windup + (profile._hitCount - 1) * _activeHitInterval + _recoverySeconds / speed;

        // 다단히트가 늘어나도 모션 재생 시간 자체는 늘리지 않습니다.
        _attackEndTime = now + System.Math.Max(animationDuration, sequenceDuration);
        _comboAvailableTime = now + System.Math.Max(animationDuration * timing.ComboNormalizedTime, sequenceDuration);
        _nextAttackTime = _attackEndTime;
        _comboStep = nextStep;
        _attackState.Value = new PlayerAttackState
        {
            Sequence = _attackState.Value.Sequence + 1,
            Step = _comboStep,
            Direction = _attackDirection,
            StartTime = now,
            EndTime = _attackEndTime,
            AnimationDuration = animationDuration
        };
    }

    private AttackTiming GetAttackTiming(int step)
    {
        if (step == 1) return _attack1Timing;
        if (step == 2) return _attack2Timing;
        return _attack3Timing;
    }

    private void Update()
    {
        // 원격 플레이어의 입력 View는 비활성화되어도 공격 표시는 갱신합니다.
        _view?.UpdateAttackPresentation();
        if (IsSpawned == false || IsServer == false)
        {
            return;
        }

        if (_actor == null || _actor.IsSpawned == false || _actor.IsAlive == false || _actor.CanAttack == false)
        {
            ClearActiveAttack();
            return;
        }

        double now = NetworkManager.ServerTime.Time;

        if (_activeProfile == null)
        {
            if ((_attackHeld || _queuedAttack) && now >= _nextAttackTime)
            {
                bool held = _attackHeld;
                _queuedAttack = false;
                _attackHeld = false; // 시작 실패 시 매 프레임 재시도하지 않습니다.
                TryStartAttack(_requestedFaceRight);
                if (_activeProfile != null) _attackHeld = held;
            }
            return;
        }

        while (_activeProfile != null && _remainingHits > 0 && now >= _nextHitTime)
        {
            AttackProfile profile = _activeProfile;

            _remainingHits--;
            _nextHitTime += _activeHitInterval;

            if (profile._useProjectile)
            {
                FireProjectile(profile);
            }
            else
            {
                PerformMeleeHit(profile);
            }
        }

        if (_activeProfile != null && _remainingHits == 0 &&
            (_attackHeld || _queuedAttack) && now >= _comboAvailableTime)
        {
            if (TryContinueAttack(now)) return;
        }

        if (_activeProfile != null && _remainingHits == 0 && now >= _attackEndTime)
        {
            _lastAttackEndTime = _attackEndTime;
            _activeProfile = null;
            _queuedAttack = false;
            _attackHeld = false;
            PublishAttackStopped();
        }
    }

    private bool TryContinueAttack(double now)
    {
        AttackProfile previousProfile = _activeProfile;
        double previousLastEnd = _lastAttackEndTime;
        double previousNextTime = _nextAttackTime;
        _activeProfile = null;
        _queuedAttack = false;
        _lastAttackEndTime = now;
        _nextAttackTime = now;
        TryStartAttack(_attackDirection > 0);
        if (_activeProfile != null) return true;

        // 발판 이탈 등으로 다음 공격이 거절되면 현재 모션을 끝까지 마무리합니다.
        _activeProfile = previousProfile;
        _lastAttackEndTime = previousLastEnd;
        _nextAttackTime = previousNextTime;
        _attackHeld = false;
        return false;
    }

    private bool TryGetProfile(out AttackProfile profile)
    {
        profile = null;

        if (_runeEquipmentController == null)
        {
            Debug.LogError("PlayerRuneEquipmentController가 없습니다.", this);

            return false;
        }

        if (_runeEquipmentController.TryGetAttackProfile(out profile, out string error) == false)
        {
            Debug.LogWarning(error, this);
            return false;
        }

        return true;
    }

    private Vector2 GetAttackPosition()
    {
        Vector2 position = transform.position;

        position.x += _attackOffset.x * _attackDirection;
        position.y += _attackOffset.y;

        return position;
    }

    private void PerformMeleeHit(AttackProfile profile)
    {
        Vector2 center = GetAttackPosition();
        float radius = _meleeRadius * profile._rangeMultiple;

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius, _targetLayer);

        Debug.Log($"[근접 판정] 위치: {center}, 반경: {radius}, " + $"감지된 콜라이더: {hits.Length}개", this);

        _hitTargets.Clear();

        foreach (Collider2D hit in hits)
        {
            if (hit == null)
            {
                continue;
            }

            RuneCombatActor target = hit.GetComponentInParent<RuneCombatActor>();

            if (target == null || _hitTargets.Add(target) == false)
            {
                continue;
            }

            bool accepted = RuneHitResolver.TryHitOnServer(_actor, target, profile, _attackPowerSnapshot);

            Debug.Log($"[근접 적중 결과] 대상: {target.name}, " + $"생존 여부(처리 후): {target.IsAlive}, " + $"적중 승인: {accepted}", this);
        }
    }

    private void FireProjectile(AttackProfile profile)
    {
        Debug.Log($"[투사체 발사] 플레이어: {name}, 위치: {GetAttackPosition()}", this);

        PlayerAttackProjectile projectile = Instantiate(_projectilePrefab, GetAttackPosition(), Quaternion.identity);

        projectile.InitOnServer(
            _actor,
            profile,
            _attackPowerSnapshot,
            Vector2.right * _attackDirection,
            _projectileSpeed,
            _projectileDistance * profile._rangeMultiple,
            _targetLayer,
            _wallLayer);

        projectile.NetworkObject.Spawn();
    }

    // 보통의 피격 경직에서 호출.
    // 사망·기절 등 강제 행동 불가는 CanAttack/IsAlive로 별도 처리.
    public bool TryInterruptOnServer()
    {
        if (IsServer == false || _activeProfile == null)
        {
            return false;
        }

        if (_activeProfile._preventAttackInterrupt)
        {
            return false;
        }

        ClearActiveAttack();

        // 중단되어도 다음 공격 가능 시각은 유지.
        return true;
    }

    private void ClearActiveAttack()
    {
        _activeProfile = null;
        _remainingHits = 0;
        _attackHeld = false;
        _queuedAttack = false;
        _comboStep = 0;
        _lastAttackEndTime = double.NegativeInfinity;
        if (IsSpawned && IsServer) PublishAttackStopped();
    }

    public void CancelPendingInputOnServer()
    {
        if (!IsServer) return;
        _attackHeld = false;
        _queuedAttack = false;
    }

    private void PublishAttackStopped()
    {
        PlayerAttackState state = _attackState.Value;
        state.Step = 0;
        _attackState.Value = state;
    }
}
