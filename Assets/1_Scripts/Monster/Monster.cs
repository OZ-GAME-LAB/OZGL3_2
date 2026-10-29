using Cysharp.Threading.Tasks;
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
    [SerializeField] private float _jumpForce = 5f;
    private float _attackRange;
    private readonly List<MonsterSkillBase> _skills = new();
    public IReadOnlyList<MonsterSkillBase> Skills => _skills;

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

    // test 속도 추후 데이터 반영 시 해당 변수 삭제
    private float _moveSpeed = 3f;
    /////////////////////////////////////////////////////

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

        GameObject visualPrefab = await ResourceManager.Inst.LoadAsset<GameObject>(data.MonsterAddressableId);

        if (visualPrefab == null)
            return false;

        GameObject visualObject = Instantiate(visualPrefab, transform, false);

        visualObject.transform.localPosition = Vector3.zero;

        if (!visualObject.TryGetComponent<MonsterVisual>(out var visual) || !visual.CheckMonsterVisualReady())
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

        return true;
    }

    public void InitPatrolArea(GameObject[] patrolSpots)
    {
        _patricList = patrolSpots;
        _patricListCount = patrolSpots.Length;
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
        // TODO : 몬스터 애니메이션 연결 부분 필요(이동 애니메이션 관련 메서드 호출)

        float distanceX = targetPosition.x - _rigidbody2D.position.x;
        Vector2 velocity = _rigidbody2D.linearVelocity;

        if (Mathf.Abs(distanceX) <= 0.05f)
        {
            velocity.x = 0f;
            _rigidbody2D.linearVelocity = velocity;
            return true;
        }

        velocity.x = Mathf.Clamp(distanceX / Time.fixedDeltaTime, -_moveSpeed, _moveSpeed);

        UpdateSightDirection(velocity.x);

        _rigidbody2D.linearVelocity = velocity;
        return false;
    }

    public void MonsterJump()
    {
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
        if (VisibleTarget == null || !VisibleTarget.isActiveAndEnabled)
        {
            MonsterStopMove();
            return;
        }

        MonsterPatricMoveAndCheck(VisibleTarget.bounds.center);
    }

    public bool IsTargetInAttackRange()
    {
        if (VisibleTarget == null || !VisibleTarget.isActiveAndEnabled)
            return false;

        Vector2 monsterPosition = transform.position;
        Vector2 targetPosition = VisibleTarget.bounds.center;

        float distance = Vector2.Distance(monsterPosition, targetPosition);

        return distance <= _attackRange;
    }

    public void MonsterStopMove()
    {
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

        if (data.AtkCollTime == null || data.AtkCollTime.Length != data.HaveSkillIdList.Length)
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
            float cooldown = data.AtkCollTime[i];

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

            if (this == null || skill == null)
                return false;

            skill.InitCooldown(cooldown);
            _skills.Add(skill);
        }

        return true;
    }

    public Transform GetAttackPoint(Transform target)
    {
        if (target == null)
            return null;

        return target.position.x >= transform.position.x ? _rightWallCheck : _leftWallCheck;
    }

    private void UpdateSightDirection(float directionX)
    {
        if (_monsterSight == null || Mathf.Approximately(directionX, 0f))
            return;

        Vector2 offset = _monsterSight.offset;

        offset.x = Mathf.Abs(offset.x) * (directionX > 0f ? 1f : -1f);

        _monsterSight.offset = offset;
    }
}
