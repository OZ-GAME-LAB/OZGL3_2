using System;
using Unity.Cinemachine;
using UnityEngine;

// 지역의 고정 기준점과 실제 출력 카메라의 차이로 배경 위치를 계산한다.
public sealed class ParallaxBackground : MonoBehaviour
{
    [Serializable]
    private sealed class BackgroundLayer
    {
        public Transform Root;
        public Vector2 Offset;
        [Tooltip("0은 화면에 고정, 1은 월드에 고정. 가로/세로 화면 이동 비율입니다.")]
        public Vector2 ScreenMotion = new Vector2(0.1f, 0.1f);
        [Tooltip("화면 안 이동량을 제한합니다. 0인 축은 제한하지 않습니다.")]
        public Vector2 MaxScreenTravel;
        [Range(0.5f, 1f)] public float HighAltitudeScale = 1f;
        [Min(1f)] public float ScaleHeight = 75f;
        [Tooltip("축소할 때 유지할 기준점. 레이어 중심으로부터의 월드 단위 거리입니다.")]
        public Vector2 ScaleAnchor;
        [NonSerialized] public Vector3 InitialScale;
        [NonSerialized] public bool HasInitialScale;
    }

    [Header("로컬 출력 카메라")]
    [SerializeField] private Camera _outputCamera;
    [SerializeField] private CinemachineBrain _cameraBrain;

    [Header("이 카메라 위치에서 기본 구도 표시")]
    [SerializeField] private Vector2 _referenceCameraPosition;
    [SerializeField, Min(1f)] private float _distanceFromCamera = 100f;

    [Header("거리별 레이어")]
    [SerializeField] private BackgroundLayer[] _layers;

    [Header("화면 전체를 채우는 바탕")]
    [SerializeField] private SpriteRenderer _backdrop;

    [Header("구름 반복 이동 (선택)")]
    [SerializeField] private ParallaxCloudLoop _cloudLoop;

    private bool _isInitialized;

    private void OnEnable()
    {
        // 맵 생성 직후에는 GameManager의 카메라 연결을 기다린다.
        if (!_isInitialized)
            return;

        StartTracking();
    }

    public bool Initialize(Camera outputCamera)
    {
        CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
        _isInitialized = false;
        _outputCamera = outputCamera;
        _cameraBrain = null;

        if (_outputCamera != null)
            _cameraBrain = _outputCamera.GetComponent<CinemachineBrain>();

        if (!ValidateReferences())
            return false;

        if (_cloudLoop != null && !_cloudLoop.Initialize(outputCamera))
            return false;

        _isInitialized = true;

        if (isActiveAndEnabled)
            StartTracking();

        return true;
    }

    private void StartTracking()
    {
        CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
        CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);
        RefreshBackground();
    }

    private void OnDisable()
    {
        CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
    }

    private bool ValidateReferences()
    {
        if (_outputCamera == null || !_outputCamera.orthographic ||
            _backdrop == null || _backdrop.sprite == null || _layers == null)
        {
            Debug.LogWarning("원경에 직교 Camera, 바탕 SpriteRenderer, 레이어를 연결해 주세요.", this);
            return false;
        }

        for (int i = 0; i < _layers.Length; i++)
        {
            if (_layers[i] == null || _layers[i].Root == null)
            {
                Debug.LogWarning("원경 레이어의 Root 연결을 확인해 주세요.", this);
                return false;
            }
        }
        return true;
    }

    private void OnCameraUpdated(CinemachineBrain brain)
    {
        // OutputCamera는 Brain의 Awake 이후 준비되므로 초기 검사 대신 갱신 시점에 확인한다.
        // 다른 카메라나 다른 플레이어의 Brain 갱신에는 반응하지 않는다.
        if (brain != null && brain == _cameraBrain && brain.OutputCamera == _outputCamera)
            RefreshBackground();
    }

    private void LateUpdate()
    {
        if (!_isInitialized)
            return;

        // Cinemachine이 없는 독립 카메라로도 같은 배경을 시험할 수 있다.
        if (_cameraBrain == null || !_cameraBrain.isActiveAndEnabled)
            RefreshBackground();
    }

    [ContextMenu("현재 카메라 위치로 원경 갱신")]
    public void RefreshBackground()
    {
        if (_outputCamera == null || _layers == null)
            return;

        Vector3 cameraPosition = _outputCamera.transform.position;
        Vector2 delta = (Vector2)cameraPosition - _referenceCameraPosition;
        float depth = Mathf.Clamp(_distanceFromCamera,
            _outputCamera.nearClipPlane + 0.1f, _outputCamera.farClipPlane - 0.1f);

        for (int i = 0; i < _layers.Length; i++)
        {
            BackgroundLayer layer = _layers[i];
            if (layer == null || layer.Root == null)
                continue;

            if (!layer.HasInitialScale)
            {
                layer.InitialScale = layer.Root.localScale;
                layer.HasInitialScale = true;
            }

            // 화면 안에서 움직이는 양을 제한하고, 나머지는 카메라를 따라간다.
            Vector2 travel = Vector2.Scale(delta, layer.ScreenMotion);
            if (layer.MaxScreenTravel.x > 0f)
                travel.x = Mathf.Clamp(travel.x, -layer.MaxScreenTravel.x, layer.MaxScreenTravel.x);
            if (layer.MaxScreenTravel.y > 0f)
                travel.y = Mathf.Clamp(travel.y, -layer.MaxScreenTravel.y, layer.MaxScreenTravel.y);

            float altitude = Mathf.Clamp01(delta.y / Mathf.Max(1f, layer.ScaleHeight));
            float scale = Mathf.Lerp(1f, layer.HighAltitudeScale, Mathf.SmoothStep(0f, 1f, altitude));
            layer.Root.localScale = layer.InitialScale * scale;
            // 매 프레임 고정 기준에서 계산하므로 하강하면 원래 구도로 돌아온다.
            Vector2 position = (Vector2)cameraPosition + layer.Offset - travel
                + layer.ScaleAnchor * (1f - scale);
            layer.Root.position = new Vector3(position.x, position.y, cameraPosition.z + depth);
        }

        if (_backdrop == null || _backdrop.sprite == null)
            return;

        _backdrop.transform.position = new Vector3(cameraPosition.x, cameraPosition.y, cameraPosition.z + depth);
        Vector2 spriteSize = _backdrop.sprite.bounds.size;
        float height = _outputCamera.orthographicSize * 2f;
        if (spriteSize.x > 0f && spriteSize.y > 0f)
        {
            _backdrop.transform.localScale = new Vector3(
                height * _outputCamera.aspect * 1.02f / spriteSize.x,
                height * 1.02f / spriteSize.y, 1f);
        }
    }
}
