using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>고정된 마스크 구역 안에서 자식 장식 묶음만 카메라에 맞춰 움직입니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SortingGroup))]
public sealed class ParallaxSceneryGroup : MonoBehaviour
{
    [Header("고정 구역과 움직이는 장식 — 둘 다 이 오브젝트의 직접 자식")]
    [SerializeField] private SpriteMask _areaMask;
    [SerializeField] private Transform _sceneryRoot;

    [Header("배치한 구도가 표시될 카메라 월드 좌표")]
    [SerializeField] private Vector2 _referenceCameraPosition;

    [Header("화면 안 이동 비율")]
    [Tooltip("0: 화면에 고정, 1: 월드에 고정, 1보다 큰 값: 근경. X/Y를 따로 조절합니다.")]
    [SerializeField] private Vector2 _screenMotion = new Vector2(0.2f, 0.2f);

    [Header("플레이어 진입 시 표시")]
    [InspectorName("플레이어가 구역 안에 있을 때만 표시")]
    [Tooltip("플레이어 Transform의 위치가 Area Mask의 사각 범위 안에 있을 때만 Scenery를 활성화합니다. 투명 픽셀 모양과 Z좌표는 검사하지 않습니다.")]
    [SerializeField] private bool _showOnlyWhenPlayerInside;

    private Camera _outputCamera;
    private Transform _player;
    private CinemachineBrain _cameraBrain;
    private Vector3 _initialLocalPosition;
    private Transform _capturedRoot;
    private bool _isInitialized;
    private bool _initialSceneryActive;
    private bool _isVisibilityControlled;

    public bool Initialize(Camera outputCamera, Transform player = null)
    {
        RestoreVisibility();
        CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
        _isInitialized = false;
        _outputCamera = outputCamera;
        _player = player;
        _cameraBrain = null;

        if (_outputCamera == null || !_outputCamera.orthographic)
        {
            Debug.LogWarning("장식 Parallax에 직교 Camera를 연결해 주세요.", this);
            return false;
        }

        if (!ValidateArea())
            return false;

        SpriteRenderer[] sprites = _sceneryRoot.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < sprites.Length; i++)
            sprites[i].maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

        // 구역의 SortingGroup 안에서만 마스크를 적용합니다.
        _areaMask.isCustomRangeActive = false;

        // 재초기화해도 이미 움직인 위치를 새 기준으로 사용하지 않습니다.
        if (_capturedRoot != _sceneryRoot)
        {
            _initialLocalPosition = _sceneryRoot.localPosition;
            _initialSceneryActive = _sceneryRoot.gameObject.activeSelf;
            _capturedRoot = _sceneryRoot;
        }

        if (_showOnlyWhenPlayerInside && _player == null)
            Debug.LogWarning("구역 진입 표시용 플레이어가 없습니다. 플레이어가 연결될 때까지 장식을 숨깁니다.", this);

        _cameraBrain = _outputCamera.GetComponent<CinemachineBrain>();
        _isInitialized = true;
        if (isActiveAndEnabled)
            StartTracking();
        return true;
    }

    private bool ValidateArea()
    {
        if (_areaMask == null || _areaMask.sprite == null || _sceneryRoot == null ||
            _areaMask.transform.parent != transform || _sceneryRoot.parent != transform ||
            _areaMask.transform == _sceneryRoot)
        {
            Debug.LogWarning("구역 아래에 Sprite가 있는 SpriteMask와 별도의 장식 부모를 나란히 연결해 주세요.", this);
            return false;
        }

        SortingGroup sortingGroup = GetComponent<SortingGroup>();
        if (sortingGroup == null || !sortingGroup.enabled)
        {
            Debug.LogWarning("구역 마스크를 분리하려면 이 오브젝트의 SortingGroup을 켜 주세요.", this);
            return false;
        }

        // 중첩 그룹/마스크는 이 구역의 표시 범위와 다른 범위를 만들 수 있습니다.
        if (_sceneryRoot.GetComponentsInChildren<SortingGroup>(true).Length > 0 ||
            _sceneryRoot.GetComponentsInChildren<SpriteMask>(true).Length > 0 ||
            _areaMask.GetComponent<SortingGroup>() != null ||
            GetComponentsInChildren<SpriteMask>(true).Length != 1)
        {
            Debug.LogWarning("장식 안의 SortingGroup/마스크를 제거하고 구역마다 마스크 하나만 사용해 주세요.", this);
            return false;
        }

        return true;
    }

    private void OnEnable()
    {
        if (_isInitialized)
            StartTracking();
    }

    private void StartTracking()
    {
        CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
        CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);
        RefreshPosition();
        RefreshVisibility();
    }

    private void OnDisable()
    {
        CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
        RestoreVisibility();
    }

    private void OnCameraUpdated(CinemachineBrain brain)
    {
        if (brain != null && brain == _cameraBrain && brain.OutputCamera == _outputCamera)
            RefreshPosition();
    }

    private void LateUpdate()
    {
        if (!_isInitialized)
            return;

        // 카메라가 정지하거나 Cinemachine 갱신이 없어도 플레이어의 출입은 검사합니다.
        RefreshVisibility();
        if (_isInitialized && (_cameraBrain == null || !_cameraBrain.isActiveAndEnabled))
            RefreshPosition();
    }

    private void RefreshVisibility()
    {
        if (_sceneryRoot == null)
            return;

        if (!_showOnlyWhenPlayerInside)
        {
            RestoreVisibility();
            return;
        }

        _isVisibilityControlled = true;
        bool visible = IsPlayerInsideArea();
        if (_sceneryRoot.gameObject.activeSelf != visible)
            _sceneryRoot.gameObject.SetActive(visible);
    }

    private bool IsPlayerInsideArea()
    {
        if (_player == null || _areaMask == null || _areaMask.sprite == null)
            return false;

        Vector3 localPosition = _areaMask.transform.InverseTransformPoint(_player.position);
        Bounds bounds = _areaMask.sprite.bounds;
        return localPosition.x >= bounds.min.x && localPosition.x <= bounds.max.x &&
            localPosition.y >= bounds.min.y && localPosition.y <= bounds.max.y;
    }

    private void RestoreVisibility()
    {
        if (!_isVisibilityControlled)
            return;

        if (_capturedRoot != null && _capturedRoot.gameObject.activeSelf != _initialSceneryActive)
            _capturedRoot.gameObject.SetActive(_initialSceneryActive);
        _isVisibilityControlled = false;
    }

    private void RefreshPosition()
    {
        if (!_isInitialized || _outputCamera == null || _sceneryRoot == null)
            return;

        Vector2 delta = (Vector2)_outputCamera.transform.position - _referenceCameraPosition;
        Vector3 displacement = new Vector3(
            delta.x * (1f - _screenMotion.x),
            delta.y * (1f - _screenMotion.y), 0f);
        // 구역과 마스크는 고정하고 장식만 이동합니다. 이동량은 월드 단위입니다.
        _sceneryRoot.localPosition = _initialLocalPosition + transform.InverseTransformVector(displacement);
    }
}
