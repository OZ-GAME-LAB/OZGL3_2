using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class RuneDescriptionPopup : UIBase
{
    [Header("설명 표시")]
    [SerializeField] private TMP_Text _text;
    [SerializeField, Min(1f)] private float _preferredWidth = 360f;
    [SerializeField] private Vector2 _padding = new(16f, 12f);
    [SerializeField] private Vector2 _cursorOffset = new(18f, 18f);
    [SerializeField, Min(0f)] private float _screenMargin = 8f;
    private RuneSlotUI _owner;
    private RectTransform _panel;
    private RectTransform _parent;
    private Canvas _canvas;
    private Vector2 _availableSize;

    public void Show(RuneSlotUI owner, ItemData data, PointerEventData pointer)
    {
        if (owner == null || data == null || pointer == null || _text == null) { Hide(owner); return; }
        _panel ??= (RectTransform)transform;
        _parent ??= transform.parent as RectTransform;
        if (_canvas == null) _canvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (_parent == null || _canvas == null) return;
        _owner = owner;
        string description = string.IsNullOrWhiteSpace(data.Description) ? "설명이 없습니다." : data.Description;
        _text.text = string.IsNullOrWhiteSpace(data.Name) ? description : data.Name + "\n\n" + description;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Position(pointer, true);
    }

    public void Move(RuneSlotUI owner, PointerEventData pointer)
    {
        if (_owner == owner && gameObject.activeSelf && pointer != null) Position(pointer, false);
    }

    public void Hide(RuneSlotUI owner)
    {
        if (_owner == owner) HideAll();
    }

    public void HideAll()
    {
        _owner = null;
        gameObject.SetActive(false);
    }

    private void Position(PointerEventData pointer, bool refreshSize)
    {
        Camera camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : pointer.enterEventCamera ?? _canvas.worldCamera;
        // 작은 인벤토리 루트가 아닌 실제 Canvas 화면을 부모 좌표로 변환합니다.
        Rect pixels = _canvas.pixelRect;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, pixels.min, camera, out Vector2 lower) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, pixels.max, camera, out Vector2 upper) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, pointer.position, camera, out Vector2 point)) return;
        Rect bounds = Rect.MinMaxRect(lower.x, lower.y, upper.x, upper.y);
        float margin = Mathf.Min(_screenMargin, Mathf.Min(bounds.width, bounds.height) * 0.1f);
        Vector2 available = bounds.size - Vector2.one * (margin * 2f);
        if (refreshSize || _availableSize != available)
        {
            _availableSize = available;
            float width = Mathf.Max(1f, Mathf.Min(_preferredWidth, available.x));
            _text.fontSize = _text.fontSizeMax;
            float height = _text.GetPreferredValues(_text.text, Mathf.Max(1f, width - _padding.x * 2f), Mathf.Infinity).y;
            _panel.sizeDelta = new Vector2(width, Mathf.Max(1f, Mathf.Min(height + _padding.y * 2f, available.y)));
            _text.rectTransform.offsetMin = _padding;
            _text.rectTransform.offsetMax = -_padding;
        }
        Vector2 size = _panel.sizeDelta;
        float x = point.x + _cursorOffset.x;
        float y = point.y - _cursorOffset.y;
        if (x + size.x > bounds.xMax - margin) x = point.x - _cursorOffset.x - size.x;
        if (y - size.y < bounds.yMin + margin) y = point.y + _cursorOffset.y + size.y;
        x = Mathf.Clamp(x, bounds.xMin + margin, bounds.xMax - margin - size.x);
        y = Mathf.Clamp(y, bounds.yMin + margin + size.y, bounds.yMax - margin);
        _panel.localPosition = new Vector3(x, y, 0f);
    }

    private void OnDisable() { _owner = null; }
}
