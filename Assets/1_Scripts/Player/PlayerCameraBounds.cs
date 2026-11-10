using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

/// <summary>추적 대상이 속한 영역을 골라 같은 카메라의 Confiner에 전달합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CinemachineCamera), typeof(CinemachineConfiner2D))]
public class PlayerCameraBounds : CinemachineExtension
{
    [Header("대상 연결")]
    [Tooltip("비워 두면 Cinemachine 카메라의 Follow 대상을 사용합니다.")]
    [SerializeField] private Transform _target;
    [Tooltip("멀티플레이에서는 해당 플레이어의 NetworkObject를 연결합니다. 단일 플레이에서는 생략 가능합니다.")]
    [SerializeField] private NetworkObject _networkOwner;

    [Header("영역 밖 동작")]
    [SerializeField] private bool _keepLastBoundsOutside = true;

    [Header("영역 전환 연출")]
    [Tooltip("새 영역의 카메라 위치로 부드럽게 전환하는 시간입니다. 0이면 즉시 전환합니다.")]
    [Min(0f)]
    [SerializeField] private float _transitionDuration = 0.45f;

    private CinemachineConfiner2D _confiner;
    private CameraBoundsArea _currentArea;
    private bool _hasLens;
    private float _lastOrthographicSize;
    private float _lastFieldOfView;
    private float _lastAspect;
    private Transform _lastTarget;
    private bool _hasPreviousPosition;
    private bool _transitionPending;
    private bool _isTransitioning;
    private bool _canUpdateTransition;
    private Vector3 _previousPosition;
    private Vector3 _transitionOffset;
    private float _transitionElapsed;

    protected override void Awake()
    {
        base.Awake();
        TryGetComponent(out _confiner);
    }

    // Cinemachine 계산 직전에 실행하므로 Fixed/Smart/Late Update 설정과 무관하게
    // 이번 화면에 사용할 영역을 먼저 선택합니다. Trigger 진입 이벤트가 필요하지 않습니다.
    public override void PrePipelineMutateCameraStateCallback(
        CinemachineVirtualCameraBase vcam, ref CameraState curState, float deltaTime)
    {
        if (!Application.isPlaying || vcam != ComponentOwner)
            return;

        _canUpdateTransition = false;
        if (_confiner == null && !TryGetComponent(out _confiner))
        {
            ResetTransition();
            return;
        }

        Transform target = _target != null ? _target : vcam.Follow;
        if (target == null || (_networkOwner != null && _networkOwner.IsSpawned && !_networkOwner.IsOwner))
        {
            ResetTransition();
            _lastTarget = null;
            ApplyArea(null);
            return;
        }

        if (target != _lastTarget || deltaTime < 0f || !vcam.PreviousStateIsValid)
            ResetTransition();
        _lastTarget = target;
        _canUpdateTransition = true;

        CameraBoundsArea next = CameraBoundsArea.SelectArea(target.position, _currentArea);
        if (next == null && _keepLastBoundsOutside && _currentArea != null && _currentArea.IsAvailable)
            next = _currentArea;

        if (next != _currentArea && _hasPreviousPosition)
            _transitionPending = true;

        ApplyArea(next);
        RefreshLens(curState.Lens);
    }

    // Confiner의 Body 단계 이후에 보정하여 전환 중 새 경계로 즉시 밀려나지 않게 합니다.
    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage,
        ref CameraState state, float deltaTime)
    {
        if (!Application.isPlaying || vcam != ComponentOwner
            || stage != CinemachineCore.Stage.Finalize || !_canUpdateTransition)
            return;

        Vector3 targetPosition = state.GetCorrectedPosition();
        if (deltaTime < 0f || !vcam.PreviousStateIsValid || _transitionDuration <= 0f)
            ResetTransition();

        if (_transitionPending)
        {
            // 전환 중 다시 영역이 바뀌어도 직전 화면 위치에서 이어갑니다.
            _transitionOffset = _previousPosition - targetPosition;
            _transitionElapsed = 0f;
            _transitionPending = false;
            _isTransitioning = true;
        }
        else if (_isTransitioning)
        {
            _transitionElapsed += Mathf.Max(0f, deltaTime);
        }

        if (_isTransitioning)
        {
            float progress = Mathf.Clamp01(_transitionElapsed / _transitionDuration);
            float weight = 1f - Mathf.SmoothStep(0f, 1f, progress);
            state.PositionCorrection += _transitionOffset * weight;
            if (progress >= 1f)
                _isTransitioning = false;
        }

        _previousPosition = state.GetCorrectedPosition();
        _hasPreviousPosition = true;
    }

    private void ResetTransition()
    {
        _hasPreviousPosition = false;
        _transitionPending = false;
        _isTransitioning = false;
        _transitionOffset = Vector3.zero;
        _transitionElapsed = 0f;
    }

    public override void OnTargetObjectWarped(
        CinemachineVirtualCameraBase vcam, Transform target, Vector3 positionDelta)
    {
        base.OnTargetObjectWarped(vcam, target, positionDelta);
        if (vcam == ComponentOwner && target == _lastTarget)
            ResetTransition();
    }

    private void ApplyArea(CameraBoundsArea area)
    {
        Collider2D shape = area != null ? area.BoundsCollider : null;
        _currentArea = area;
        if (_confiner == null || _confiner.BoundingShape2D == shape)
            return;

        _confiner.BoundingShape2D = shape;
        _confiner.InvalidateBoundingShapeCache();
    }

    private void RefreshLens(LensSettings lens)
    {
        if (!_hasLens || !Mathf.Approximately(_lastOrthographicSize, lens.OrthographicSize)
            || !Mathf.Approximately(_lastFieldOfView, lens.FieldOfView)
            || !Mathf.Approximately(_lastAspect, lens.Aspect))
        {
            _confiner.InvalidateLensCache();
            _lastOrthographicSize = lens.OrthographicSize;
            _lastFieldOfView = lens.FieldOfView;
            _lastAspect = lens.Aspect;
            _hasLens = true;
        }
    }

    private void OnDisable()
    {
        ResetTransition();
        _canUpdateTransition = false;
        _lastTarget = null;
        ApplyArea(null);
        _hasLens = false;
    }
}
