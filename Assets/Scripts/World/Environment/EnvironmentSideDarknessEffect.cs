using UnityEngine;

/// <summary>카메라 좌우 절반을 덮는 검은 스크린의 농도를 방향별 진행량으로 조절합니다.</summary>
[DisallowMultipleComponent]
public sealed class EnvironmentSideDarknessEffect : EnvironmentScreenEffect
{
    [Header("화면 절반씩 채울 스크린 — 직접 자식, 중앙 피벗")]
    [SerializeField] private SpriteRenderer _leftScreen;
    [SerializeField] private SpriteRenderer _rightScreen;
    [Header("카메라 배치와 농도 반응")]
    [SerializeField, Min(0.1f)] private float _distanceFromCamera = 1f;
    [SerializeField, Min(0f)] private float _responseTime = 0.25f;

    private Transform _target;
    private EnvironmentDirectionalDarknessArea[] _areas;
    private float _regionIntensity;
    private float _leftAlpha;
    private float _rightAlpha;
    private bool _snapNext = true;

    public override void BindWorldContext(Transform target, Transform mapRoot)
    {
        _target = target;
        _areas = mapRoot != null
            ? mapRoot.GetComponentsInChildren<EnvironmentDirectionalDarknessArea>(true)
            : System.Array.Empty<EnvironmentDirectionalDarknessArea>();
    }

    protected override bool Prepare()
    {
        if (!OutputCamera.orthographic || !ValidScreen(_leftScreen) ||
            !ValidScreen(_rightScreen) || _leftScreen == _rightScreen)
            return false;
        ResetEffect();
        return true;
    }

    private bool ValidScreen(SpriteRenderer screen)
    {
        return screen != null && screen.sprite != null && screen.transform.parent == transform &&
            screen.sprite.bounds.size.x > 0f && screen.sprite.bounds.size.y > 0f;
    }

    public override void ApplyIntensity(float intensity)
    {
        _regionIntensity = Mathf.Clamp01(intensity);
        UpdateColors();
    }

    private void Update()
    {
        float left = 0f;
        float right = 0f;
        if (_target != null && _areas != null)
        {
            Vector2 position = _target.position;
            for (int i = 0; i < _areas.Length; i++)
            {
                EnvironmentDirectionalDarknessArea area = _areas[i];
                if (area == null) continue;
                float alpha = area.Evaluate(position);
                if (area.Side != EnvironmentDirectionalDarknessArea.ScreenSide.Right)
                    left = Mathf.Max(left, alpha);
                if (area.Side != EnvironmentDirectionalDarknessArea.ScreenSide.Left)
                    right = Mathf.Max(right, alpha);
            }
        }

        float blend = _snapNext || _responseTime <= 0f
            ? 1f : 1f - Mathf.Exp(-Time.deltaTime / _responseTime);
        _leftAlpha = Mathf.Lerp(_leftAlpha, left, blend);
        _rightAlpha = Mathf.Lerp(_rightAlpha, right, blend);
        _snapNext = false;
        UpdateColors();
    }

    private void UpdateColors()
    {
        if (_leftScreen != null)
            _leftScreen.color = new Color(0f, 0f, 0f, _leftAlpha * _regionIntensity);
        if (_rightScreen != null)
            _rightScreen.color = new Color(0f, 0f, 0f, _rightAlpha * _regionIntensity);
    }

    public override void ResetEffect()
    {
        _regionIntensity = _leftAlpha = _rightAlpha = 0f;
        _snapNext = true;
        UpdateColors();
    }

    public override void RefreshCamera()
    {
        if (OutputCamera == null || _leftScreen == null || _rightScreen == null) return;
        float distance = Mathf.Clamp(_distanceFromCamera,
            OutputCamera.nearClipPlane + 0.01f, OutputCamera.farClipPlane - 0.01f);
        transform.SetPositionAndRotation(OutputCamera.transform.position +
            OutputCamera.transform.forward * distance, OutputCamera.transform.rotation);
        transform.localScale = Vector3.one;
        float height = OutputCamera.orthographicSize * 2f;
        float halfWidth = height * OutputCamera.aspect * 0.5f;
        FitScreen(_leftScreen, -halfWidth * 0.5f, halfWidth, height);
        FitScreen(_rightScreen, halfWidth * 0.5f, halfWidth, height);
    }

    private static void FitScreen(SpriteRenderer screen, float x, float width, float height)
    {
        Bounds bounds = screen.sprite.bounds;
        Vector3 scale = new Vector3(width / bounds.size.x, height / bounds.size.y, 1f);
        screen.transform.localScale = scale;
        screen.transform.localRotation = Quaternion.identity;
        screen.transform.localPosition = new Vector3(x - bounds.center.x * scale.x,
            -bounds.center.y * scale.y, 0f);
    }
}
