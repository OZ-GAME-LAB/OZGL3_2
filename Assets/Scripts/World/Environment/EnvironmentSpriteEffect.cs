using UnityEngine;

/// <summary>카메라를 따라가는 안개 이미지의 전체 알파를 조절합니다.</summary>
[DisallowMultipleComponent]
public sealed class EnvironmentSpriteEffect : EnvironmentScreenEffect
{
    [Header("안개 이미지 — 색상 알파는 최대 강도 기준")]
    [SerializeField] private SpriteRenderer[] _sprites;
    [Header("화면 배치")]
    [SerializeField, Min(0.1f)] private float _distanceFromCamera = 5f;
    [SerializeField] private bool _fitViewport = true;
    [SerializeField, Min(1f)] private float _viewportPadding = 1.1f;

    private Color[] _originalColors;
    private Vector3 _originalScale;

    protected override bool Prepare()
    {
        if (!OutputCamera.orthographic || _sprites == null || _sprites.Length == 0)
            return false;

        _originalColors = new Color[_sprites.Length];
        _originalScale = transform.localScale;
        for (int i = 0; i < _sprites.Length; i++)
        {
            if (_sprites[i] == null || _sprites[i].sprite == null)
                return false;
            _originalColors[i] = _sprites[i].color;
        }
        return true;
    }

    public override void ApplyIntensity(float intensity)
    {
        for (int i = 0; i < _sprites.Length; i++)
        {
            if (_sprites[i] == null)
                continue;
            Color color = _originalColors[i];
            color.a *= Mathf.Clamp01(intensity);
            _sprites[i].color = color;
        }
    }

    public override void ResetEffect()
    {
        ApplyIntensity(0f);
    }

    public override void RefreshCamera()
    {
        if (OutputCamera == null)
            return;

        float distance = Mathf.Clamp(_distanceFromCamera,
            OutputCamera.nearClipPlane + 0.01f, OutputCamera.farClipPlane - 0.01f);
        transform.SetPositionAndRotation(
            OutputCamera.transform.position + OutputCamera.transform.forward * distance,
            OutputCamera.transform.rotation);

        // 이미지들은 루트 중심에 배치합니다. 첫 이미지가 화면 크기의 기준입니다.
        transform.localScale = _originalScale;
        if (!_fitViewport || _sprites[0] == null)
            return;
        Vector3 size = _sprites[0].localBounds.size;
        Vector3 scale = _sprites[0].transform.lossyScale;
        float width = Mathf.Abs(size.x * scale.x);
        float height = Mathf.Abs(size.y * scale.y);
        if (width <= 0f || height <= 0f)
            return;
        float viewHeight = OutputCamera.orthographicSize * 2f * _viewportPadding;
        float fit = Mathf.Max(viewHeight / height, viewHeight * OutputCamera.aspect / width);
        transform.localScale = _originalScale * fit;
    }
}
