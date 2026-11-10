using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>영역에 배치할 효과별 설정과 인스턴스를 소유합니다. 실제 표현은 각 효과가 담당합니다.</summary>
[DisallowMultipleComponent]
public sealed class EnvironmentEffectArea : MonoBehaviour, ISerializationCallbackReceiver
{
    [Serializable]
    private sealed class EffectEntry
    {
        public EnvironmentScreenEffect Prefab;
        [Tooltip("입자 생성량: 1은 원본, 0.5는 절반, 0은 새 입자 생성 중지. 이미지에는 적용하지 않습니다.")]
        [Min(0f)] public float EmissionMultiplier = 1f;
        [Tooltip("먼지는 밝기, 이미지는 원래 알파에 곱하는 불투명도입니다. 0이면 효과를 끕니다.")]
        [Range(0f, 1f)] public float Intensity = 0.5f;
    }

    private sealed class EffectState
    {
        public EffectEntry Entry;
        public EnvironmentScreenEffect Prefab;
        public EnvironmentScreenEffect Instance;
        public float Intensity = -1f;
        public float EmissionMultiplier = -1f;
    }

    [Header("영역 효과 — 효과마다 생성량과 표시 강도를 설정")]
    [Tooltip("서로 다른 효과를 함께 등록할 수 있습니다. 같은 프리팹을 중복 등록하면 첫 항목만 사용합니다.")]
    [SerializeField] private EffectEntry[] _effects = Array.Empty<EffectEntry>();

    // 기존 프리팹의 연결을 새 목록으로 옮기기 위해 유지합니다.
    [SerializeField, HideInInspector] private EnvironmentScreenEffect[] _effectPrefabs;
    [SerializeField, HideInInspector] private float _intensity = 0.5f;

    private BoxCollider2D[] _boxes;
    private EffectState[] _states;
    private GameObject _effectRoot;
    private Camera _outputCamera;
    private bool _initialized;

    public void OnBeforeSerialize() { }
    public void OnAfterDeserialize() { MigrateLegacySettings(); }

    private void MigrateLegacySettings()
    {
        if (_effectPrefabs == null || _effectPrefabs.Length == 0)
            return;
        if (_effects == null || _effects.Length == 0)
        {
            _effects = new EffectEntry[_effectPrefabs.Length];
            for (int i = 0; i < _effects.Length; i++)
                _effects[i] = new EffectEntry { Prefab = _effectPrefabs[i], Intensity = _intensity };
        }
        _effectPrefabs = Array.Empty<EnvironmentScreenEffect>();
    }

    private void Awake()
    {
        RefreshColliders();
    }

    private void Start()
    {
        if (!_initialized)
            Initialize(Camera.main);
    }

    // 카메라는 시작 시 한 번만 연결합니다. 플레이어나 지역 프로필은 필요하지 않습니다.
    public bool Initialize(Camera outputCamera)
    {
        if (_initialized)
            return true;
        if (outputCamera == null || !outputCamera.orthographic)
        {
            Debug.LogWarning("영역 효과에 Orthographic Main Camera가 필요합니다.", this);
            return false;
        }

        MigrateLegacySettings();
        _outputCamera = outputCamera;
        RefreshColliders();
        _initialized = true;
        RebuildEffects();
        return true;
    }

