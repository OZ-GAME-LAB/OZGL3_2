using System;
using UnityEngine;

// 카메라 추적은 부모 ParallaxBackground, 바람에 의한 이동은 이 컴포넌트가 담당한다.
public sealed class ParallaxCloudLoop : MonoBehaviour
{
    [Serializable]
    private sealed class CloudGroup
    {
        public Transform Root;
        public SpriteRenderer Band;
        public SpriteRenderer Scattered;
        [NonSerialized] public Vector3 RootPosition;
        [NonSerialized] public Vector3 BandPosition;
        [NonSerialized] public Vector3 ScatteredPosition;
    }

    [Header("반복할 구름 두 묶음")]
    [SerializeField] private CloudGroup[] _groups;

    [Header("가로 이동 (월드 단위)")]
    [SerializeField, Min(0f)] private float _horizontalSpeed = 0.35f;
    [SerializeField] private float _firstGroupStartX = -12f;
    [SerializeField, Min(0f)] private float _offscreenPadding = 2f;

    [Header("작은 상하 움직임")]
    [SerializeField, Min(0f)] private float _bandVerticalRange = 0.18f;
    [SerializeField, Min(0f)] private float _scatteredVerticalRange = 0.28f;
    [SerializeField, Min(0.1f)] private float _verticalCycleDuration = 20f;
    [SerializeField] private bool _useUnscaledTime;

    private Camera _outputCamera;
    private float _groupHalfWidth;
    private float _travel;
    private float _elapsedTime;
    private bool _hasInitialPositions;
    private bool _isInitialized;

    public bool Initialize(Camera outputCamera)
    {
        _isInitialized = false;
        if (outputCamera == null || !outputCamera.orthographic ||
            _groups == null || _groups.Length != 2)
        {
            Debug.LogWarning("구름 루프에 직교 Camera와 두 구름 묶음이 필요합니다.", this);
            return false;
        }

        for (int i = 0; i < _groups.Length; i++)
        {
            CloudGroup group = _groups[i];
            if (group == null || group.Root == null || group.Band == null ||
                group.Scattered == null || group.Band.sprite == null || group.Scattered.sprite == null ||
                group.Root.parent != transform || group.Band.transform.parent != group.Root ||
                group.Scattered.transform.parent != group.Root)
            {
                Debug.LogWarning("구름 Root 및 그 자식 Band / Scattered 연결을 확인해 주세요.", this);
                return false;
            }
        }

        if (_groups[0].Root == _groups[1].Root)
        {
            Debug.LogWarning("반복 구름은 서로 다른 두 Root를 연결해야 합니다.", this);
            return false;
        }

        _outputCamera = outputCamera;
        _groupHalfWidth = 0f;
        for (int i = 0; i < _groups.Length; i++)
        {
            CloudGroup group = _groups[i];
            if (!_hasInitialPositions)
            {
                group.RootPosition = group.Root.localPosition;
                group.BandPosition = group.Band.transform.localPosition;
                group.ScatteredPosition = group.Scattered.transform.localPosition;
            }

            // 투명 여백까지 포함한 전체 스프라이트 폭으로 화면 밖 반환 지점을 잡는다.
            IncludeWidth(group, group.Band);
            IncludeWidth(group, group.Scattered);
        }

        _hasInitialPositions = true;
        _travel = 0f;
        _elapsedTime = 0f;
        _isInitialized = true;
        RefreshClouds(0f);
        return true;
    }

    private void IncludeWidth(CloudGroup group, SpriteRenderer sprite)
    {
        Bounds bounds = sprite.bounds;
        float left = transform.InverseTransformPoint(bounds.min).x - group.Root.localPosition.x;
        float right = transform.InverseTransformPoint(bounds.max).x - group.Root.localPosition.x;
        _groupHalfWidth = Mathf.Max(_groupHalfWidth, Mathf.Max(Mathf.Abs(left), Mathf.Abs(right)));
    }

    private void Update()
    {
        if (!_isInitialized || _outputCamera == null)
            return;

        RefreshClouds(_useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
    }

    private void RefreshClouds(float deltaTime)
    {
        float rootScaleX = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
        float viewHalfWidth = _outputCamera.orthographicSize * _outputCamera.aspect / rootScaleX;
        float edge = viewHalfWidth + _groupHalfWidth + Mathf.Max(0f, _offscreenPadding);
        float loopWidth = edge * 2f;
        _travel = Mathf.Repeat(_travel + Mathf.Max(0f, _horizontalSpeed) * deltaTime / rootScaleX, loopWidth);
        float duration = Mathf.Max(0.1f, _verticalCycleDuration);
        _elapsedTime = Mathf.Repeat(_elapsedTime + deltaTime, duration);

        for (int i = 0; i < _groups.Length; i++)
        {
            CloudGroup group = _groups[i];
            Vector3 position = group.RootPosition;
            // 두 묶음은 반 주기 간격. 큰 프레임 지연에도 초과 이동량을 보존한다.
            position.x = Mathf.Repeat(_firstGroupStartX + _travel + i * loopWidth * 0.5f + edge, loopWidth) - edge;
            group.Root.localPosition = position;

            float wave = Mathf.Sin(_elapsedTime / duration * Mathf.PI * 2f + i * Mathf.PI);
            Vector3 bandPosition = group.BandPosition;
            Vector3 scatteredPosition = group.ScatteredPosition;
            bandPosition.y += wave * _bandVerticalRange;
            scatteredPosition.y -= wave * _scatteredVerticalRange;
            group.Band.transform.localPosition = bandPosition;
            group.Scattered.transform.localPosition = scatteredPosition;
        }
    }
}
