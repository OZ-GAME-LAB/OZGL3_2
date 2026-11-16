using UnityEngine;

// 범위 표시만 담당합니다. 자원의 상태와 표시 여부는 상호작용 본체가 전달합니다.
[DisallowMultipleComponent]
public sealed class ResourceRangeEffect : MonoBehaviour
{
    [Header("내부 / 가장자리 파티클")]
    [SerializeField] private ParticleSystem _interior;
    [SerializeField] private ParticleSystem _border;

    [Header("색상과 움직임")]
    [SerializeField] private Color _color = new Color(0.55f, 0.95f, 0.68f, 0.65f);
    [SerializeField, Min(0.01f)] private float _particleSize = 0.35f;
    [SerializeField, Min(0.01f)] private float _lifetime = 1.4f;
    [SerializeField, Min(0f)] private float _riseSpeed = 0.35f;
    [SerializeField, Range(0f, 1f)] private float _borderRiseMultiplier = 0.3f;
    [SerializeField] private int _sortingOrder = 320;

    [Header("밀도와 제한")]
    [SerializeField, Min(0f)] private float _interiorRatePerArea = 0.8f;
    [SerializeField, Min(0f)] private float _borderRatePerLength = 1.2f;
    [SerializeField, Min(1f)] private float _maxRatePerLayer = 24f;
    [SerializeField, Min(1)] private int _maxParticlesPerLayer = 64;
    private static readonly Gradient LifetimeFade = new Gradient
    {
        colorKeys = new[]
        {
            new GradientColorKey(Color.white, 0f),
            new GradientColorKey(Color.white, 1f)
        },
        alphaKeys = new[]
        {
            new GradientAlphaKey(0f, 0f),
            new GradientAlphaKey(1f, 0.1f),
            new GradientAlphaKey(0f, 1f)
        }
    };
    private bool _configured;
    private bool _visible;

    public void Configure(BoxCollider2D bounds)
    {
        SetVisible(false);
        _configured = false;
        if (bounds == null || _interior == null || _border == null
            || transform.parent != bounds.transform)
            return;

        Vector2 size = bounds.size;
        if (size.x <= 0f || size.y <= 0f)
            return;

        // 콜라이더의 바로 아래 자식으로 두어 같은 좌표계를 사용합니다.
        transform.localPosition = new Vector3(bounds.offset.x, bounds.offset.y, 0f);
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        float width = bounds.transform.TransformVector(Vector3.right * size.x).magnitude;
        float height = bounds.transform.TransformVector(Vector3.up * size.y).magnitude;
        ConfigureLayer(_interior, size, width * height * _interiorRatePerArea, 0.75f, 1f);
        ConfigureLayer(_border, size, 2f * (width + height) * _borderRatePerLength, 1f, _borderRiseMultiplier);
        _configured = true;
    }

    private void ConfigureLayer(ParticleSystem particles, Vector2 size, float rate, float opacity, float riseMultiplier)
    {
        particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        particles.transform.localPosition = Vector3.zero;
        particles.transform.localRotation = Quaternion.identity;
        particles.transform.localScale = Vector3.one;
        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = true;
        main.prewarm = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.cullingMode = ParticleSystemCullingMode.Pause;
        main.startLifetime = new ParticleSystem.MinMaxCurve(_lifetime * 0.65f, _lifetime);
        float particleSize = Mathf.Min(_particleSize, Mathf.Min(size.x, size.y) * 0.75f);
        main.startSize = new ParticleSystem.MinMaxCurve(particleSize * 0.55f, particleSize);
        main.startSpeed = 0f;
        Color color = _color;
        color.a *= opacity;
        main.startColor = color;
        main.maxParticles = _maxParticlesPerLayer;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = Mathf.Clamp(rate, 0f, _maxRatePerLayer);
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.scale = new Vector3(size.x, size.y, particles == _interior ? 0f : 1f);
        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.y = _riseSpeed * riseMultiplier;
        // 생성 범위만 콜라이더에 맞추고, 떠오른 입자는 수명에 따라 사라집니다.
        ParticleSystem.ColorOverLifetimeModule lifetimeColor = particles.colorOverLifetime;
        lifetimeColor.enabled = true;
        lifetimeColor.color = new ParticleSystem.MinMaxGradient(LifetimeFade);
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = _sortingOrder;
    }

    public void SetVisible(bool visible)
    {
        visible = visible && _configured && isActiveAndEnabled;
        if (_visible == visible && visible)
            return;
        _visible = visible;
        SetLayerVisible(_interior, visible);
        SetLayerVisible(_border, visible);
    }

    private static void SetLayerVisible(ParticleSystem particles, bool visible)
    {
        if (particles == null) return;
        if (visible) particles.Play(false);
        else particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnDisable()
    {
        SetVisible(false);
    }
}
