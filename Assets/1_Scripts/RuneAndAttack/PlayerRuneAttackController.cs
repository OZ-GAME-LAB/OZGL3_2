using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerRuneAttackController : NetworkBehaviour
{
    [Header("캐릭터 연결")]
    [SerializeField] private RuneCombatActor _actor;

    [Header("임시 장착 룬 - 서버에서 사용")]
    [SerializeField] private string[] _testRuneIds;
    [SerializeField] private PlayerRuneEquipmentController _runeEquipmentController;

    [Header("기본 공격")]
    [SerializeField, Min(0.01f)] private float _baseAttackInterval = 0.6f;
    [SerializeField, Min(0f)] private float _windupSeconds = 0.1f;
    [SerializeField, Min(0.01f)] private float _tripleHitInterval = 0.12f;
    [SerializeField, Min(0.01f)] private float _recoverySeconds = 0.15f;

    [Header("근접 공격")]
    [SerializeField] private LayerMask _targetLayer;    
    [SerializeField] private Vector2 _attackOffset = new Vector2(0.8f, 0.3f);
    [SerializeField, Min(0.01f)] private float _meleeRadius = 0.7f;

    [Header("투사체")]
    [SerializeField] private PlayerAttackProjectile _projectilePrefab;
    [SerializeField] private LayerMask _wallLayer;
    [SerializeField, Min(0.01f)] private float _projectileSpeed = 10f;
    [SerializeField, Min(0.01f)] private float _projectileDistance = 8f;
    [SerializeField, Min(0.01f)] private float _projectileRadius = 0.15f;

    private readonly HashSet<RuneCombatActor> _hitTargets = new();

    private AttackProfile _activeProfile;
    private float _attackPowerSnapshot;

    private int _remainingHits;
    private int _attackDirection;

    private double _nextHitTime;
    private double _attackEndTime;
    private double _nextAttackTime;

    private float _activeHitInterval;

    public bool IsAttackingOnServer => _activeProfile != null;

    private void Awake()
    {
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
    }

    public override void OnNetworkDespawn()
    {
        ClearActiveAttack();
    }

    private void OnDisable()
    {
        ClearActiveAttack();
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

        RequestAttackServerRpc(faceRight);
    }

    [ServerRpc]
    private void RequestAttackServerRpc(bool faceRight, ServerRpcParams rpcParams = default)
    {
        Debug.Log($"[서버 공격 수신] {name}", this);

        if (rpcParams.Receive.SenderClientId != OwnerClientId)
        {
            Debug.LogWarning("[공격 중단] 요청자와 소유자가 다릅니다.", this);
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

        Debug.Log($"[실제 공격 설정] " + $"룬: {(_testRuneIds == null ? "(없음)" : string.Join(", ", _testRuneIds))}, " + $"투사체: {profile._useProjectile}, " + $"타격 수: {profile._hitCount}", this);

        if (profile._useProjectile && _projectilePrefab == null)
        {
            Debug.LogError("투사체 프리팹을 연결해 주세요.", this);
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

        float speed = profile._attackSpeedMultiple;
        float windup = _windupSeconds / speed;

        _activeHitInterval = _tripleHitInterval / speed;
        _nextHitTime = now + windup;

        double sequenceDuration = windup + (profile._hitCount - 1) * _activeHitInterval + _recoverySeconds / speed;

        double attackCycle = System.Math.Max(_baseAttackInterval / speed, sequenceDuration);

        _attackEndTime = now + attackCycle;
        _nextAttackTime = _attackEndTime;
    }

    private void Update()
    {
        if (IsSpawned == false || IsServer == false || _activeProfile == null)
        {
            return;
        }

        if (_actor == null || _actor.IsSpawned == false || _actor.IsAlive == false || _actor.CanAttack == false)
        {
            ClearActiveAttack();
            return;
        }

        double now = NetworkManager.ServerTime.Time;

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

        if (_activeProfile != null && _remainingHits == 0 && now >= _attackEndTime)
        {
            ClearActiveAttack();
        }
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
            _projectileRadius * profile._projectileSizeMultiple,
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
    }
}
