using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class InteractionUIPositioner : MonoBehaviour
{
    // Canvas 로컬 단위. Canvas Scaler 기준 크기에 따라 조정.
    [SerializeField, Min(0f)] private float _edgePadding = 12f;
    [SerializeField, Min(0f)] private float _returnPadding = 16f;

    private static readonly Vector2[] Pivots =
    {
        new(0.5f, 0f),
        new(0.5f, 1f),
        new(0f, 0.5f),
        new(1f, 0.5f)
    };

    private readonly Vector3[] _corners = new Vector3[4];

    private Transform[] _anchors;
    private Camera _worldCamera;
    private Camera _uiCamera;

    private RectTransform _uiRect;
    private RectTransform _canvasRect;

    private int _currentIndex = -1;

    public void SetTargets(Transform[] anchors, Camera worldCamera)
    {
        _anchors = null;
        _currentIndex = -1;

        if (anchors == null || anchors.Length != 4 ||
            worldCamera == null)
        {
            Debug.LogError("상·하·우·좌 Anchor 4개와 카메라가 필요합니다.");
            return;
        }

        foreach (Transform anchor in anchors)
        {
            if (anchor == null)
            {
                Debug.LogError("Anchor 배열에 비어 있는 항목이 있습니다.");
                return;
            }
        }

        Canvas canvas = GetComponentInParent<Canvas>();

        if (canvas == null)
        {
            Debug.LogError("부모 Canvas가 필요합니다.");
            return;
        }

        canvas = canvas.rootCanvas;

        if (canvas.renderMode == RenderMode.WorldSpace)
        {
            Debug.LogError("이 컴포넌트는 Screen Space Canvas용입니다.");
            return;
        }

        _uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera;

        if (canvas.renderMode == RenderMode.ScreenSpaceCamera && _uiCamera == null)
        {
            Debug.LogError("Canvas의 Render Camera를 연결해 주세요.");
            return;
        }

        _uiRect = (RectTransform)transform;
        _canvasRect = (RectTransform)canvas.transform;
        _worldCamera = worldCamera;
        _anchors = (Transform[])anchors.Clone();

        // 최초 배치 전에 UI 크기를 확정한다.
        Canvas.ForceUpdateCanvases();
        UpdatePosition();
    }

    private void LateUpdate()
    {
        if (_anchors != null)
            UpdatePosition();
    }

    private void UpdatePosition()
    {
        // 현재 방향이 여전히 들어가는지 먼저 확인한다.
        bool currentFits =
            _currentIndex >= 0 &&
            PlaceAt(_currentIndex) &&
            FitsInside(_edgePadding);

        for (int i = 0; i < _anchors.Length; i++)
        {
            if (!PlaceAt(i))
                continue;

            float padding = _edgePadding;

            // 현재 위치가 유효할 때, 우선순위가 높은 위치로
            // 돌아가려면 추가 여유 공간을 요구한다.
            if (currentFits && i < _currentIndex)
                padding += _returnPadding;

            if (!FitsInside(padding))
                continue;

            _currentIndex = i;
            return;
        }

        // 모든 후보가 실패하면 위쪽 위치를 화면 안으로 보정한다.
        _currentIndex = -1;

        if (PlaceAt(0))
            MoveInside();
    }

    private bool PlaceAt(int index)
    {
        Transform anchor = _anchors[index];

        if (anchor == null || _worldCamera == null)
            return false;

        Vector3 screenPoint =
            _worldCamera.WorldToScreenPoint(anchor.position);

        // 카메라 뒤에 있는 위치는 후보로 사용하지 않는다.
        if (screenPoint.z <= 0f)
            return false;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect,
                screenPoint,
                _uiCamera,
                out Vector2 canvasPoint))
        {
            return false;
        }

        _uiRect.pivot = Pivots[index];

        _uiRect.position = _canvasRect.TransformPoint(
            new Vector3(canvasPoint.x, canvasPoint.y, 0f));

        return true;
    }

    private Rect GetUIBounds()
    {
        _uiRect.GetWorldCorners(_corners);

        Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new(float.NegativeInfinity, float.NegativeInfinity);

        foreach (Vector3 corner in _corners)
        {
            Vector3 local = _canvasRect.InverseTransformPoint(corner);
            Vector2 point = new(local.x, local.y);

            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private bool FitsInside(float padding)
    {
        Rect ui = GetUIBounds();
        Rect canvas = _canvasRect.rect;

        return ui.xMin >= canvas.xMin + padding &&
               ui.xMax <= canvas.xMax - padding &&
               ui.yMin >= canvas.yMin + padding &&
               ui.yMax <= canvas.yMax - padding;
    }

    private void MoveInside()
    {
        Rect ui = GetUIBounds();
        Rect canvas = _canvasRect.rect;

        float offsetX = GetCorrection(
            ui.xMin, ui.xMax,
            canvas.xMin + _edgePadding,
            canvas.xMax - _edgePadding);

        float offsetY = GetCorrection(
            ui.yMin, ui.yMax,
            canvas.yMin + _edgePadding,
            canvas.yMax - _edgePadding);

        _uiRect.position += _canvasRect.TransformVector(
            new Vector3(offsetX, offsetY, 0f));
    }

    private static float GetCorrection(
        float min, float max, float limitMin, float limitMax)
    {
        // UI 자체가 표시 영역보다 크면 가운데 정렬한다.
        if (max - min > limitMax - limitMin)
            return (limitMin + limitMax - min - max) * 0.5f;

        if (min < limitMin)
            return limitMin - min;

        if (max > limitMax)
            return limitMax - max;

        return 0f;
    }

    private void OnDisable()
    {
        _anchors = null;
        _worldCamera = null;
        _currentIndex = -1;
    }
}
