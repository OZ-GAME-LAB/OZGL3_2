using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class Monster : MonoBehaviour
{
    [SerializeField] private Rigidbody2D _rigidbody2D;
    [SerializeField] private Transform _groundCheck;
    [SerializeField] private Transform _rightWallCheck;
    [SerializeField] private Transform _leftWallCheck;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private float _groundCheckRadius = 0.1f;
    [SerializeField, Min(0.01f)] private float _wallCheckDistance = 0.1f;
    [SerializeField] private float _jumpForce = 5f;
    private readonly RaycastHit2D[] _wallCheckHits = new RaycastHit2D[1];
    private float _attackRange;
    private readonly List<MonsterSkillBase> _skills = new();
    public IReadOnlyList<MonsterSkillBase> Skills => _skills;
    private MonsterNetwork _network;
    private MonsterRuneCombatActor _combatActor;
    private Coroutine _contactDamageRoutine;
    private readonly List<Collider2D> _bodyContacts = new();
    private readonly HashSet<PlayerRuneCombatActor> _contactDamageTargets = new();
    public MoveType MovementType { get; private set; }
    public bool CanMove => MovementType == MoveType.Move || MovementType == MoveType.Fly;
    public bool CanAttack => _network != null && _network.CanAttack;
    public bool IsStaggered => _network != null && _network.IsStaggered;
    public bool IsDashing
    {
        get
        {
            foreach (MonsterSkillBase skill in _skills)
            {
                if (skill != null && skill.isActiveAndEnabled && skill.IsDashing)
                    return true;
            }

            return false;
        }
    }
    public bool HasPatrolPoints => _patricListCount > 0;

    [SerializeField] private Collider2D _monsterCollider;
    [SerializeField] private Collider2D _monsterSight;
    [SerializeField] private Transform _eyePosition;
    [SerializeField] private LayerMask _sightBlockLayer;
    [SerializeField] private LayerMask _playerLayer;
    [SerializeField, Min(0.01f)] private float _sightCheckInterval = 0.2f;
    private readonly HashSet<Collider2D> _playerCandidates = new();
    private float _sightCheckTimer;
    public Collider2D VisibleTarget { get; private set; }

    private GameObject[] _patricList;
    private int _patricCurIndexNo = 0;
    private int _patricListCount;

    public MonsterVM ViewModel { get; } = new MonsterVM();

    private float _moveSpeed = 3f;
    private bool _isChasing;

    private void Awake()
    {
        _network = GetComponent<MonsterNetwork>();
        _combatActor = GetComponent<MonsterRuneCombatActor>();
        if (_rigidbody2D == null)
            _rigidbody2D = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        _contactDamageRoutine = StartCoroutine(ContactDamageRoutine());
    }

    private void OnDisable()
    {
        if (_contactDamageRoutine != null)
        {
            StopCoroutine(_contactDamageRoutine);
            _contactDamageRoutine = null;
        }

        _bodyContacts.Clear();
        _contactDamageTargets.Clear();
    }

    private IEnumerator ContactDamageRoutine()
    {
        WaitForFixedUpdate wait = new WaitForFixedUpdate();
        while (true)
        {
            yield return wait;
            ApplyContactDamageOnServer();
        }
    }

    private void ApplyContactDamageOnServer()
    {
        if (_network == null || !_network.IsServer || !_network.IsSpawned || !_network.IsReady
            || _combatActor == null || !_combatActor.CanAttack
            || _monsterCollider == null || !_monsterCollider.isActiveAndEnabled)
            return;

        _bodyContacts.Clear();
        _contactDamageTargets.Clear();
        _monsterCollider.GetContacts(new ContactFilter2D { useTriggers = false }, _bodyContacts);

        foreach (Collider2D contact in _bodyContacts)
        {
            if (contact == null)
                continue;

            PlayerRuneCombatActor player = contact.GetComponentInParent<PlayerRuneCombatActor>();
            if (player == null || !_combatActor.CanHit(player) || !_contactDamageTargets.Add(player))
                continue;

            Vector2 hitDirection = contact.bounds.center - _monsterCollider.bounds.center;
            player.ReceiveContactDamageOnServer(_combatActor, hitDirection);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if ((_playerLayer.value & (1 << other.gameObject.layer)) == 0)
            return;

        if (_monsterSight == null || !_monsterSight.IsTouching(other))
            return;

        _playerCandidates.Add(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (_monsterSight != null && _monsterSight.IsTouching(other))
            return;

        _playerCandidates.Remove(other);

        if (VisibleTarget == other)
            VisibleTarget = null;
    }

    private void Update()
    {
        _sightCheckTimer -= Time.deltaTime;

        if (_sightCheckTimer > 0f)
            return;

        _sightCheckTimer = _sightCheckInterval;
        RefreshVisibleTarget();
    }

    public async UniTask<bool> InitMonsterVisual(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            Debug.LogError("몬스터 ID가 없습니다.");
            return false;
        }

        MonsterData data = DataManager.Inst.GetData<MonsterData>(id);

        if (data == null || string.IsNullOrWhiteSpace(data.MonsterAddressableId))
        {
            Debug.LogError($"몬스터 데이터 또는 외형 주소가 없습니다: {id}");
            return false;
        }

        MovementType = data.MonsterType;

        GameObject visualPrefab = await ResourceManager.Inst.LoadAsset<GameObject>(data.MonsterAddressableId);

        if (this == null || cancellationToken.IsCancellationRequested || _network == null || !_network.IsSpawned || visualPrefab == null)
            return false;

        GameObject visualObject = Instantiate(visualPrefab, transform, false);

        visualObject.transform.localPosition = Vector3.zero;

        if (!visualObject.TryGetComponent<MonsterVisual>(out var visual) || !visual.CheckMonsterVisualReady(MovementType, data.Atk > 0f))
        {
            Debug.LogError($"MonsterVisual의 참조가 준비되지 않았습니다: {id}");
            Destroy(visualObject);
            return false;
        }

        _groundCheck = visual.GroundCheckReturn();
        _rightWallCheck = visual.RightCheckReturn();
        _leftWallCheck = visual.LeftCheckReturn();
        _eyePosition = visual.EyePositionReturn();
        _monsterCollider = visual.MonsterColliderReturn();
        _attackRange = data.AtkDistance;

        if (MovementType == MoveType.Fix && _monsterSight != null)
            _monsterSight.offset = Vector2.zero;

        if (CanMove)
        {
            _moveSpeed = data.MonsterSpeed;

            if (float.IsNaN(_moveSpeed) || float.IsInfinity(_moveSpeed) || _moveSpeed <= 0f)
            {
                Debug.LogWarning($"몬스터 이동 속도가 올바르지 않아 3을 사용합니다: {id}, 값: {data.MonsterSpeed}", this);
                _moveSpeed = 3f;
            }

        }

        return true;
    }

    public void InitPatrolArea(GameObject[] patrolSpots)
    {
        _patricList = patrolSpots;
        _patricListCount = patrolSpots == null ? 0 : patrolSpots.Length;
        _patricCurIndexNo = 0;
    }

    private void RefreshVisibleTarget()
    {
        VisibleTarget = null;

        if (_monsterSight == null || !_monsterSight.isActiveAndEnabled || _eyePosition == null)
            return;

        _playerCandidates.RemoveWhere(candidate =>
            candidate == null || !candidate.isActiveAndEnabled || !_monsterSight.IsTouching(candidate));

        float nearestDistance = float.PositiveInfinity;

        foreach (Collider2D candidate in _playerCandidates)
        {
            PlayerNetworkStats player = candidate.GetComponentInParent<PlayerNetworkStats>();
            if (player == null || !player.IsAlive)
                continue;

            Vector3 targetPosition = candidate.bounds.center;

            if (!HasClearSight(targetPosition))
                continue;

            float distance = ((Vector2)targetPosition - (Vector2)_eyePosition.position).sqrMagnitude;

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                VisibleTarget = candidate;
            }
        }
    }

    public bool MonsterPatricMoveAndCheck(Vector3 targetPosition)
    {
        _isChasing = false;
        return MoveAndCheck(targetPosition, _moveSpeed);
    }

    private bool MoveAndCheck(Vector3 targetPosition, float moveSpeed)
    {
        if (!CanMove || _rigidbody2D == null)
            return false;

        if (IsStaggered)
        {
            MonsterStopMove();
            return false;
        }

        // TODO : 몬스터 애니메이션 연결 부분 필요(이동 애니메이션 관련 메서드 호출)

        float distanceX = targetPosition.x - _rigidbody2D.position.x;
        Vector2 velocity = _rigidbody2D.linearVelocity;

        if (Mathf.Abs(distanceX) <= 0.05f)
        {
            velocity.x = 0f;
            _rigidbody2D.linearVelocity = velocity;
            return true;
        }

        velocity.x = Mathf.Clamp(distanceX / Time.fixedDeltaTime, -moveSpeed, moveSpeed);

        UpdateSightDirection(velocity.x);

        _rigidbody2D.linearVelocity = velocity;

        Transform wallCheck = distanceX > 0f ? _rightWallCheck : _leftWallCheck;

        if (MovementType == MoveType.Move && wallCheck != null)
        {
            Vector2 direction = distanceX > 0f ? Vector2.right : Vector2.left;
            ContactFilter2D wallFilter = new ContactFilter2D { useTriggers = false };
            wallFilter.SetLayerMask(_groundLayer);

            if (Physics2D.Raycast(wallCheck.position, direction, wallFilter, _wallCheckHits, _wallCheckDistance) > 0)
                MonsterJump();
        }

        return false;
    }

    public void MonsterJump()
    {
        if (MovementType != MoveType.Move || IsStaggered || _rigidbody2D == null || _groundCheck == null)
            return;

        bool isGrounded = Physics2D.OverlapCircle(
        _groundCheck.position,
        _groundCheckRadius,
        _groundLayer) != null;

        if (!isGrounded || _rigidbody2D.linearVelocity.y > 0f)
            return;

        _rigidbody2D.AddForce(Vector2.up * _jumpForce, ForceMode2D.Impulse);
    }

    public Vector3 MonsterPatricPositionSelect()
    {
        if (!CanMove || !HasPatrolPoints)
            return transform.position;

        if (_patricCurIndexNo >= _patricListCount)
        {
            _patricCurIndexNo = 0;
        }

        Vector3 target = _patricList[_patricCurIndexNo].transform.position;

        _patricCurIndexNo++;

        return target;
    }

    public void MonsterChaisingTarget()
    {
        if (!CanMove || VisibleTarget == null || !VisibleTarget.isActiveAndEnabled)
        {
            MonsterStopMove();
            return;
        }

        if (TryGetAttackSkill(out MonsterSkillBase skill) && skill.StopAtAttackRange && skill.IsAttacking)
        {
            MonsterStopMove();
            return;
        }

        _isChasing = true;
        MoveAndCheck(VisibleTarget.bounds.center, _moveSpeed * 1.2f);
        LimitRangedChaseMovement();
    }

    private void FixedUpdate()
    {
        if (_isChasing)
            LimitRangedChaseMovement();
    }

    private void LimitRangedChaseMovement()
    {
        if (_rigidbody2D == null || _monsterCollider == null || VisibleTarget == null
            || !VisibleTarget.isActiveAndEnabled || !TryGetAttackSkill(out MonsterSkillBase skill)
            || !skill.StopAtAttackRange)
            return;

        ColliderDistance2D result = _monsterCollider.Distance(VisibleTarget);
        if (!result.isValid)
            return;

        if (result.distance <= _attackRange)
        {
            MonsterStopMove();
            return;
        }

        float maxSpeed = (result.distance - _attackRange + 0.001f) / Time.fixedDeltaTime;
        Vector2 velocity = _rigidbody2D.linearVelocity;
        velocity.x = Mathf.Clamp(velocity.x, -maxSpeed, maxSpeed);
        _rigidbody2D.linearVelocity = velocity;
    }

    public bool IsTargetInAttackRange()
    {
        if (_monsterCollider == null || !_monsterCollider.isActiveAndEnabled || VisibleTarget == null || !VisibleTarget.isActiveAndEnabled)
            return false;

        ColliderDistance2D result = _monsterCollider.Distance(VisibleTarget);

        if (!result.isValid)
            return false;

        float distance = Mathf.Max(0f, result.distance);

        return distance <= _attackRange;
    }

    public void MonsterStopMove()
    {
        _isChasing = false;
        if (_rigidbody2D == null)
            return;

        Vector2 velocity = _rigidbody2D.linearVelocity;
        velocity.x = 0f;
        _rigidbody2D.linearVelocity = velocity;
    }

    public bool HasClearSight(Vector3 targetPosition)
    {
        Vector2 origin = _eyePosition.position;
        Vector2 direction = (Vector2)targetPosition - origin;
        float distance = direction.magnitude;

        if (distance <= 0.001f)
            return true;

        RaycastHit2D hit = Physics2D.Raycast(
            origin,
            direction.normalized,
            distance,
            _sightBlockLayer);

        return hit.collider == null;
    }

    public async UniTask<bool> InitMonsterSkills(string id)
    {
        MonsterData data = DataManager.Inst.GetData<MonsterData>(id);

        if (data == null)
            return false;

        if (data.HaveSkillIdList == null || data.HaveSkillIdList.Length == 0)
            return true;

        if (data.MonsterSkillCollTime == null || data.MonsterSkillCollTime.Length != data.HaveSkillIdList.Length)
        {
            Debug.LogError($"스킬과 쿨타임 배열의 개수가 다릅니다: {id}", this);
            return false;
        }

        if (MonsterSkillManager.Inst == null)
        {
            Debug.LogError("MonsterSkillManager가 준비되지 않았습니다.", this);
            return false;
        }

        if (_monsterCollider == null)
            return false;

        Physics2D.SyncTransforms();

        float bodyHeight = _monsterCollider.bounds.size.y;

        if (bodyHeight <= 0f)
        {
            Debug.LogError("몬스터 몸 Collider의 높이를 확인하세요.", this);
            return false;
        }

        for (int i = 0; i < data.HaveSkillIdList.Length; i++)
        {
            float cooldown = data.MonsterSkillCollTime[i];

            if (float.IsNaN(cooldown) || float.IsInfinity(cooldown) || cooldown < 0f)
            {
                Debug.LogError($"잘못된 쿨타임입니다: {id}, 인덱스 {i}", this);
                return false;
            }

            MonsterSkillBase skill = await MonsterSkillManager.Inst.CreateSkillAsync(
                    data.HaveSkillIdList[i],
                    transform,
                    bodyHeight,
                    data.Atk);

            if (this == null || _network == null || !_network.IsSpawned || skill == null)
                return false;

            skill.InitCooldown(cooldown);
            _skills.Add(skill);
        }

        return true;
    }

    public bool TryGetAttackSkill(out MonsterSkillBase skill)
    {
        skill = null;

        if (!CanAttack)
            return false;

        foreach (MonsterSkillBase candidate in _skills)
        {
            if (candidate == null || !candidate.isActiveAndEnabled || candidate.IsDeathSkill)
                continue;

            skill = candidate;
            return true;
        }

        return false;
    }

    public void ExecuteDeathSkills()
    {
        if (_network == null || !_network.IsServer || !_network.IsSpawned || _network.IsAlive)
            return;

        foreach (MonsterSkillBase skill in _skills)
        {
            if (skill != null && !skill.IsDeathSkill)
                skill.enabled = false;
        }

        foreach (MonsterSkillBase skill in _skills)
        {
            if (skill == null || !skill.isActiveAndEnabled || !skill.IsDeathSkill)
                continue;

            try
            {
                skill.UseSkill(null, transform.position);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, skill);
            }
        }
    }

    public Transform GetAttackPoint(Transform target)
    {
        if (target == null)
            return null;

        return target.position.x >= transform.position.x ? _rightWallCheck : _leftWallCheck;
    }

