using System.Collections;
using UnityEngine;

/// <summary>세로 틈의 너비를 좌우로 펼칩니다. 포탈의 이동 기능이나 게임 진행 상태는 관리하지 않습니다.</summary>
[DisallowMultipleComponent]
public sealed class PortalActivationEffect : MonoBehaviour
{
    [Header("포탈 시각 요소")]
    [SerializeField] private MeshRenderer _surface;
    [SerializeField] private ParticleSystem _rimSparkles;

    [Header("좌우 열림 연출")]
    [SerializeField, Min(0f)] private float _openDuration = 0.8f;
    [SerializeField, Range(0.005f, 1f)] private float _startWidthRatio = 0.02f;
    [SerializeField] private AnimationCurve _widthCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool _openOnEnable = true;

    private Vector3 _fullSurfaceScale;
    private Vector3 _fullShapeScale;
    private float _fullEmissionMultiplier;
    private float _elapsed;
    private float _duration;
    private bool _initialized;
    private Coroutine _openRoutine;

    public bool IsOpen { get; private set; }
    public bool IsOpening { get; private set; }

    private void Awake()
    {
        if (InitializeVisuals())
            ResetClosed();
    }

    private void OnEnable()
    {
        if (_openOnEnable)
            Open();
    }

    /// <summary>닫힌 포탈을 엽니다. 이미 열리거나 열린 포탈에 대한 중복 호출은 무시합니다.</summary>
    public void Open()
    {
        if (!isActiveAndEnabled || IsOpen || IsOpening || !InitializeVisuals())
            return;

        _elapsed = 0f;
        _duration = Mathf.Max(0f, _openDuration);
        IsOpening = true;
        ApplyWidth(Mathf.Clamp(_startWidthRatio, 0.005f, 1f));
        _surface.enabled = true;
        _rimSparkles.Play(false);

        if (_duration <= 0f)
        {
            CompleteOpen();
            return;
        }

        _openRoutine = StartCoroutine(OpenRoutine());
    }

    /// <summary>즉시 숨기고 입자를 비웁니다. 이후 Open()으로 처음부터 다시 재생할 수 있습니다.</summary>
    public void ResetClosed()
    {
        if (_openRoutine != null)
        {
            StopCoroutine(_openRoutine);
            _openRoutine = null;
        }
        IsOpen = false;
        IsOpening = false;
        _elapsed = 0f;
        if (!_initialized)
            return;

        if (_surface != null)
        {
            _surface.enabled = false;
            _surface.transform.localScale = _fullSurfaceScale;
        }
        if (_rimSparkles != null)
        {
            _rimSparkles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.ShapeModule shape = _rimSparkles.shape;
            shape.scale = _fullShapeScale;
            ParticleSystem.EmissionModule emission = _rimSparkles.emission;
            emission.rateOverTimeMultiplier = _fullEmissionMultiplier;
        }
    }

    private bool InitializeVisuals()
    {
        if (_initialized)
            return _surface != null && _rimSparkles != null;
        if (_surface == null || _rimSparkles == null
            || _surface.transform == transform || _rimSparkles.transform == transform
            || !_surface.transform.IsChildOf(transform) || !_rimSparkles.transform.IsChildOf(transform)
            || _rimSparkles.transform.IsChildOf(_surface.transform))
        {
            Debug.LogWarning("포탈 본체와 입자를 서로 분리된 자식 오브젝트로 연결해 주세요.", this);
            return false;
        }

        _fullSurfaceScale = _surface.transform.localScale;
        _fullShapeScale = _rimSparkles.shape.scale;
        _fullEmissionMultiplier = _rimSparkles.emission.rateOverTimeMultiplier;
        _rimSparkles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = _rimSparkles.main;
        main.playOnAwake = false;
        main.prewarm = false;
        _initialized = true;
        return true;
    }

    private IEnumerator OpenRoutine()
    {
        while (IsOpening)
        {
            yield return null;
            AdvanceOpening(Time.deltaTime);
        }
        _openRoutine = null;
    }

    private void AdvanceOpening(float deltaTime)
    {
        if (!IsOpening)
            return;
        if (_surface == null || _rimSparkles == null)
        {
            ResetClosed();
            return;
        }
        _elapsed = Mathf.Min(_elapsed + Mathf.Max(0f, deltaTime), _duration);
        if (_elapsed >= _duration)
        {
            CompleteOpen();
            return;
        }

        float progress = _elapsed / _duration;
        float eased = _widthCurve == null || _widthCurve.length == 0
            ? progress : Mathf.Clamp01(_widthCurve.Evaluate(progress));
        ApplyWidth(Mathf.Lerp(Mathf.Clamp(_startWidthRatio, 0.005f, 1f), 1f, eased));
    }

    private void ApplyWidth(float ratio)
    {
        Vector3 scale = _fullSurfaceScale;
        scale.x *= ratio;
        _surface.transform.localScale = scale;

        // 입자의 Transform과 크기는 유지하고 새 입자가 나오는 타원의 너비만 변경합니다.
        ParticleSystem.ShapeModule shape = _rimSparkles.shape;
        scale = _fullShapeScale;
        scale.x *= ratio;
        shape.scale = scale;
        ParticleSystem.EmissionModule emission = _rimSparkles.emission;
        emission.rateOverTimeMultiplier = _fullEmissionMultiplier * ratio;
    }

    private void CompleteOpen()
    {
        ApplyWidth(1f);
        IsOpening = false;
        IsOpen = true;
    }

    private void OnDisable()
    {
        ResetClosed();
    }
}
