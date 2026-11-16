using UnityEngine;
using UnityEngine.UI;

// Geometry and warning presentation only. FishingVM owns all gameplay state.
[RequireComponent(typeof(CanvasRenderer))]
public class FishingHudGraphic : MaskableGraphic
{
    [Header("Circle")]
    [Tooltip("Twice the displayed logging reference diameter with the current HUD layout.")]
    [SerializeField, Min(1f)] private float _diameter = 68.5f;
    [SerializeField, Min(0.1f)] private float _lineWidth = 2f;
    [SerializeField] private Color _outlineColor = new(0.025f, 0.025f, 0.03f, 1f);
    [SerializeField] private Color _waterColor = new(0f, 0.62f, 0.86f, 0.95f);
    [SerializeField] private Color _zoneColor = new(0.15f, 0.8f, 0.3f, 1f);
    [SerializeField] private Color _pointerColor = new(1f, 0.08f, 0.12f, 1f);

    [Header("Warning")]
    [SerializeField, Min(0.1f)] private float _warningPeriod = 0.7f;
    [SerializeField] private Color _warningDark = new(0.24f, 0.025f, 0.05f, 1f);
    [SerializeField] private Color _warningBright = new(0.62f, 0.12f, 0.2f, 1f);

    private const int Segments = 96;
    private const float Softness = 0.75f;
    private static readonly Vector2[] Directions = CreateDirections();
    private readonly Vector2[] _waterPolygon = new Vector2[Segments + 2];
    private int _waterVertexCount;
    private float _angle;
    private float _zoneStart = 30f;
    private float _zoneWidth = 54f;
    private float _pointerWidth = 4f;
    private float _fill;
    private bool _warning;
    private float _warningTime;

    public void SetState(float angle, float zoneStart, float zoneWidth, float pointerWidth,
        float fill, bool warning)
    {
        fill = Mathf.Clamp01(fill);
        if (_angle == angle && _zoneStart == zoneStart && _zoneWidth == zoneWidth &&
            _pointerWidth == pointerWidth && _fill == fill && _warning == warning) return;
        if (_fill != fill) BuildWaterPolygon(fill);
        if (_warning != warning) _warningTime = 0f;
        _angle = angle;
        _zoneStart = zoneStart;
        _zoneWidth = zoneWidth;
        _pointerWidth = pointerWidth;
        _fill = fill;
        _warning = warning;
        SetVerticesDirty();
    }

    private void Update()
    {
        if (!_warning) return;
        _warningTime = Mathf.Repeat(_warningTime + Time.unscaledDeltaTime, Mathf.Max(0.1f, _warningPeriod));
        SetVerticesDirty();
    }

    protected override void OnDisable()
    {
        _warning = false;
        _warningTime = 0f;
        base.OnDisable();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = rectTransform.rect;
        float scale = Mathf.Min(1f, (Mathf.Min(rect.width, rect.height) - 16f) /
            (_diameter + Mathf.Max(18f, _lineWidth + 2f * Softness)));
        if (scale <= 0f) return;
        Vector2 center = rect.center;
        float radius = _diameter * 0.5f * scale;
        DrawWater(vh, center, radius);

        Color outline = _outlineColor;
        if (_warning)
        {
            float pulse = (1f + Mathf.Cos(_warningTime * Mathf.PI * 2f / Mathf.Max(0.1f, _warningPeriod))) * 0.5f;
            outline = Color.Lerp(_warningDark, _warningBright, pulse);
        }
        Color transparent = outline;
        transparent.a = 0f;
        float inner = Mathf.Max(0f, radius - _lineWidth * 0.5f * scale);
        float outer = radius + _lineWidth * 0.5f * scale;
        DrawArc(vh, center, Mathf.Max(0f, inner - Softness * scale), inner, 0f, 360f, transparent, outline);
        DrawArc(vh, center, inner, outer, 0f, 360f, outline, outline);
        DrawArc(vh, center, outer, outer + Softness * scale, 0f, 360f, outline, transparent);
        DrawArc(vh, center, radius + scale, radius + 6f * scale, _zoneStart, _zoneWidth, _zoneColor, _zoneColor);
        // Same angular width as the VM's overlap test, drawn above the success zone.
        DrawArc(vh, center, Mathf.Max(0f, radius - 4f * scale), radius + 8f * scale,
            _angle - _pointerWidth * 0.5f, _pointerWidth, _pointerColor, _pointerColor);
    }

    private void BuildWaterPolygon(float fill)
    {
        _waterVertexCount = 0;
        if (fill <= 0f) return;
        // Invert circular segment area so each quarter is area, not height.
        float low = -1f;
        float high = 1f;
        for (int i = 0; i < 20; i++)
        {
            float y = (low + high) * 0.5f;
            float area = (Mathf.Asin(y) + y * Mathf.Sqrt(Mathf.Max(0f, 1f - y * y)) + Mathf.PI * 0.5f) / Mathf.PI;
            if (area < fill) low = y;
            else high = y;
        }
        float level = fill >= 1f ? 1f : (low + high) * 0.5f;
        Vector2 previous = Directions[Segments - 1];
        for (int i = 0; i < Segments; i++)
        {
            Vector2 next = Directions[i];
            bool previousInside = previous.y <= level;
            bool nextInside = next.y <= level;
            if (previousInside != nextInside)
            {
                float fraction = (level - previous.y) / (next.y - previous.y);
                _waterPolygon[_waterVertexCount++] = previous + (next - previous) * fraction;
            }
            if (nextInside) _waterPolygon[_waterVertexCount++] = next;
            previous = next;
        }
    }

    private void DrawWater(VertexHelper vh, Vector2 center, float radius)
    {
        if (_waterVertexCount < 3) return;
        int first = vh.currentVertCount;
        for (int i = 0; i < _waterVertexCount; i++)
            AddVertex(vh, center + _waterPolygon[i] * radius, _waterColor);
        for (int i = 1; i < _waterVertexCount - 1; i++)
            vh.AddTriangle(first, first + i, first + i + 1);
    }

    private void DrawArc(VertexHelper vh, Vector2 center, float inner, float outer,
        float start, float angle, Color innerColor, Color outerColor)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt(Segments * angle / 360f));
        int first = vh.currentVertCount;
        for (int i = 0; i <= steps; i++)
        {
            Vector2 direction = start == 0f && angle == 360f
                ? Directions[i] : Direction(start + angle * i / steps);
            AddVertex(vh, center + direction * inner, innerColor);
            AddVertex(vh, center + direction * outer, outerColor);
        }
        for (int i = 0; i < steps; i++)
        {
            int index = first + i * 2;
            vh.AddTriangle(index, index + 1, index + 3);
            vh.AddTriangle(index, index + 3, index + 2);
        }
    }

    private void AddVertex(VertexHelper vh, Vector2 point, Color tint)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = point;
        vertex.color = tint * color;
        vh.AddVert(vertex);
    }

    private static Vector2 Direction(float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
    }

    private static Vector2[] CreateDirections()
    {
        Vector2[] result = new Vector2[Segments + 1];
        for (int i = 0; i <= Segments; i++) result[i] = Direction(i * 360f / Segments);
        return result;
    }
}
