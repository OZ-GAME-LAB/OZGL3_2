using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerWallCling : MonoBehaviour
{
    [Header("벽 감지")]
    [Tooltip("오른쪽을 볼 때의 손 높이 기준점입니다. 부모의 좌우 반전을 그대로 따릅니다.")]
    [SerializeField] private Transform _wallProbeOrigin;
    [SerializeField] private LayerMask _wallLayers = ~0;
    [Tooltip("기준점에서 벽 방향으로 탐색하는 거리입니다. 거리와 높이는 월드 단위입니다.")]
    [SerializeField, Min(0.01f)] private float _wallCheckDistance = 0.2f;
    [Tooltip("Ray 시작점을 몸 안쪽으로 물리는 거리입니다. 벽 내부에서 Ray가 시작되는 것을 방지합니다.")]
    [SerializeField, Min(0f)] private float _rayStartInset = 0.25f;
    [Tooltip("손 높이보다 이만큼 위에도 벽이 있으면 기존 벽 붙잡기 모션을 사용합니다.")]
    [SerializeField, Min(0.01f)] private float _upperCheckHeight = 0.9f;
    [Tooltip("벽면에서 안쪽으로 이만큼 들어가 윗면을 확인합니다. 아주 얇은 지형은 더 작은 값이 필요합니다.")]
    [SerializeField, Min(0.01f)] private float _ledgeSurfaceInset = 0.05f;

    [Header("모서리 매달리기 위치 보정")]
    [Tooltip("오른쪽을 보는 ledge_hang 모션의 손끝 위치입니다. Sprite가 아닌 Player 기준 로컬 좌표입니다.")]
    [SerializeField] private Vector2 _ledgeHandLocalPosition = new Vector2(1.45f, 2.31f);
    [Tooltip("손을 모서리에 맞출 때 허용하는 몸의 최대 이동 거리입니다. 경로가 막혀 있으면 보정하지 않습니다.")]
    [SerializeField, Min(0f)] private float _maxLedgeSnapDistance = 1.5f;

    [Header("벽 점프")]
    [SerializeField, Min(0f)] private float _releaseJumpGrace = 0.1f;
    [SerializeField, Min(0f)] private float _regrabDelay = 0.2f;
    [SerializeField, Min(0f)] private float _jumpHorizontalSpeed = 12f;
    [SerializeField, Min(0f)] private float _jumpControlTime = 0.12f;

    private Rigidbody2D _body;
    private BoxCollider2D _box;
    private PhysicsMaterial2D _originalMaterial;
    private PhysicsMaterial2D _airMaterial;
    private float _savedGravity;
    private bool _usingAirMaterial;
    private int _lastWallDirection;
    private float _wallJumpExpiresAt = -1f;
    private int _blockedWallDirection;
    private bool _leftJumpWall;
    private bool _releasedWallInput;
    private float _regrabBlockedUntil;
    private float _jumpControlUntil;
    private float _jumpVelocityX;
    private const float SurfaceTolerance = 0.02f;
    private const float ClearanceInset = 0.005f;
    // 위치 계산 오차를 흡수하는 수치 기준입니다. 실제 이동과 모든 보정 검사가 함께 사용합니다.
    private const float SnapPositionTolerance = 0.001f;
    private Collider2D _ledgeSupport;
    private Vector2 _ledgeLocalPoint;
    private Vector2 _ledgeLocalSidePoint;
    private bool _lastWasLedge;
    private bool _blockedFromLedge;
    private Collider2D _blockedLedgeSupport;
    private readonly List<RaycastHit2D> _rayHits = new List<RaycastHit2D>(16);
    private readonly List<Collider2D> _overlapHits = new List<Collider2D>(16);
    private readonly List<Collider2D> _ledgePassThroughPlatforms = new List<Collider2D>(4);
    private readonly List<Collider2D> _snapPassThroughPlatforms = new List<Collider2D>(4);

    public bool IsClinging { get; private set; }
    public bool IsLedgeHanging { get; private set; }
    public int WallDirection { get; private set; }
    public bool CanWallJump => IsClinging ||
        (_lastWallDirection != 0 && Time.time <= _wallJumpExpiresAt);

    public void Initialize(Rigidbody2D body, Collider2D bodyCollider)
    {
        _body = body;
        _box = bodyCollider as BoxCollider2D;
        if (_body == null || _box == null || _wallProbeOrigin == null)
        {
            Debug.LogWarning("벽 붙잡기에 Rigidbody2D, 몸 BoxCollider2D, Wall Probe Origin이 필요합니다.", this);
            enabled = false;
            return;
        }
        _originalMaterial = _box.sharedMaterial;
        PhysicsMaterial2D source = _originalMaterial != null ? _originalMaterial : _body.sharedMaterial;
        _airMaterial = new PhysicsMaterial2D("Player Air Friction")
        {
            hideFlags = HideFlags.HideAndDontSave,
            friction = 0f,
            bounciness = source != null ? source.bounciness : 0f
        };
        SyncSensorTransform();
    }

    public void SyncSensorTransform()
    {
        if (_wallProbeOrigin == null) return;
        // 초기화와 방향 전환 때만 호출합니다. 센서 설정은 바꾸지 않고
        // 부모 Transform 반전을 다음 물리 조회에 반영합니다.
        Physics2D.SyncTransforms();
    }

    public void UpdateState(float input, bool grounded, bool airborneAllowed, bool inputEnabled)
    {
        if (!enabled || _body == null || _box == null) return;
        if (grounded || _wallProbeOrigin == null || !_wallProbeOrigin.gameObject.activeInHierarchy)
        {
            ResetState();
            return;
        }
        SetAirMaterial(true);
        ContactFilter2D filter = CreateFilter(true);
        if (_blockedWallDirection != 0)
        {
            if (HasLeftJumpWall(filter)) _leftJumpWall = true;
            if (input * _blockedWallDirection <= 0.01f) _releasedWallInput = true;
        }
        int facingDirection = transform.lossyScale.x < 0f ? -1 : 1;
        int side = input * facingDirection > 0.01f ? facingDirection : 0;
        bool regrabAllowed = Time.time >= _regrabBlockedUntil &&
            (side != _blockedWallDirection || _leftJumpWall || _releasedWallInput);
        if (!inputEnabled || !airborneAllowed || side == 0 || !regrabAllowed)
        {
            EndCling(inputEnabled);
            if (!inputEnabled) { _lastWallDirection = 0; _wallJumpExpiresAt = -1f; }
            return;
        }
        if (IsLedgeHanging)
        {
            // 보정 후에는 원래 감지점이 이동하므로, 잡은 모서리를 기준으로 유지합니다.
            if (side != WallDirection || !TryMaintainLedge(filter)) EndCling(true);
            else HoldStill();
            return;
        }

        Vector2 origin = GetRayOrigin(side);
        float distance = _rayStartInset + _wallCheckDistance;
        if (!TryRay(origin, Vector2.right * side, distance, filter, out RaycastHit2D wall) ||
            !IsSideSurface(wall, side))
        {
            EndCling(true);
            return;
        }

        Vector2 upperOrigin = origin + Vector2.up * _upperCheckHeight;
        if (TryRay(upperOrigin, Vector2.right * side, distance, filter, out RaycastHit2D upper))
        {
            if (!IsSideSurface(upper, side)) { EndCling(true); return; }
            BeginCling(side, false);
        }
        else
        {
            if (!TryGetLedge(wall, upperOrigin.y, side, filter, out Vector2 corner) ||
                !TryAlignToLedge(corner, side))
            {
                // 윗면이나 안전한 보정 공간이 없으면 허공에 벽 모션을 출력하지 않습니다.
                EndCling(true);
                return;
            }
            _ledgeSupport = wall.collider;
            _ledgeLocalPoint = _ledgeSupport.transform.InverseTransformPoint(corner);
            _ledgeLocalSidePoint = _ledgeSupport.transform.InverseTransformPoint(wall.point);
            BeginCling(side, true);
        }
        HoldStill();
    }

    private void BeginCling(int side, bool ledge)
    {
        if (!IsClinging)
        {
            _savedGravity = _body.gravityScale;
            IsClinging = true;
        }
        IsLedgeHanging = ledge;
        _lastWasLedge = ledge;
        WallDirection = side;
        _lastWallDirection = side;
    }

    private void HoldStill()
    {
        _wallJumpExpiresAt = Time.time + _releaseJumpGrace;
        _body.gravityScale = 0f;
        _body.linearVelocity = Vector2.zero;
    }

    private ContactFilter2D CreateFilter(bool wallOnly)
    {
        ContactFilter2D filter = new ContactFilter2D();
        int mask = Physics2D.GetLayerCollisionMask(_box.gameObject.layer);
        filter.SetLayerMask(wallOnly ? mask & _wallLayers.value : mask);
        filter.useTriggers = false;
        return filter;
    }

    private bool IsObstacle(Collider2D hit, bool excludeOneWay)
    {
        if (hit == null || !hit.isActiveAndEnabled || hit.isTrigger ||
            hit.attachedRigidbody == _body || Physics2D.GetIgnoreCollision(_box, hit)) return false;
        if (excludeOneWay && hit.usedByEffector && hit.TryGetComponent(out PlatformEffector2D effector) &&
            effector.enabled && effector.useOneWay) return false;
        return true;
    }

    private bool TryRay(Vector2 origin, Vector2 direction, float distance,
        ContactFilter2D filter, out RaycastHit2D closest)
    {
        closest = default;
        Physics2D.Raycast(origin, direction, filter, _rayHits, distance);
        bool found = false;
        for (int i = 0; i < _rayHits.Count; i++)
        {
            RaycastHit2D hit = _rayHits[i];
            if (!IsObstacle(hit.collider, true) || (found && hit.distance >= closest.distance)) continue;
            closest = hit;
            found = true;
        }
        return found;
    }

    private static bool IsSideSurface(RaycastHit2D hit, int side)
    {
        // 내부에서 시작한 Ray의 가짜 법선은 벽면으로 인정하지 않습니다.
        return hit.distance > 0f && hit.normal.x * side <= -0.7f && Mathf.Abs(hit.normal.y) <= 0.5f;
    }

    private bool TryGetLedge(RaycastHit2D wall, float upperY, int side,
        ContactFilter2D filter, out Vector2 corner)
    {
        corner = default;
        Vector2 start = new Vector2(wall.point.x + side * _ledgeSurfaceInset, upperY);
        if (!TryRay(start, Vector2.down, upperY - wall.point.y + SurfaceTolerance, filter, out RaycastHit2D top) ||
            top.collider != wall.collider || top.distance <= 0f || top.normal.y < 0.9f) return false;
        corner = new Vector2(wall.point.x, top.point.y);
        return true;
    }

    private bool TryMaintainLedge(ContactFilter2D filter)
    {
        if (!IsObstacle(_ledgeSupport, true)) return false;
        Vector2 corner = _ledgeSupport.transform.TransformPoint(_ledgeLocalPoint);
        Vector2 topStart = corner + new Vector2(WallDirection * _ledgeSurfaceInset, SurfaceTolerance);
        if (!TryRay(topStart, Vector2.down, SurfaceTolerance * 2f, filter, out RaycastHit2D top) ||
            top.collider != _ledgeSupport || top.distance <= 0f || top.normal.y < 0.9f) return false;
        Vector2 sideStart = (Vector2)_ledgeSupport.transform.TransformPoint(_ledgeLocalSidePoint) -
            Vector2.right * WallDirection * SurfaceTolerance;
        if (!TryRay(sideStart, Vector2.right * WallDirection, SurfaceTolerance * 2f, filter, out RaycastHit2D wall) ||
            wall.collider != _ledgeSupport || !IsSideSurface(wall, WallDirection)) return false;
        return TryAlignToLedge(corner, WallDirection);
    }

    private Vector2 HandOffset(int side)
    {
        // 방향을 바꿔도 점프한 벽 쪽을 검사할 수 있도록 방향을 인수로 받습니다.
        Vector3 scale = transform.lossyScale;
        return new Vector2(_ledgeHandLocalPosition.x * Mathf.Abs(scale.x) * side,
            _ledgeHandLocalPosition.y * scale.y);
    }

    private Vector2 GetRayOrigin(int side)
    {
        Vector2 offset = _wallProbeOrigin.position - transform.position;
        int facing = transform.lossyScale.x < 0f ? -1 : 1;
        offset.x *= side * facing;
        return _body.position + offset - Vector2.right * side * _rayStartInset;
    }

    private bool TryAlignToLedge(Vector2 corner, int side)
    {
        Vector2 target = corner - HandOffset(side);
        Vector2 delta = target - _body.position;
        if (delta.sqrMagnitude <= SnapPositionTolerance * SnapPositionTolerance)
            delta = Vector2.zero;
        if (delta.sqrMagnitude > _maxLedgeSnapDistance * _maxLedgeSnapDistance || !TryValidateBodySnap(delta)) return false;
        if (delta.sqrMagnitude > 0f)
            _body.position += delta;
        return true;
    }

    // delta는 TryAlignToLedge에서 제자리 판정을 마친 실제 이동량입니다.
    // 검사 중 기록은 임시 목록에 모으고, 전체 성공 시에만 확정합니다.
    private bool TryValidateBodySnap(Vector2 delta)
    {
        _snapPassThroughPlatforms.Clear();
        // 감지 대상 레이어 외의 지형도 보정 경로를 막아야 합니다.
        ContactFilter2D filter = CreateFilter(false);
        float distance = delta.magnitude;
        if (distance > 0f)
        {
            _box.Cast(delta / distance, filter, _rayHits, distance);
            for (int i = 0; i < _rayHits.Count; i++)
            {
                RaycastHit2D hit = _rayHits[i];
                if (!IsObstacle(hit.collider, false)) continue;
                if (CanPassPlatformForSnap(hit.collider, delta))
                {
                    RecordSnapPlatform(hit.collider);
                    continue;
                }
                // 이미 닿은 벽에서 바깥으로 떨어지는 이동은 허용합니다.
                if (hit.distance <= ClearanceInset && Vector2.Dot(delta, hit.normal) >= 0f) continue;
                if (hit.distance < distance) return false;
            }
        }
        Vector3 scale = _box.transform.lossyScale;
        Vector2 size = new Vector2(Mathf.Abs(scale.x) * _box.size.x, Mathf.Abs(scale.y) * _box.size.y);
        size -= Vector2.one * ClearanceInset * 2f;
        // 보정 직후 Transform은 이전 프레임 위치일 수 있으므로 물리 콜라이더를 기준으로 합니다.
        Vector2 center = (Vector2)_box.bounds.center + delta;
        Physics2D.OverlapBox(center, size, _box.transform.eulerAngles.z, filter, _overlapHits);
        for (int i = 0; i < _overlapHits.Count; i++)
        {
            Collider2D obstacle = _overlapHits[i];
            if (!IsObstacle(obstacle, false)) continue;
            if (!CanPassPlatformForSnap(obstacle, delta)) return false;
            RecordSnapPlatform(obstacle);
        }
        // 경로와 도착 위치 검사가 모두 성공했을 때만 유지 검사에 사용할 기록을 갱신합니다.
        _ledgePassThroughPlatforms.Clear();
        _ledgePassThroughPlatforms.AddRange(_snapPassThroughPlatforms);
        return true;
    }

    private void RecordSnapPlatform(Collider2D platform)
    {
        if (!_snapPassThroughPlatforms.Contains(platform)) _snapPassThroughPlatforms.Add(platform);
    }

    private bool CanPassPlatformForSnap(Collider2D platform, Vector2 delta)
    {
        if (!platform.usedByEffector || !platform.TryGetComponent(out PlatformEffector2D effector) ||
            !effector.enabled || !effector.useOneWay) return false;

        Vector2 up = Quaternion.Euler(0f, 0f, effector.rotationalOffset) * effector.transform.up;
        float upwardTravel = Vector2.Dot(delta, up);
        if (delta.sqrMagnitude == 0f)
        {
            // 제자리 유지는 이번 매달림에서 이미 통과를 승인한 발판에만 적용합니다.
            if (!IsLedgeHanging || !_ledgePassThroughPlatforms.Contains(platform)) return false;
        }
        else if (upwardTravel <= 0f)
        {
            // 실제 하강이나 수평 이동에는 이전 통과 기록을 적용하지 않습니다.
            return false;
        }

        Bounds bodyBounds = _box.bounds;
        Bounds platformBounds = platform.bounds;
        Vector2 axis = new Vector2(Mathf.Abs(up.x), Mathf.Abs(up.y));
        float bodyBottom = Vector2.Dot(bodyBounds.center, up) - Vector2.Dot(bodyBounds.extents, axis);
        float platformBottom = Vector2.Dot(platformBounds.center, up) - Vector2.Dot(platformBounds.extents, axis);
        // 몸의 아래쪽이 발판 아래에 있어야 합니다. 이미 발판 위에 선 상태는 제외합니다.
        if (bodyBottom >= platformBottom - ClearanceInset) return false;

        return true;
    }

    private bool HasLeftJumpWall(ContactFilter2D filter)
    {
        // 모서리 매달리기는 몸이 벽에서 떨어져 있으므로 손과 지형 사이 거리로 확인합니다.
        if (_blockedFromLedge)
        {
            if (!IsObstacle(_blockedLedgeSupport, true)) return true;
            Vector2 hand = _body.position + HandOffset(_blockedWallDirection);
            return Vector2.Distance(hand, _blockedLedgeSupport.ClosestPoint(hand)) > _wallCheckDistance;
        }
        // Ray 범위 안에서는 몸이 아직 닿지 않아도 같은 벽에 붙은 것으로 취급합니다.
        // 방향 전환으로 기준점이 반대편으로 이동해도 점프한 벽 쪽을 계속 검사합니다.
        return !TryRay(GetRayOrigin(_blockedWallDirection), Vector2.right * _blockedWallDirection,
            _rayStartInset + _wallCheckDistance, filter, out RaycastHit2D wall) ||
            !IsSideSurface(wall, _blockedWallDirection);
    }

    public void ReleaseForInput(float input)
    {
        if (IsClinging && input * WallDirection <= 0.01f) EndCling(true);
    }

    private void EndCling(bool allowGrace)
    {
        if (!IsClinging) return;
        _lastWallDirection = WallDirection;
        _wallJumpExpiresAt = allowGrace ? Time.time + _releaseJumpGrace : -1f;
        IsClinging = false;
        IsLedgeHanging = false;
        _ledgePassThroughPlatforms.Clear();
        WallDirection = 0;
        if (_body != null) _body.gravityScale = _savedGravity;
    }

    public bool TryJump(float input, float jumpImpulse, out bool idleJump)
    {
        idleJump = true;
        if (!enabled || _body == null || !CanWallJump) return false;
        int side = IsClinging ? WallDirection : _lastWallDirection;
        idleJump = input * side >= -0.01f;
        _blockedFromLedge = _lastWasLedge;
        _blockedLedgeSupport = _lastWasLedge ? _ledgeSupport : null;
        EndCling(false);
        _lastWallDirection = 0;
        _wallJumpExpiresAt = -1f;
        _blockedWallDirection = side;
        _leftJumpWall = false;
        _releasedWallInput = input * side <= 0.01f;
        _regrabBlockedUntil = Time.time + _regrabDelay;
        _jumpControlUntil = Time.time + _jumpControlTime;
        _jumpVelocityX = idleJump ? 0f : -side * _jumpHorizontalSpeed;
        _body.linearVelocity = new Vector2(_jumpVelocityX, 0f);
        _body.AddForce(Vector2.up * jumpImpulse, ForceMode2D.Impulse);
        return true;
    }

    public float ResolveHorizontalVelocity(float requested)
    {
        if (IsClinging) return 0f;
        return Time.time < _jumpControlUntil ? _jumpVelocityX : requested;
    }

    private void SetAirMaterial(bool airborne)
    {
        if (_box == null || _usingAirMaterial == airborne) return;
        _box.sharedMaterial = airborne ? _airMaterial : _originalMaterial;
        _usingAirMaterial = airborne;
    }

    public void ResetState()
    {
        EndCling(false);
        _lastWallDirection = 0;
        _wallJumpExpiresAt = -1f;
        _blockedWallDirection = 0;
        _blockedLedgeSupport = _ledgeSupport = null;
        _blockedFromLedge = false;
        _lastWasLedge = false;
        _ledgePassThroughPlatforms.Clear();
        _snapPassThroughPlatforms.Clear();
        _leftJumpWall = _releasedWallInput = false;
        _regrabBlockedUntil = _jumpControlUntil = 0f;
        SetAirMaterial(false);
    }

    private void OnDrawGizmosSelected()
    {
        if (_wallProbeOrigin == null) return;
        int side = transform.lossyScale.x < 0f ? -1 : 1;
        Vector3 start = _wallProbeOrigin.position - Vector3.right * side * _rayStartInset;
        Vector3 ray = Vector3.right * side * (_rayStartInset + _wallCheckDistance);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(start, start + ray);
        Gizmos.DrawLine(start + Vector3.up * _upperCheckHeight, start + Vector3.up * _upperCheckHeight + ray);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + (Vector3)HandOffset(side), 0.06f);
    }

    private void OnDisable() { ResetState(); }
    private void OnDestroy()
    {
        ResetState();
        if (_airMaterial == null) return;
        if (Application.isPlaying) Destroy(_airMaterial);
        else DestroyImmediate(_airMaterial);
    }
}
