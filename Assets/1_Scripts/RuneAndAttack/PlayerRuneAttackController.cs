using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerRuneAttackController : NetworkBehaviour
{
    [Header("캐릭터 연결")]
    [SerializeField] private RuneCombatActor _actor;

    [Header("임시 장착 룬 - 서버에서 사용")]
    [SerializeField] private string[] _testRuneIds;

    [Header("기본 공격")]
    [SerializeField, Min(0.01f)]
    private float _baseAttackInterval = 0.6f;

    [SerializeField, Min(0f)]
    private float _windupSeconds = 0.1f;

    [SerializeField, Min(0.01f)]
    private float _tripleHitInterval = 0.12f;

    [SerializeField, Min(0.01f)]
    private float _recoverySeconds = 0.15f;

    [Header("근접 공격")]
    [SerializeField] 
    private LayerMask _targetLayer;

    [SerializeField]
    private Vector2 _attackOffset = new Vector2(0.8f, 0.3f);

    [SerializeField, Min(0.01f)]
    private float _meleeRadius = 0.7f;

    [Header("투사체")]
    [SerializeField] private PlayerAttackProjectile _projectilePrefab;
    [SerializeField] private LayerMask _wallLayer;

    [SerializeField, Min(0.01f)]
    private float _projectileSpeed = 10f;

    [SerializeField, Min(0.01f)]
    private float _projectileDistance = 8f;

    [SerializeField, Min(0.01f)]
    private float _projectileRadius = 0.15f;

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
        if (!IsSpawned || !IsOwner || !isActiveAndEnabled)
        {
            return;
        }

        RequestAttackServerRpc(faceRight);
    }

    [ServerRpc]
    private void RequestAttackServerRpc(bool faceRight, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
        {
            return;
        }

        if (isActiveAndEnabled == false || _actor == null || _actor.IsSpawned == false || _actor.IsAlive == false || _actor.CanAttack == false)
        {
            return;
        }

        double now = NetworkManager.ServerTime.Time;

        if (_activeProfile != null || now < _nextAttackTime)
        {
            return;
        }

        if (!TryBuildProfile(out AttackProfile profile))
        {
            return;
        }

        if (profile._useProjectile && _projectilePrefab == null)
        {
            Debug.LogError("투사체 프리팹을 연결해 주세요.", this);
            return;
        }

        float attackPower = _actor.AttackPower;

        if (attackPower < 0f || float.IsNaN(attackPower) || float.IsInfinity(attackPower))
        {
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

    private bool TryBuildProfile(out AttackProfile profile)
    {
        profile = null;

        if (DataManager.Inst == null)
        {
            return false;
        }

        int count = _testRuneIds == null ? 0 : _testRuneIds.Length;
        var slots = new RuneData[count];

        for (int i = 0; i < count; i++)
        {
            string id = _testRuneIds[i];

            if (string.IsNullOrEmpty(id))
            {
                continue;
            }
            
            slots[i] = DataManager.Inst.GetData<RuneData>(id);

            if (slots[i] == null)
            {
                Debug.LogError($"룬 데이터가 없습니다: {id}", this);
                return false;
            }
        }

        if (AttackProfileBuilder.TryBuild(slots, out profile, out string error) == false)
        {
            Debug.LogError(error, this);
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

        _hitTargets.Clear();

        foreach (Collider2D hit in hits)
        {
            RuneCombatActor target = hit.GetComponentInParent<RuneCombatActor>();

            // 한 몬스터에 콜라이더가 여러 개 있어도 타격당 한 번만 처리.
            if (target == null || !_hitTargets.Add(target))
            {
                continue;
            }

            RuneHitResolver.TryHitOnServer(_actor, target, profile, _attackPowerSnapshot);
        }
    }

    private void FireProjectile(AttackProfile profile)
    {
        PlayerAttackProjectile projectile = Instantiate(
            _projectilePrefab,
            GetAttackPosition(),
            Quaternion.identity);

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
