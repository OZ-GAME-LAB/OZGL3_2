using System.Collections.Generic;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

/// <summary>로컬 플레이어의 지역을 선택하고 화면 효과를 전환합니다. Parallax와 독립적입니다.</summary>
[DisallowMultipleComponent]
public sealed class EnvironmentPresentationController : MonoBehaviour
{
    private sealed class EffectState
    {
        public EnvironmentScreenEffect Instance;
        public float Current;
        public float Start;
        public float Target;
        public float Elapsed;
        public float Duration;
        public float FadeOutDuration;
    }

    [Header("지역 — 비워 두면 초기화 시 맵에서 한 번 수집")]
    [Tooltip("동일 우선순위에서 현재 지역이 없으면 배열의 앞쪽을 선택합니다.")]
    [SerializeField] private EnvironmentPresentationArea[] _areas;
    [SerializeField] private EnvironmentPresentationProfile _defaultProfile;
    [SerializeField, Min(0.01f)] private float _areaCheckInterval = 0.1f;

    private readonly Dictionary<EnvironmentScreenEffect, EffectState> _effects =
        new Dictionary<EnvironmentScreenEffect, EffectState>();
    private readonly HashSet<EnvironmentScreenEffect> _requestedEffects =
        new HashSet<EnvironmentScreenEffect>();
    private Transform _target;
    private NetworkObject _networkOwner;
    private Camera _outputCamera;
    private CinemachineBrain _cameraBrain;
    private Transform _effectRoot;
    private EnvironmentPresentationArea _currentArea;
    private EnvironmentPresentationProfile _currentProfile;
    private float _checkRemaining;
    private bool _initialized;
    private bool _hasSelection;

    public EnvironmentPresentationArea CurrentArea => _currentArea;
    public EnvironmentPresentationProfile CurrentProfile => _currentProfile;

    public bool Initialize(Transform target, Camera outputCamera, Transform mapRoot)
    {
        if (target == null || outputCamera == null || mapRoot == null)
        {
            Debug.LogWarning("환경 연출에 로컬 플레이어, 출력 Camera, 맵 Root를 연결해 주세요.", this);
            return false;
        }

        ReleaseEffects();
        _target = target;
        _networkOwner = target.GetComponentInParent<NetworkObject>();
        _outputCamera = outputCamera;
        _cameraBrain = outputCamera.GetComponent<CinemachineBrain>();
        if (_areas == null || _areas.Length == 0)
            _areas = mapRoot.GetComponentsInChildren<EnvironmentPresentationArea>(true);

        // 맵의 크기/회전이 화면 효과에 상속되지 않게 독립 루트를 소유합니다.
        _effectRoot = new GameObject("Environment Screen Effects (Runtime)").transform;
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(
            _effectRoot.gameObject, gameObject.scene);
        _initialized = true;
        Physics2D.SyncTransforms();
        if (isActiveAndEnabled)
            StartTracking();
        return true;
    }

    private bool HasLocalTarget()
    {
        return _target != null && _target.gameObject.activeInHierarchy && _outputCamera != null
            && (_networkOwner == null || !_networkOwner.IsSpawned || _networkOwner.IsOwner);
    }

    private void OnEnable()
    {
        if (_initialized)
            StartTracking();
    }

