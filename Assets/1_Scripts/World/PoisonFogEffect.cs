using UnityEngine;

/// <summary>바탕 안개와 흐르는 안개의 시각 설정을 관리합니다. 피해 및 네트워크 상태는 관리하지 않습니다.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class PoisonFogEffect : MonoBehaviour
{
    [Header("바탕 안개 / 흐르는 안개 연결")]
    [SerializeField] private ParticleSystem _particles;
    [SerializeField] private ParticleSystemRenderer _particleRenderer;
    [SerializeField] private ParticleSystem _wisps;
    [SerializeField] private ParticleSystemRenderer _wispRenderer;

    [Header("농도와 색상")]
    [SerializeField, Range(0f, 1f), Tooltip("0: 숨김 / 1: 가장 짙음. 두 층의 투명도를 함께 조절합니다.")]
    private float _density = 0.75f;
    [SerializeField] private Color _fogColor = new Color(0.46f, 0.38f, 0.52f, 1f);

    [Header("발생 중심 범위 (외곽으로 자연스럽게 퍼집니다)")]
    [SerializeField, Tooltip("입자가 발생하는 타원 영역의 너비와 높이입니다. 입자의 가장자리는 이 영역 밖까지 퍼집니다.")]
    private Vector2 _areaSize = new Vector2(24f, 5f);
    [SerializeField, Range(0.5f, 2f), Tooltip("입자 크기와 수명 중 확산 이동에 적용하는 배율입니다.")]
    private float _spread = 1f;
    [SerializeField] private int _sortingOrder = 15;

    [Header("움직임과 질감")]
    [SerializeField, Range(0f, 1f)] private float _flowSpeed = 0.3f;
    [SerializeField, Range(0f, 1f)] private float _swirlStrength = 0.4f;
    [SerializeField, Range(0.2f, 3f)] private float _noiseScale = 1f;

    [Header("입자 수 제한")]
    [SerializeField, Range(1f, 8f), Tooltip("두 층을 합한 초당 발생량입니다. 최대 입자 수는 각 Particle System에서 제한합니다.")]
    private float _fullDensityEmission = 5.6f;

    private static readonly int DensityId = Shader.PropertyToID("_Density");
    private static readonly int ColorId = Shader.PropertyToID("_FogColor");
    private static readonly int MotionId = Shader.PropertyToID("_FogMotion");
    private static readonly int TimeId = Shader.PropertyToID("_FogTime");
    private static readonly int LayerId = Shader.PropertyToID("_Layer");
    private MaterialPropertyBlock _baseProperties;
    private MaterialPropertyBlock _wispProperties;
    private bool _settingsDirty = true;
    private float _flowTime;

    public float Density => _density;

    private void OnEnable()
    {
        _flowTime = 0f;
        _settingsDirty = true;
        ClearParticles(_particles);
        ClearParticles(_wisps);
        ApplySettings();
    }

    private void OnDisable()
    {
        ClearParticles(_particles);
        ClearParticles(_wisps);
        if (_particleRenderer != null)
            _particleRenderer.enabled = false;
        if (_wispRenderer != null)
            _wispRenderer.enabled = false;
    }

    private void OnValidate()
    {
        _density = Mathf.Clamp01(_density);
        _areaSize.x = Mathf.Max(0.1f, _areaSize.x);
        _areaSize.y = Mathf.Max(0.1f, _areaSize.y);
        _spread = Mathf.Clamp(_spread, 0.5f, 2f);
        _fullDensityEmission = Mathf.Clamp(_fullDensityEmission, 1f, 8f);
        // Unity 객체 변경은 메인 스레드의 Update 또는 편집기 미리보기에서 처리합니다.
        _settingsDirty = true;
    }

    private void Update()
    {
        if (_settingsDirty && !ApplySettings())
            return;
        AdvanceShader(Application.IsPlaying(gameObject) ? Time.deltaTime : 0f);
    }

    /// <summary>농도를 변경합니다. 이미 떠 있는 입자에도 바로 반영됩니다.</summary>
    public void SetDensity(float density)
    {
        if (float.IsNaN(density) || float.IsInfinity(density))
            return;
        _density = Mathf.Clamp01(density);
        _settingsDirty = true;
        if (isActiveAndEnabled)
            ApplySettings();
    }

    private bool ApplySettings()
    {
        if (_particles == null || _particleRenderer == null || _wisps == null || _wispRenderer == null)
            return false;
        if (_baseProperties == null)
            _baseProperties = new MaterialPropertyBlock();
        if (_wispProperties == null)
            _wispProperties = new MaterialPropertyBlock();

        ApplyLayer(_particles, _particleRenderer, _baseProperties, false);
        ApplyLayer(_wisps, _wispRenderer, _wispProperties, true);
        _settingsDirty = false;
        AdvanceShader(0f);
        RefreshPlayback(_particles);
        RefreshPlayback(_wisps);
        return true;
    }

    private void ApplyLayer(ParticleSystem particles, ParticleSystemRenderer renderer,
        MaterialPropertyBlock properties, bool isWisp)
    {
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.scale = new Vector3(_areaSize.x * 0.5f, _areaSize.y * 0.5f, 1f);
        ParticleSystem.EmissionModule emission = particles.emission;
        // 농도는 주로 투명도로 조절해, 높은 농도에서도 입자가 지나치게 중첩되지 않게 합니다.
        emission.rateOverTime = _density <= 0f ? 0f
            : _fullDensityEmission * (isWisp ? 0.57f : 0.43f) * Mathf.Lerp(0.8f, 1f, _density);
        ParticleSystem.MainModule main = particles.main;
        main.startSizeX = new ParticleSystem.MinMaxCurve(isWisp ? 7f * _spread : 9f * _spread,
            isWisp ? 13f * _spread : 15f * _spread);
        main.startSizeY = new ParticleSystem.MinMaxCurve(isWisp ? 1f * _spread : 3.5f * _spread,
            isWisp ? 2.4f * _spread : 6f * _spread);
        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.x = new ParticleSystem.MinMaxCurve(_flowSpeed * (isWisp ? 0.6f : -0.2f),
            _flowSpeed * (isWisp ? 1.6f : 0.65f));
        velocity.y = new ParticleSystem.MinMaxCurve(-_flowSpeed * 0.15f, _flowSpeed * 0.18f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.radial = new ParticleSystem.MinMaxCurve(_flowSpeed * _spread * 0.1f,
            _flowSpeed * _spread * 0.3f);

        renderer.enabled = _density > 0f;
        renderer.sortingOrder = _sortingOrder + (isWisp ? 1 : 0);
        properties.SetFloat(DensityId, _density);
        properties.SetColor(ColorId, _fogColor);
        properties.SetVector(MotionId, new Vector4(_flowSpeed, _swirlStrength, _noiseScale, 0f));
        properties.SetFloat(LayerId, isWisp ? 1f : 0f);
    }

    private void RefreshPlayback(ParticleSystem particles)
    {
        if (_density <= 0f)
        {
            ClearParticles(particles);
            return;
        }
        if (particles.particleCount == 0)
        {
            particles.Simulate(particles.main.duration, false, true, true);
            if (Application.IsPlaying(gameObject))
                particles.Play(false);
        }
    }

    private static void ClearParticles(ParticleSystem particles)
    {
        if (particles != null)
            particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void AdvanceShader(float deltaTime)
    {
        if (_baseProperties == null || _wispProperties == null || _density <= 0f)
            return;
        _flowTime += deltaTime;
        _baseProperties.SetFloat(TimeId, _flowTime);
        _wispProperties.SetFloat(TimeId, _flowTime);
        _particleRenderer.SetPropertyBlock(_baseProperties);
        _wispRenderer.SetPropertyBlock(_wispProperties);
    }

#if UNITY_EDITOR
    /// <summary>선택한 프리팹의 두 층을 같은 시간 간격으로 미리 재생합니다.</summary>
    public void AdvanceEditorPreview(float deltaTime)
    {
        if (Application.IsPlaying(gameObject) || !isActiveAndEnabled)
            return;
        if (_settingsDirty && !ApplySettings())
            return;
        float step = Mathf.Clamp(deltaTime, 0f, 0.05f);
        if (_density > 0f)
        {
            _particles.Simulate(step, false, false, false);
            _wisps.Simulate(step, false, false, false);
        }
        AdvanceShader(step);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.7f, 0.5f, 0.8f, 0.6f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Vector3 previous = new Vector3(_areaSize.x * 0.5f, 0f, 0f);
        for (int i = 1; i <= 48; i++)
        {
            float angle = i * Mathf.PI * 2f / 48f;
            Vector3 next = new Vector3(Mathf.Cos(angle) * _areaSize.x * 0.5f,
                Mathf.Sin(angle) * _areaSize.y * 0.5f, 0f);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
#endif
}
