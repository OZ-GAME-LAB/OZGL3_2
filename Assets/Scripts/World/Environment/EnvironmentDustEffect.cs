using System;
using UnityEngine;

/// <summary>공통 영역에서 먼지를 생성합니다. 생성 후 이동과 수명은 Particle System이 담당합니다.</summary>
[DisallowMultipleComponent]
public sealed class EnvironmentDustEffect : EnvironmentScreenEffect
{
    [Serializable]
    private sealed class DustLayer
    {
        public ParticleSystem Particles;
        // 기존 프리팹의 참조를 유지하되, 월드 고정 좌표계로 사용합니다.
        public Transform SimulationSpace;
        [Min(0.1f)] public float DistanceFromCamera = 5f;
        [NonSerialized] public ParticleSystemRenderer Renderer;
        [NonSerialized] public MaterialPropertyBlock Properties;
        [NonSerialized] public Color BaseColor;
        [NonSerialized] public ParticleSystem.MinMaxCurve Rate;
        [NonSerialized] public ParticleSystem.Burst[] Bursts;
        [NonSerialized] public float Time;
        [NonSerialized] public float Remainder;
        [NonSerialized] public float BurstRemainder;
    }

    [Header("작은 먼지 / 가까운 흐릿한 먼지")]
    [SerializeField] private DustLayer[] _layers;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BoundsId = Shader.PropertyToID("_DustBounds");
    private static readonly int CameraRightId = Shader.PropertyToID("_DustCameraRight");
    private static readonly int CameraUpId = Shader.PropertyToID("_DustCameraUp");
    private EnvironmentEffectArea _area;
    private float _emissionMultiplier = 1f;
    private bool _prepared;
    private bool _emitParticles;

    public override bool BindArea(EnvironmentEffectArea area)
    {
        _area = area;
        return area != null;
    }

    public override void SetEmissionMultiplier(float multiplier)
    {
        _emissionMultiplier = Mathf.Max(0f, multiplier);
    }

    protected override bool Prepare()
    {
        _prepared = false;
        if (!OutputCamera.orthographic || _layers == null || _layers.Length == 0)
            return false;
        for (int i = 0; i < _layers.Length; i++)
        {
            DustLayer layer = _layers[i];
            if (layer == null || layer.Particles == null || layer.SimulationSpace == null
                || !layer.Particles.transform.IsChildOf(transform)
                || !layer.SimulationSpace.IsChildOf(transform)
                || layer.SimulationSpace == layer.Particles.transform
                || layer.Particles.transform.IsChildOf(layer.SimulationSpace))
                return false;
            layer.Renderer = layer.Particles.GetComponent<ParticleSystemRenderer>();
            if (layer.Renderer == null || layer.Renderer.sharedMaterial == null
                || layer.Renderer.sharedMaterial.shader.name != "WanderSoul/Environment/GlowDust")
                return false;

            layer.Properties = new MaterialPropertyBlock();
            layer.BaseColor = layer.Renderer.sharedMaterial.GetColor(BaseColorId);
            layer.Particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = layer.Particles.main;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.None;
            main.simulationSpace = ParticleSystemSimulationSpace.Custom;
            main.customSimulationSpace = layer.SimulationSpace;
            main.useUnscaledTime = false;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

            ParticleSystem.EmissionModule emission = layer.Particles.emission;
            layer.Rate = emission.rateOverTime;
            layer.Bursts = new ParticleSystem.Burst[emission.burstCount];
            emission.GetBursts(layer.Bursts);
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = layer.Particles.shape;
            shape.enabled = false;
            // 좌표의 화면 투영을 상수로 만들어 기존 셰이더의 화면 경계 마스크를 해제합니다.
            layer.Renderer.GetPropertyBlock(layer.Properties);
            layer.Properties.SetVector(BoundsId, new Vector4(-1f, -1f, 2f, 2f));
            layer.Properties.SetVector(CameraRightId, Vector4.zero);
            layer.Properties.SetVector(CameraUpId, Vector4.zero);
            layer.Renderer.SetPropertyBlock(layer.Properties);
        }
        _prepared = true;
        ResetEffect();
        return true;
    }

    private void OnEnable()
    {
        if (_prepared)
        {
            RefreshEmission();
        }
    }

    public override void SetTargetIntensity(float intensity)
    {
        _emitParticles = intensity > 0f;
        if (_prepared && isActiveAndEnabled)
            RefreshEmission();
    }