#if UNITY_EDITOR
    [ContextMenu("진단/몬스터 공격 상태 확인")]
    private void LogAttackDiagnostics()
    {
        var message = new System.Text.StringBuilder();
        message.AppendLine($"[몬스터 공격 진단] {name}, 위치: {transform.position}, 유형: {MovementType}");
        message.AppendLine($"Monster 활성: {isActiveAndEnabled}, 공격 가능: {CanAttack}, 사거리: {_attackRange}, 감지 후보: {_playerCandidates.Count}");
        message.AppendLine($"현재 감지 대상: {(VisibleTarget != null ? VisibleTarget.name : "없음")}");

        if (_network != null)
            message.AppendLine($"서버: {_network.IsServer}, Spawn: {_network.IsSpawned}, 초기화 완료: {_network.IsReady}, 생존: {_network.IsAlive}, 공격력: {_network.AttackPower}, 경직: {_network.IsStaggered}");

        var bt = GetComponent<Unity.Behavior.BehaviorGraphAgent>();
        message.AppendLine($"BT 활성: {bt != null && bt.isActiveAndEnabled}, 그래프 연결: {bt != null && bt.Graph != null}");
        message.AppendLine($"감지 Collider 활성: {_monsterSight != null && _monsterSight.isActiveAndEnabled}, 몸 Collider 활성: {_monsterCollider != null && _monsterCollider.isActiveAndEnabled}");
        if (_rigidbody2D != null)
            message.AppendLine($"물리 활성: {_rigidbody2D.simulated}, Body Type: {_rigidbody2D.bodyType}");

        foreach (MonsterSkillBase skill in _skills)
        {
            if (skill != null)
                message.AppendLine($"스킬: {skill.SkillId}, 클래스: {skill.GetType().Name}, 활성: {skill.isActiveAndEnabled}, 사망 스킬: {skill.IsDeathSkill}, 실행 중: {skill.IsAttacking}, 쿨타임 완료: {skill.IsCooldownComplete}");
        }

        foreach (PlayerRuneCombatActor player in FindObjectsByType<PlayerRuneCombatActor>(FindObjectsSortMode.None))
        {
            foreach (Collider2D other in player.GetComponentsInChildren<Collider2D>())
            {
                if (other.isTrigger || !other.isActiveAndEnabled)
                    continue;

                bool layerMatches = (_playerLayer.value & (1 << other.gameObject.layer)) != 0;
                bool touching = _monsterSight != null && _monsterSight.IsTouching(other);
                bool overlaps = _monsterSight != null && _monsterSight.Distance(other).isOverlapped;
                message.AppendLine($"플레이어 Collider: {other.name}, 위치: {other.bounds.center}, 레이어 일치: {layerMatches}, 감지 원 겹침: {overlaps}, 물리 접촉: {touching}, 후보 등록: {_playerCandidates.Contains(other)}");

                if (_monsterCollider != null)
                {
                    ColliderDistance2D distance = _monsterCollider.Distance(other);
                    message.AppendLine($"거리 계산 유효: {distance.isValid}, 몸 사이 거리: {Mathf.Max(0f, distance.distance):F3}, 사거리 안: {distance.isValid && Mathf.Max(0f, distance.distance) <= _attackRange}");
                }

                if (_eyePosition != null)
                {
                    Vector2 direction = other.bounds.center - _eyePosition.position;
                    RaycastHit2D obstruction = Physics2D.Raycast(_eyePosition.position, direction.normalized, direction.magnitude, _sightBlockLayer);
                    message.AppendLine($"눈 위치: {_eyePosition.position}, 시야를 막은 Collider: {(obstruction.collider != null ? obstruction.collider.name : "없음")}, 공격 위치 연결: {GetAttackPoint(other.transform) != null}");
                }
            }
        }

        Debug.Log(message.ToString(), this);
    }
#endif

    private void UpdateSightDirection(float directionX)
    {
        if (_monsterSight == null || Mathf.Approximately(directionX, 0f))
            return;

        Vector2 offset = _monsterSight.offset;

        offset.x = Mathf.Abs(offset.x) * (directionX > 0f ? 1f : -1f);

        _monsterSight.offset = offset;
    }
}
