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

    private CinemachineConfiner2D _confiner;
    private CameraBoundsArea _currentArea;
    private bool _hasLens;
    private float _lastOrthographicSize;
    private float _lastFieldOfView;
    private float _lastAspect;

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

        if (_confiner == null && !TryGetComponent(out _confiner))
            return;

        Transform target = _target != null ? _target : vcam.Follow;
        if (target == null || (_networkOwner != null && _networkOwner.IsSpawned && !_networkOwner.IsOwner))
        {
            ApplyArea(null);
            return;
        }

        CameraBoundsArea next = CameraBoundsArea.SelectArea(target.position, _currentArea);
        if (next == null && _keepLastBoundsOutside && _currentArea != null && _currentArea.IsAvailable)
            next = _currentArea;

        ApplyArea(next);
        RefreshLens(curState.Lens);
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
        ApplyArea(null);
        _hasLens = false;
    }
}