    private void RebuildEffects()
    {
        ReleaseEffects();
        _states = new EffectState[_effects == null ? 0 : _effects.Length];
        // 영역의 Parallax 이동/확대가 이미 생성된 입자에 전달되지 않도록 독립 루트를 사용합니다.
        _effectRoot = new GameObject(name + " Effects (Runtime)");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_effectRoot, gameObject.scene);
        _effectRoot.SetActive(false);
        for (int i = 0; i < _states.Length; i++)
        {
            EffectEntry entry = _effects[i];
            EffectState state = new EffectState { Entry = entry, Prefab = entry != null ? entry.Prefab : null };
            _states[i] = state;
            if (state.Prefab == null || IsDuplicatePrefab(i))
                continue;
            EnvironmentScreenEffect instance = Instantiate(state.Prefab, _effectRoot.transform);
            instance.gameObject.SetActive(false);
            instance.BindSourcePrefab(state.Prefab);
            if (!instance.BindArea(this) || !instance.Initialize(_outputCamera))
            {
                Debug.LogWarning("영역 배치를 지원하는 효과인지, 이미지/입자 연결이 올바른지 확인해 주세요.", this);
                DestroyOwnedObject(instance.gameObject);
                continue;
            }
            state.Instance = instance;
        }
        if (isActiveAndEnabled)
            ResumeEffects();
    }

    private bool IsDuplicatePrefab(int index)
    {
        for (int i = 0; i < index; i++)
        {
            if (_effects[i] != null && _effects[i].Prefab == _effects[index].Prefab)
                return true;
        }
        return false;
    }

    private void OnEnable()
    {
        if (_initialized)
            ResumeEffects();
    }

    private void ResumeEffects()
    {
        ApplySettings(true);
        _effectRoot.SetActive(true);
    }

    private void Update()
    {
        if (!_initialized)
            return;
        if (HasChangedEntries())
            RebuildEffects();
        else
            ApplySettings(false);
    }

    private bool HasChangedEntries()
    {
        int count = _effects == null ? 0 : _effects.Length;
        if (_states.Length != count)
            return true;
        for (int i = 0; i < count; i++)
        {
            EffectEntry entry = _effects[i];
            if (_states[i].Entry != entry || _states[i].Prefab != (entry != null ? entry.Prefab : null))
                return true;
        }
        return false;
    }

    private void ApplySettings(bool force)
    {
        for (int i = 0; i < _states.Length; i++)
        {
            EffectState state = _states[i];
            EnvironmentScreenEffect instance = state.Instance;
            if (instance == null)
                continue;
            float intensity = Mathf.Clamp01(state.Entry.Intensity);
            float emission = Mathf.Max(0f, state.Entry.EmissionMultiplier);
            if (!force && Mathf.Approximately(state.Intensity, intensity)
                && Mathf.Approximately(state.EmissionMultiplier, emission))
                continue;
            state.Intensity = intensity;
            state.EmissionMultiplier = emission;
            instance.SetEmissionMultiplier(emission);
            if (intensity <= 0f)
            {
                instance.ResetEffect();
                instance.gameObject.SetActive(false);
                continue;
            }
            instance.ApplyIntensity(intensity);
            instance.SetTargetIntensity(intensity);
            instance.RefreshCamera();
            instance.gameObject.SetActive(true);
        }
    }

    private void LateUpdate()
    {
        if (!_initialized)
            return;
        for (int i = 0; i < _states.Length; i++)
        {
            EnvironmentScreenEffect instance = _states[i].Instance;
            if (instance != null && instance.isActiveAndEnabled)
                instance.RefreshCamera();
        }
    }

    private void OnDisable()
    {
        if (_effectRoot != null)
            _effectRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        ReleaseEffects();
    }

    private void ReleaseEffects()
    {
        if (_effectRoot == null)
            return;
        _effectRoot.SetActive(false);
        DestroyOwnedObject(_effectRoot);
        _effectRoot = null;
    }

    private static void DestroyOwnedObject(GameObject ownedObject)
    {
        if (Application.isPlaying)
            Destroy(ownedObject);
        else
            DestroyImmediate(ownedObject);
    }

    // 실행 중 Collider를 추가하거나 제거했을 때 호출합니다.
    public void RefreshColliders()
    {
        _boxes = GetComponents<BoxCollider2D>();
    }

    public bool Supports(EnvironmentScreenEffect prefab)
    {
        MigrateLegacySettings();
        if (prefab == null || _effects == null)
            return false;
        for (int i = 0; i < _effects.Length; i++)
        {
            if (_effects[i] != null && _effects[i].Prefab == prefab)
                return true;
        }
        return false;
    }

    // 이미지 배치용 사각 범위입니다. 여러 박스 사이의 빈 공간도 포함합니다.
    public bool TryGetBounds(out Bounds bounds)
    {
        bounds = default;
        if (!isActiveAndEnabled)
            return false;
        if (_boxes == null)
            RefreshColliders();
        bool found = false;
        for (int i = 0; i < _boxes.Length; i++)
        {
            BoxCollider2D box = _boxes[i];
            if (GetBoxWeight(box) <= 0f)
                continue;
            if (found)
                bounds.Encapsulate(box.bounds);
            else
                bounds = box.bounds;
            found = true;
        }
        return found;
    }

    public float GetSpawnWeight()
    {
        if (!isActiveAndEnabled)
            return 0f;
        if (_boxes == null)
            RefreshColliders();
        float total = 0f;
        for (int i = 0; i < _boxes.Length; i++)
            total += GetBoxWeight(_boxes[i]);
        return total;
    }

    private static float GetBoxWeight(BoxCollider2D box)
    {
        if (box == null || !box.enabled || !box.isTrigger || !box.gameObject.activeInHierarchy)
            return 0f;
        Vector3 x = box.transform.TransformVector(new Vector3(box.size.x, 0f, 0f));
        Vector3 y = box.transform.TransformVector(new Vector3(0f, box.size.y, 0f));
        return Mathf.Abs(x.x * y.y - x.y * y.x);
    }

    public bool TryGetSpawnPosition(out Vector2 position)
    {
        position = default;
        float total = GetSpawnWeight();
        if (total <= 0f)
            return false;
        float selection = Random.value * total;
        BoxCollider2D selected = null;
        for (int i = 0; i < _boxes.Length; i++)
        {
            float weight = GetBoxWeight(_boxes[i]);
            if (weight <= 0f)
                continue;
            selected = _boxes[i];
            selection -= weight;
            if (selection <= 0f)
                break;
        }
        if (selected == null)
            return false;

        // 실제 물리 모양도 확인해 회전, 스케일, 둥근 모서리의 바깥에 생성되지 않게 합니다.
        for (int attempt = 0; attempt < 16; attempt++)
        {
            Vector2 local = selected.offset + new Vector2(
                Random.Range(-0.5f, 0.5f) * selected.size.x,
                Random.Range(-0.5f, 0.5f) * selected.size.y);
            position = selected.transform.TransformPoint(local);
            if (selected.OverlapPoint(position))
                return true;
        }
        return false;
    }
}
