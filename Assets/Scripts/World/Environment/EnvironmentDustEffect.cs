using System;
using UnityEngine;

/// <summary>입자의 모양과 수명은 Particle System, 화면 시차와 전환은 이 컴포넌트가 담당합니다.</summary>
[DisallowMultipleComponent]
public sealed class EnvironmentDustEffect : EnvironmentScreenEffect
{
    [Serializable]
    private sealed class DustLayer
    {
        public ParticleSystem Particles;
        public Transform SimulationSpace;
        [Tooltip("1은 월드 물체와 같은 화면 이동량, 1보다 크면 가까운 전경입니다.")]
        public Vector2 ScreenMotion = new Vector2(1.1f, 1.05f);
        [Min(0.1f)] public float DistanceFromCamera = 5f;

        [NonSerialized] public ParticleSystemRenderer Renderer;
        [NonSerialized] public MaterialPropertyBlock Properties;
        [NonSerialized] public Color BaseColor;
    }

    [Header("작은 먼지 / 가까운 흐릿한 먼지")]
    [SerializeField] private DustLayer[] _layers;
    [Header("화면 생성 범위 — 왼쪽 아래가 (0, 0), 오른쪽 위가 (1, 1)")]
    [Tooltip("X/Y는 왼쪽 아래 위치, W/H는 너비와 높이입니다.")]
    [SerializeField] private Rect _viewportBounds = new Rect(0f, 0.1f, 1f, 0.74f);
    [Header("경계 안쪽 페이드 폭 — 화면 너비/높이의 비율")]
    [SerializeField] private Vector2 _edgeFadeWidth = new Vector2(0.03f, 0.06f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BoundsId = Shader.PropertyToID("_DustBounds");
    private static readonly int FadeId = Shader.PropertyToID("_DustFade");
    private static readonly int CameraPositionId = Shader.PropertyToID("_DustCameraPosition");
    private static readonly int CameraRightId = Shader.PropertyToID("_DustCameraRight");
    private static readonly int CameraUpId = Shader.PropertyToID("_DustCameraUp");
    private Vector3 _previousCameraPosition;
    private bool _hasCameraPosition;
    private bool _prepared;
    private bool _emitParticles;

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
        }

        _prepared = true;
        ResetEffect();
        return true;
    }

    private void OnEnable()
    {
        if (!_prepared)
            return;
        RefreshCamera();
        RefreshEmission();
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
            ParticleSystem particles = _layers[i].Particles;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = _emitParticles;
            // 퇴장 중에는 재생을 유지하고 생성만 끕니다. 재진입해도 기존 입자가 유지됩니다.
            if (_emitParticles && !particles.isPlaying)
                particles.Play(false);
        }
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
            // Additive 머티리얼의 RGB를 줄여 살아 있는 입자도 함께 사라지게 합니다.
            layer.Renderer.GetPropertyBlock(layer.Properties);
            layer.Properties.SetColor(BaseColorId, color);
            layer.Renderer.SetPropertyBlock(layer.Properties);
        }
    }

    public override void RefreshCamera()
    {
        if (!_prepared || OutputCamera == null)
            return;
        Vector3 cameraPosition = OutputCamera.transform.position;
        Vector3 delta = _hasCameraPosition ? cameraPosition - _previousCameraPosition : Vector3.zero;
        float height = OutputCamera.orthographicSize * 2f;
        float width = height * OutputCamera.aspect;
        Rect bounds = GetViewportBounds();
        Vector2 fade = new Vector2(Mathf.Clamp(_edgeFadeWidth.x, 0f, bounds.width * 0.5f),
            Mathf.Clamp(_edgeFadeWidth.y, 0f, bounds.height * 0.5f));
        Vector3 spawnOffset = OutputCamera.transform.right * ((bounds.center.x - 0.5f) * width)
            + OutputCamera.transform.up * ((bounds.center.y - 0.5f) * height);

        for (int i = 0; i < _layers.Length; i++)
        {
            DustLayer layer = _layers[i];
            float distance = Mathf.Clamp(layer.DistanceFromCamera,
                OutputCamera.nearClipPlane + 0.1f, OutputCamera.farClipPlane - 0.1f);
            Vector3 position = cameraPosition + OutputCamera.transform.forward * distance;
            if (!_hasCameraPosition)
                layer.SimulationSpace.position = position;
            else
            {
                Vector3 simulationPosition = layer.SimulationSpace.position;
                simulationPosition.x += delta.x * (1f - layer.ScreenMotion.x);
                simulationPosition.y += delta.y * (1f - layer.ScreenMotion.y);
                simulationPosition.z = position.z;
                layer.SimulationSpace.position = simulationPosition;
            }
            layer.Particles.transform.position = position + spawnOffset;
            ParticleSystem.ShapeModule shape = layer.Particles.shape;
            shape.scale = new Vector3(width * bounds.width, height * bounds.height, 0.01f);
            layer.Renderer.GetPropertyBlock(layer.Properties);
            layer.Properties.SetVector(BoundsId, new Vector4(bounds.xMin, bounds.yMin, bounds.xMax, bounds.yMax));
            layer.Properties.SetVector(FadeId, new Vector4(fade.x, fade.y, 0f, 0f));
            layer.Properties.SetVector(CameraPositionId, cameraPosition);
            layer.Properties.SetVector(CameraRightId, OutputCamera.transform.right / Mathf.Max(width, 0.001f));
            layer.Properties.SetVector(CameraUpId, OutputCamera.transform.up / Mathf.Max(height, 0.001f));
            layer.Renderer.SetPropertyBlock(layer.Properties);
        }
        _previousCameraPosition = cameraPosition;
        _hasCameraPosition = true;
    }

    private Rect GetViewportBounds()
    {
        float x = Mathf.Clamp(_viewportBounds.x, 0f, 0.999f);
        float y = Mathf.Clamp(_viewportBounds.y, 0f, 0.999f);
        return new Rect(x, y, Mathf.Clamp(_viewportBounds.width, 0.001f, 1f - x),
            Mathf.Clamp(_viewportBounds.height, 0.001f, 1f - y));
    }

    private void OnValidate()
    {
        _viewportBounds = GetViewportBounds();
        _edgeFadeWidth.x = Mathf.Clamp(_edgeFadeWidth.x, 0f, _viewportBounds.width * 0.5f);
        _edgeFadeWidth.y = Mathf.Clamp(_edgeFadeWidth.y, 0f, _viewportBounds.height * 0.5f);
    }

    public override void ResetEffect()
    {
        _emitParticles = false;
        _hasCameraPosition = false;
        if (!_prepared)
            return;
        for (int i = 0; i < _layers.Length; i++)
        {
            ParticleSystem particles = _layers[i].Particles;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        ApplyIntensity(0f);
    }

    private void OnDisable()
    {
        ResetEffect();
    }
}
