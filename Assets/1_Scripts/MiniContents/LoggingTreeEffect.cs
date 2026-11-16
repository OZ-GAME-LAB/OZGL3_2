using UnityEngine;

// 나무의 그림만 이동합니다. 타격점과 Collider의 Transform은 변경하지 않습니다.
public sealed class LoggingTreeEffect : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _visual;
    [Header("타격 흔들림")]
    [SerializeField, Min(0.01f)] private float _impactSeconds = 0.16f;
    [SerializeField, Min(0f)] private float _impactAmplitude = 0.12f;
    [Header("벌목 완료 연출")]
    [SerializeField, Min(0.01f)] private float _sinkSeconds = 1.1f;
    [SerializeField, Min(0f)] private float _sinkHorizontalAmplitude = 0.18f;
    [SerializeField, Min(0f)] private float _sinkVerticalAmplitude = 0.025f;
    [SerializeField, Min(0f)] private float _shakeFrequency = 18f;
    [Tooltip("이미지 밑의 투명 여백 비율. 원래 그림의 바닥을 가림 기준으로 사용합니다.")]
    [SerializeField, Range(0f, 0.9f)] private float _bottomInset;
    private static readonly int OffsetId = Shader.PropertyToID("_LoggingWorldOffset");
    private static readonly int PlaneId = Shader.PropertyToID("_LoggingClipPlane");
    private static readonly int ClipId = Shader.PropertyToID("_LoggingClipEnabled");
    private static readonly int VisibleId = Shader.PropertyToID("_LoggingVisible");
    private MaterialPropertyBlock _properties;
    private Vector3 _right, _up;
    private Vector4 _plane;
    private float _height;
    private bool _initialized;

    public float ImpactSeconds => SafeDuration(_impactSeconds, 0.16f);
    public float SinkSeconds => SafeDuration(_sinkSeconds, 1.1f);
    private static float SafeDuration(float value, float fallback) =>
        float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Max(0.01f, value);

    public void Initialize()
    {
        if (_visual == null || _visual.sprite == null) return;
        _properties ??= new MaterialPropertyBlock();
        Bounds bounds = _visual.sprite.bounds;
        Transform visual = _visual.transform;
        _right = visual.TransformVector(Vector3.right);
        _up = visual.TransformVector(Vector3.up);
        float bottom = bounds.min.y + bounds.size.y * _bottomInset;
        Vector3 origin = visual.TransformPoint(new Vector3(0f, bottom, 0f));
        Vector3 normal = _up.normalized;
        _plane = new Vector4(normal.x, normal.y, normal.z, Vector3.Dot(normal, origin));
        _height = bounds.max.y - bottom;
        _initialized = true;
        ResetPresentation();
    }

    public void ResetPresentation()
    {
        if (_initialized) Apply(Vector3.zero, false, true);
    }

    // 반환값은 다음 프레임에도 연출을 갱신해야 하는지를 나타냅니다.
    public bool Present(bool sinking, double startTime, float duration, double now)
    {
        if (!_initialized) return false;
        float elapsed = (float)(now - startTime);
        if (elapsed < 0f) { ResetPresentation(); return true; }
        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
        if (t >= 1f)
        {
            Apply(Vector3.zero, sinking, !sinking);
            return false;
        }
        float wave = Mathf.Sin(elapsed * _shakeFrequency * Mathf.PI * 2f);
        float envelope = 1f - t;
        float x = wave * (sinking ? _sinkHorizontalAmplitude : _impactAmplitude) * envelope;
        float y = sinking ? -_height * Mathf.SmoothStep(0f, 1f, t)
            + Mathf.Sin(elapsed * _shakeFrequency * 1.7f * Mathf.PI * 2f) * _sinkVerticalAmplitude * envelope : 0f;
        Apply(_right * x + _up * y, sinking, true);
        return true;
    }

    private void Apply(Vector3 offset, bool clip, bool visible)
    {
        _visual.GetPropertyBlock(_properties);
        _properties.SetVector(OffsetId, offset);
        _properties.SetVector(PlaneId, _plane);
        _properties.SetFloat(ClipId, clip ? 1f : 0f);
        _properties.SetFloat(VisibleId, visible ? 1f : 0f);
        _visual.SetPropertyBlock(_properties);
    }

    private void OnDisable() { ResetPresentation(); }
}
