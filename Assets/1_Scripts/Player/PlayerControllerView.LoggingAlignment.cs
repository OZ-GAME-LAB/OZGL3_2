using UnityEngine;

public partial class PlayerControllerView
{
    [Header("벌목 위치 보정")]
    [Tooltip("logging_05 도끼날 끝: (741.5,306.5), Pivot (350,32), PPU 97.94699574. 스프라이트 로컬 좌표.")]
    [SerializeField] private Vector2 _loggingAxeTip = new Vector2(3.9970598f, 2.8025362f);
    [SerializeField, Min(0.1f)] private float _loggingAlignmentSpeed = 8f;
    [SerializeField, Min(0f)] private float _loggingAlignmentMaxDistance = 2.2f;
    [SerializeField, Min(0.01f)] private float _loggingAlignmentTolerance = 0.04f;
    [SerializeField, Min(0.01f)] private float _loggingHitHeightTolerance = 0.3f;
    private ulong _loggingAlignmentRequest;
    private Vector3 _loggingAlignmentTarget;
    private float _loggingAlignmentElapsed;
    private readonly RaycastHit2D[] _loggingPathHits = new RaycastHit2D[8];

    public float LoggingAlignmentTolerance => _loggingAlignmentTolerance;

    public bool TryCalculateLoggingPosition(Vector3 hitPoint, int facing, out Vector3 destination)
    {
        destination = transform.position;
        if (_animator == null || Mathf.Abs(facing) != 1 || !IsGroundedForAttack() ||
            _isCrouching || _jumpInProgress || IsHitControlLocked || _isDialogueMode) return false;
        return TryCalculateLoggingGeometry(hitPoint, facing, out destination);
    }

    private bool TryCalculateLoggingGeometry(Vector3 hitPoint, int facing, out Vector3 destination)
    {
        destination = transform.position;
        Vector3 tip = _animator.transform.TransformPoint(_loggingAxeTip);
        float reach = Mathf.Abs(tip.x - transform.position.x);
        if (float.IsNaN(reach) || float.IsInfinity(reach) || reach <= 0f ||
            Mathf.Abs(tip.y - hitPoint.y) > _loggingHitHeightTolerance) return false;
        destination.x = hitPoint.x - facing * reach;
        return !float.IsNaN(destination.x) && !float.IsInfinity(destination.x) &&
            Mathf.Abs(destination.x - transform.position.x) <= _loggingAlignmentMaxDistance;
    }

    public bool BeginLoggingAlignment(ulong requestId, Vector3 destination)
    {
        if (requestId == 0 || _loggingAlignmentRequest != 0 || _networkObject == null ||
            !_networkObject.IsOwner || _rigidbody2D == null || _bodyCollider == null ||
            _groundCheck == null || _isCrouching || _jumpInProgress ||
            !IsGroundedForAttack() || _isDialogueMode || IsHitControlLocked ||
            float.IsNaN(destination.x) || float.IsInfinity(destination.x) ||
            Mathf.Abs(destination.x - transform.position.x) > _loggingAlignmentMaxDistance + 0.1f)
            return false;
        EnterMiniGameMode();
        _loggingAlignmentRequest = requestId;
        _loggingAlignmentTarget = destination;
        _loggingAlignmentElapsed = 0f;
        return true;
    }

    public void StopLoggingAlignment()
    {
        if (_loggingAlignmentRequest == 0) return;
        _loggingAlignmentRequest = 0;
        if (_rigidbody2D != null && !IsHitControlLocked)
        {
            Vector2 velocity = _rigidbody2D.linearVelocity;
            velocity.x = 0f;
            _rigidbody2D.linearVelocity = velocity;
        }
    }

    private void FinishLoggingAlignment(bool success)
    {
        ulong requestId = _loggingAlignmentRequest;
        StopLoggingAlignment();
        _interactionController?.OnLoggingAlignmentFinished(requestId, success);
    }

    private void UpdateLoggingAlignment()
    {
        _loggingAlignmentElapsed += Time.fixedDeltaTime;
        if (_loggingAlignmentElapsed > 0.75f || !IsGroundedForAttack() ||
            Mathf.Abs(transform.position.y - _loggingAlignmentTarget.y) > _loggingHitHeightTolerance)
        {
            FinishLoggingAlignment(false);
            return;
        }
        float remaining = _loggingAlignmentTarget.x - transform.position.x;
        if (Mathf.Abs(remaining) <= _loggingAlignmentTolerance)
        {
            FinishLoggingAlignment(true);
            return;
        }
        float step = Mathf.Clamp(remaining, -_loggingAlignmentSpeed * Time.fixedDeltaTime,
            _loggingAlignmentSpeed * Time.fixedDeltaTime);
        if (!IsLoggingStepSafe(step))
        {
            FinishLoggingAlignment(false);
            return;
        }
        SetMoveInput(0f);
        Vector2 velocity = _rigidbody2D.linearVelocity;
        velocity.x = step / Time.fixedDeltaTime;
        _rigidbody2D.linearVelocity = velocity;
        UpdateGroundStateAndAnimation();
    }

    private bool IsLoggingStepSafe(float step)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
        filter.useTriggers = false;
        int count = _bodyCollider.Cast(Vector2.right * Mathf.Sign(step), filter,
            _loggingPathHits, Mathf.Abs(step) + 0.02f);
        if (count == _loggingPathHits.Length) return false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = _loggingPathHits[i];
            if (hit.collider == _bodyCollider || hit.rigidbody == _rigidbody2D) continue;
            if (Vector2.Dot(hit.normal, Vector2.right * Mathf.Sign(step)) < -0.1f) return false;
        }
        filter.SetLayerMask(_groundLayer);
        Vector2 foot = (Vector2)_groundCheck.position + Vector2.right * step;
        count = Physics2D.Raycast(foot + Vector2.up * 0.1f, Vector2.down, filter, _loggingPathHits, 0.25f);
        if (count == _loggingPathHits.Length) return false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = _loggingPathHits[i];
            if (hit.collider != _bodyCollider && hit.rigidbody != _rigidbody2D &&
                !IsIgnoredThinPlatform(hit.collider) && hit.normal.y >= MinimumGroundNormalY) return true;
        }
        return false;
    }
}