    private void RefreshEmission()
    {
        for (int i = 0; i < _layers.Length; i++)
        {
            if (_emitParticles && !_layers[i].Particles.isPlaying)
                _layers[i].Particles.Play(false);
        }
    }

    private void Update()
    {
        if (!_prepared || !_emitParticles || OutputCamera == null || Time.deltaTime <= 0f)
            return;
        for (int i = 0; i < _layers.Length; i++)
            AdvanceEmission(_layers[i], Time.deltaTime);
    }

    private void AdvanceEmission(DustLayer layer, float deltaTime)
    {
        ParticleSystem.MainModule main = layer.Particles.main;
        float duration = Mathf.Max(0.01f, main.duration);
        float remaining = deltaTime * main.simulationSpeed;
        while (remaining > 0f)
        {
            if (layer.Time >= duration)
            {
                if (!main.loop)
                    return;
                layer.Time = 0f;
            }
            float step = Mathf.Min(remaining, duration - layer.Time);
            float end = layer.Time + step;
            float rate = Mathf.Max(0f, layer.Rate.Evaluate(layer.Time / duration, UnityEngine.Random.value));
            layer.Remainder += rate * step * _emissionMultiplier;
            int count = Mathf.FloorToInt(layer.Remainder);
            layer.Remainder -= count;
            Emit(layer, count);
            EmitBursts(layer, layer.Time, end);
            layer.Time = end;
            remaining -= step;
        }
    }

    private void EmitBursts(DustLayer layer, float from, float to)
    {
        for (int i = 0; i < layer.Bursts.Length; i++)
        {
            ParticleSystem.Burst burst = layer.Bursts[i];
            float interval = Mathf.Max(0.01f, burst.repeatInterval);
            int first = Mathf.Max(0, Mathf.CeilToInt((from - burst.time) / interval));
            int limit = Mathf.CeilToInt((to - burst.time) / interval);
            if (burst.cycleCount > 0)
                limit = Mathf.Min(limit, burst.cycleCount);
            for (int cycle = first; cycle < limit; cycle++)
            {
                if (UnityEngine.Random.value < burst.probability)
                {
                    float count = Mathf.Max(0, Mathf.RoundToInt(burst.count.Evaluate(
                        layer.Time / layer.Particles.main.duration, UnityEngine.Random.value)));
                    layer.BurstRemainder += count * _emissionMultiplier;
                    int emitCount = Mathf.FloorToInt(layer.BurstRemainder);
                    layer.BurstRemainder -= emitCount;
                    Emit(layer, emitCount);
                }
            }
        }
    }

    private void Emit(DustLayer layer, int count)
    {
        count = Mathf.Min(count, layer.Particles.main.maxParticles - layer.Particles.particleCount);
        for (int i = 0; i < count; i++)
        {
            if (!TryGetSpawnPosition(out Vector2 position))
                return;
            float distance = Mathf.Clamp(layer.DistanceFromCamera,
                OutputCamera.nearClipPlane + 0.1f, OutputCamera.farClipPlane - 0.1f);
            Vector3 world = new Vector3(position.x, position.y,
                OutputCamera.transform.position.z + distance);
            ParticleSystem.EmitParams parameters = new ParticleSystem.EmitParams
            {
                position = layer.SimulationSpace.InverseTransformPoint(world),
                applyShapeToPosition = false
            };
            layer.Particles.Emit(parameters, 1);
        }
    }

    private bool TryGetSpawnPosition(out Vector2 position)
    {
        position = default;
        return _area != null && _area.TryGetSpawnPosition(out position);
    }

    public override void ApplyIntensity(float intensity)
    {
        if (!_prepared)
            return;
        float brightness = Mathf.Clamp01(intensity);
        for (int i = 0; i < _layers.Length; i++)
        {
            DustLayer layer = _layers[i];
            Color color = layer.BaseColor;
            color.r *= brightness;
            color.g *= brightness;
            color.b *= brightness;
            layer.Renderer.GetPropertyBlock(layer.Properties);
            layer.Properties.SetColor(BaseColorId, color);
            layer.Renderer.SetPropertyBlock(layer.Properties);
        }
    }

    public override void ResetEffect()
    {
        _emitParticles = false;
        if (!_prepared)
            return;
        for (int i = 0; i < _layers.Length; i++)
        {
            DustLayer layer = _layers[i];
            layer.Time = 0f;
            layer.Remainder = 0f;
            layer.BurstRemainder = 0f;
            layer.Particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        ApplyIntensity(0f);
    }

    private void OnDisable()
    {
        ResetEffect();
    }
}
