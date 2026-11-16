using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class GatheringProgressGraphic : MaskableGraphic
{
    [Header("Circle")]
    [SerializeField, Min(1f)] private float _diameter = 34.25f;
    [SerializeField, Min(0.1f)] private float _lineWidth = 1.7f;
    [SerializeField] private Color _outlineColor = new(0.025f, 0.025f, 0.03f, 1f);
    [SerializeField] private Color _fillColor = new(0.25f, 0.85f, 0.4f, 1f);

    private const int Segments = 64;
    private const float Softness = 0.65f;
    private static readonly Vector2[] Directions = CreateDirections();
    private readonly Vector2[] _fillPolygon = new Vector2[Segments + 2];
    private float _progress;

    public void SetProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);
        if (_progress == progress) return;
        _progress = progress;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = rectTransform.rect;
        float scale = Mathf.Min(1f, (Mathf.Min(rect.width, rect.height) - 16f) /
            (_diameter + _lineWidth + Softness * 2f));
        if (scale <= 0f) return;
        Vector2 center = rect.center;
        float radius = _diameter * 0.5f * scale;
        DrawFill(vh, center, radius);

        float inner = Mathf.Max(0f, radius - _lineWidth * 0.5f * scale);
        float outer = radius + _lineWidth * 0.5f * scale;
        Color transparent = _outlineColor;
        transparent.a = 0f;
        DrawBand(vh, center, Mathf.Max(0f, inner - Softness * scale), inner, transparent, _outlineColor);
        DrawBand(vh, center, inner, outer, _outlineColor, _outlineColor);
        DrawBand(vh, center, outer, outer + Softness * scale, _outlineColor, transparent);
    }

    private void DrawFill(VertexHelper vh, Vector2 center, float radius)
    {
        if (_progress <= 0f) return;
        // A horizontal waterline moves from bottom to top at a constant speed.
        float level = _progress * 2f - 1f;
        int count = 0;
        Vector2 previous = Directions[Segments - 1];
        for (int i = 0; i < Segments; i++)
        {
            Vector2 next = Directions[i];
            bool previousInside = previous.y <= level;
            bool nextInside = next.y <= level;
            if (previousInside != nextInside)
            {
                float fraction = (level - previous.y) / (next.y - previous.y);
                _fillPolygon[count++] = previous + (next - previous) * fraction;
            }
            if (nextInside) _fillPolygon[count++] = next;
            previous = next;
        }

        int first = vh.currentVertCount;
        for (int i = 0; i < count; i++) AddVertex(vh, center + _fillPolygon[i] * radius, _fillColor);
        for (int i = 1; i < count - 1; i++) vh.AddTriangle(first, first + i, first + i + 1);
    }

    private void DrawBand(VertexHelper vh, Vector2 center, float inner, float outer,
        Color innerColor, Color outerColor)
    {
        int first = vh.currentVertCount;
        for (int i = 0; i <= Segments; i++)
        {
            AddVertex(vh, center + Directions[i] * inner, innerColor);
            AddVertex(vh, center + Directions[i] * outer, outerColor);
        }
        for (int i = 0; i < Segments; i++)
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

    private static Vector2[] CreateDirections()
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
