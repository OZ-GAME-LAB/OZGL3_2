using UnityEngine;
using UnityEngine.UI;

// Presentation only: the view model owns judgment, timing and completed rounds.
[RequireComponent(typeof(CanvasRenderer))]
public class ResourceTimingGraphic : MaskableGraphic
{
    [Header("Circle")]
    [SerializeField, Min(1f)] private float _referenceDiameter = 40f;
    [SerializeField, Min(0.1f)] private float _lineWidth = 2f;
    [SerializeField, Min(0.1f)] private float _edgeSoftness = 0.75f;
    [SerializeField] private Color _referenceColor = new(0.025f, 0.025f, 0.03f, 1f);
    [SerializeField] private Color _startColor = new(1f, 0.55f, 0.65f, 0.72f);
    [SerializeField] private Color _matchedColor = new(0.95f, 0.06f, 0.1f, 0.95f);
    [SerializeField] private Color _endColor = new(0.025f, 0.025f, 0.03f, 0.95f);

    [Header("Progress")]
    [SerializeField] private Vector2 _barSize = new(88f, 8f);
    [SerializeField, Min(0f)] private float _barGap = 10f;
    [SerializeField] private Color _progressColor = new(0.25f, 0.85f, 0.4f, 1f);
    [SerializeField] private Color _trackColor = new(0.08f, 0.09f, 0.1f, 0.55f);

    private const int Segments = 64;
    private const float Padding = 8f;
    private static readonly Vector2[] CircleDirections = CreateCircleDirections();

    private float _sizeRatio = 2.5f;
    private float _startRatio = 2.5f;
    private float _endRatio = 0.2f;
    private float _progress;

    public void SetState(float sizeRatio, float startRatio, float endRatio, float progress)
    {
        sizeRatio = Mathf.Max(0f, sizeRatio);
        progress = Mathf.Clamp01(progress);
        if (_sizeRatio == sizeRatio && _startRatio == startRatio &&
            _endRatio == endRatio && _progress == progress)
            return;

        _sizeRatio = sizeRatio;
        _startRatio = startRatio;
        _endRatio = endRatio;
        _progress = progress;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = rectTransform.rect;
        float maximumDiameter = _referenceDiameter * Mathf.Max(1f, _startRatio)
            + _lineWidth + _edgeSoftness * 2f;
        float barWidth = Mathf.Max(4f, _barSize.x);
        float barHeight = Mathf.Max(4f, _barSize.y);
        float totalHeight = maximumDiameter + _barGap + barHeight;
        float scale = Mathf.Min(
            (rect.width - Padding * 2f) / Mathf.Max(maximumDiameter, barWidth),
            (rect.height - Padding * 2f) / totalHeight);
        if (scale <= 0f)
            return;

        Vector2 center = rect.center - Vector2.up * ((_barGap + barHeight) * 0.5f * scale);
        float radius = _referenceDiameter * 0.5f * scale;
        float softness = _edgeSoftness * scale;
        Color movingColor = _sizeRatio >= 1f
            ? Color.Lerp(_matchedColor, _startColor, Mathf.InverseLerp(1f, _startRatio, _sizeRatio))
            : Color.Lerp(_endColor, _matchedColor, Mathf.InverseLerp(_endRatio, 1f, _sizeRatio));

        DrawDisk(vh, center, radius * _sizeRatio, softness, movingColor);
        // Draw the fixed outline last so the moving disk cannot cover it.
        DrawOutline(vh, center, radius, _lineWidth * scale, softness);

        Vector2 barCenter = center + Vector2.up *
            ((maximumDiameter * 0.5f + _barGap + barHeight * 0.5f) * scale);
        DrawProgress(vh, barCenter, new Vector2(barWidth, barHeight) * scale, scale);
    }

    private void DrawDisk(VertexHelper vh, Vector2 center, float radius, float softness, Color tint)
    {
        float solidRadius = Mathf.Max(0f, radius - softness);
        int first = vh.currentVertCount;
        AddVertex(vh, center, tint);
        for (int i = 0; i <= Segments; i++)
            AddVertex(vh, center + CircleDirections[i] * solidRadius, tint);
        for (int i = 0; i < Segments; i++)
            vh.AddTriangle(first, first + i + 1, first + i + 2);

        Color transparent = tint;
        transparent.a = 0f;
        DrawBand(vh, center, solidRadius, radius, tint, transparent);
    }

    private void DrawOutline(VertexHelper vh, Vector2 center, float radius, float width, float softness)
    {
        float inner = Mathf.Max(0f, radius - width * 0.5f);
        float outer = radius + width * 0.5f;
        Color transparent = _referenceColor;
        transparent.a = 0f;
        DrawBand(vh, center, Mathf.Max(0f, inner - softness), inner, transparent, _referenceColor);
        DrawBand(vh, center, inner, outer, _referenceColor, _referenceColor);
        DrawBand(vh, center, outer, outer + softness, _referenceColor, transparent);
    }

    private void DrawBand(VertexHelper vh, Vector2 center, float inner, float outer,
        Color innerColor, Color outerColor)
    {
        int first = vh.currentVertCount;
        for (int i = 0; i <= Segments; i++)
        {
            AddVertex(vh, center + CircleDirections[i] * inner, innerColor);
            AddVertex(vh, center + CircleDirections[i] * outer, outerColor);
        }
        for (int i = 0; i < Segments; i++)
        {
            int index = first + i * 2;
            vh.AddTriangle(index, index + 1, index + 3);
            vh.AddTriangle(index, index + 3, index + 2);
        }
    }

    private void DrawProgress(VertexHelper vh, Vector2 center, Vector2 size, float scale)
    {
        Rect outer = new(center - size * 0.5f, size);
        DrawRectangle(vh, outer, _referenceColor);
        float border = Mathf.Min(1.25f * scale, size.y * 0.25f);
        Rect inner = Rect.MinMaxRect(outer.xMin + border, outer.yMin + border,
            outer.xMax - border, outer.yMax - border);
        DrawRectangle(vh, inner, _trackColor);
        if (_progress > 0f)
            DrawRectangle(vh, new Rect(inner.x, inner.y, inner.width * _progress, inner.height), _progressColor);

        for (int i = 1; i < 3; i++)
        {
            float x = inner.xMin + inner.width * i / 3f;
            DrawRectangle(vh, new Rect(x - scale * 0.5f, inner.y, scale, inner.height), _referenceColor);
        }
    }

    private void DrawRectangle(VertexHelper vh, Rect rect, Color tint)
    {
        int first = vh.currentVertCount;
        AddVertex(vh, new Vector2(rect.xMin, rect.yMin), tint);
        AddVertex(vh, new Vector2(rect.xMax, rect.yMin), tint);
        AddVertex(vh, new Vector2(rect.xMax, rect.yMax), tint);
        AddVertex(vh, new Vector2(rect.xMin, rect.yMax), tint);
        vh.AddTriangle(first, first + 1, first + 2);
        vh.AddTriangle(first, first + 2, first + 3);
    }

    private void AddVertex(VertexHelper vh, Vector2 position, Color tint)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = tint * color;
        vh.AddVert(vertex);
    }

    private static Vector2[] CreateCircleDirections()
    {
        Vector2[] directions = new Vector2[Segments + 1];
        for (int i = 0; i <= Segments; i++)
        {
            float angle = i * Mathf.PI * 2f / Segments;
            directions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
        return directions;
    }
}