    private void StartTracking()
    {
        CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
        CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);
        _hasSelection = false;
        RefreshArea();
    }

    private void Update()
    {
        if (!_initialized)
            return;
        if (!HasLocalTarget())
        {
            if (_hasSelection)
                ClearPresentation();
            return;
        }

        _checkRemaining -= Time.deltaTime;
        if (!_hasSelection || _checkRemaining <= 0f)
            RefreshArea();

        foreach (EffectState state in _effects.Values)
            UpdateTransition(state, Time.deltaTime);
    }

    // 실행 중 추가한 영역도 명시적으로 연결해 매 프레임 씬 탐색을 피합니다.
    public void RegisterArea(EnvironmentPresentationArea area)
    {
        if (area == null)
            return;
        int count = _areas == null ? 0 : _areas.Length;
        for (int i = 0; i < count; i++)
        {
            if (_areas[i] == area)
                return;
        }
        System.Array.Resize(ref _areas, count + 1);
        _areas[count] = area;
        _checkRemaining = 0f;
    }

    public void RefreshArea()
    {
        if (!_initialized || !isActiveAndEnabled || !HasLocalTarget())
            return;

        _checkRemaining = Mathf.Max(0.01f, _areaCheckInterval);
        EnvironmentPresentationArea selected = SelectArea();
        EnvironmentPresentationProfile profile = selected != null ? selected.Profile : _defaultProfile;
        _currentArea = selected;
        if (_hasSelection && _currentProfile == profile)
            return;

        _currentProfile = profile;
        _hasSelection = true;
        ApplyProfile(profile);
    }

    private EnvironmentPresentationArea SelectArea()
    {
        Vector2 position = _target.position;
        EnvironmentPresentationArea selected =
            _currentArea != null && _currentArea.Contains(position) ? _currentArea : null;
        for (int i = 0; i < _areas.Length; i++)
        {
            EnvironmentPresentationArea area = _areas[i];
            if (area != null && area.Contains(position)
                && (selected == null || area.Priority > selected.Priority))
                selected = area;
        }
        return selected;
    }

    private void ApplyProfile(EnvironmentPresentationProfile profile)
    {
        _requestedEffects.Clear();
        if (profile != null)
        {
            for (int i = 0; i < profile.EffectCount; i++)
            {
                EnvironmentPresentationProfile.EffectEntry entry = profile.GetEffect(i);
                if (entry == null || entry.Prefab == null)
                    continue;
                if (!_requestedEffects.Add(entry.Prefab))
                {
                    Debug.LogWarning("프로필에 같은 효과가 중복되어 첫 번째 설정만 사용합니다.", profile);
                    continue;
                }
                EffectState state = GetOrCreateEffect(entry.Prefab);
                if (state == null)
                    continue;
                state.FadeOutDuration = entry.FadeOutDuration;
                SetTarget(state, entry.Intensity,
                    entry.Intensity >= state.Current ? entry.FadeInDuration : entry.FadeOutDuration);
            }
        }
        foreach (KeyValuePair<EnvironmentScreenEffect, EffectState> effect in _effects)
        {
            if (!_requestedEffects.Contains(effect.Key))
                SetTarget(effect.Value, 0f, effect.Value.FadeOutDuration);
        }
    }

    private EffectState GetOrCreateEffect(EnvironmentScreenEffect prefab)
    {
        if (_effects.TryGetValue(prefab, out EffectState state))
            return state;
        // 비활성 부모 아래에서 준비해 첫 프레임에 최대 밝기로 보이는 것을 막습니다.
        GameObject staging = new GameObject("Preparing Effect");
        staging.transform.SetParent(_effectRoot, false);
        staging.SetActive(false);
        EnvironmentScreenEffect instance = Instantiate(prefab, staging.transform);
        instance.gameObject.SetActive(false);
        if (!instance.Initialize(_outputCamera))
        {
            Debug.LogWarning("환경 효과의 카메라 및 이미지 연결을 확인해 주세요.", prefab);
            Destroy(staging);
            return null;
        }
        instance.ApplyIntensity(0f);
        instance.transform.SetParent(_effectRoot, false);
        Destroy(staging);
        state = new EffectState { Instance = instance };
        _effects.Add(prefab, state);
        return state;
    }

    private void SetTarget(EffectState state, float target, float duration)
    {
        if (Mathf.Approximately(state.Target, target))
            return;
        state.Start = state.Current;
        state.Target = target;
        state.Elapsed = 0f;
        state.Duration = duration;
        state.Instance.SetTargetIntensity(target);
        if (target > 0f && !state.Instance.gameObject.activeSelf)
        {
            state.Instance.RefreshCamera();
            state.Instance.gameObject.SetActive(true);
        }
        if (duration <= 0f)
            UpdateTransition(state, 0f);
    }

    private void UpdateTransition(EffectState state, float deltaTime)
    {
        if (state.Instance == null || !state.Instance.gameObject.activeSelf)
            return;
        state.Elapsed += deltaTime;
        float progress = state.Duration <= 0f ? 1f : Mathf.Clamp01(state.Elapsed / state.Duration);
        state.Current = Mathf.Lerp(state.Start, state.Target, Mathf.SmoothStep(0f, 1f, progress));
        state.Instance.ApplyIntensity(state.Current);
        if (progress >= 1f && state.Target <= 0f)
        {
            state.Instance.ResetEffect();
            state.Instance.gameObject.SetActive(false);
        }
    }

    /// <summary>순간이동 완료 후 호출합니다. 기존 효과를 지우고 도착지 효과를 다시 등장시킵니다.</summary>
    public void NotifyTargetTeleported()
    {
        if (!_initialized)
            return;
        ClearPresentation();
        Physics2D.SyncTransforms();
        RefreshArea();
    }

    private void OnCameraUpdated(CinemachineBrain brain)
    {
        if (brain == _cameraBrain && brain != null && brain.OutputCamera == _outputCamera)
            RefreshEffectsCamera();
    }

    private void LateUpdate()
    {
        if (_initialized && (_cameraBrain == null || !_cameraBrain.isActiveAndEnabled))
            RefreshEffectsCamera();
    }

    private void RefreshEffectsCamera()
    {
        foreach (EffectState state in _effects.Values)
        {
            if (state.Instance != null && state.Instance.gameObject.activeSelf)
                state.Instance.RefreshCamera();
        }
    }

    private void ClearPresentation()
    {
        foreach (EffectState state in _effects.Values)
        {
            if (state.Instance != null)
            {
                state.Instance.ResetEffect();
                state.Instance.gameObject.SetActive(false);
            }
            state.Current = state.Start = state.Target = state.Elapsed = state.Duration = 0f;
        }
        _currentArea = null;
        _currentProfile = null;
        _hasSelection = false;
    }

    private void OnDisable()
    {
        CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
        ClearPresentation();
    }

    private void OnDestroy()
    {
        ReleaseEffects();
    }

    private void ReleaseEffects()
    {
        CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
        ClearPresentation();
        _effects.Clear();
        _initialized = false;
        if (_effectRoot != null)
            Destroy(_effectRoot.gameObject);
    }
}
