using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class SlidingPuzzleTileUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RawImage _image;
    [SerializeField] private Button _button;

    public int TileId { get; private set; }
    public RectTransform RectTransform => (RectTransform)transform;
    public event Action<int> Clicked;

    public void Initialize(int tileId, Texture texture)
    {
        TileId = tileId;
        _image.texture = texture;
        int sourceIndex = tileId - 1;
        float part = 1f / SlidingPuzzleModel.Width;
        // UI의 첫 행은 위쪽, 텍스처 UV의 원점은 왼쪽 아래입니다.
        _image.uvRect = new Rect(sourceIndex % 3 * part,
            (2 - sourceIndex / 3) * part, part, part);
        _button.transition = Selectable.Transition.None;
        _button.onClick.RemoveListener(OnClick);
        _button.onClick.AddListener(OnClick);
    }

    public void SetInteractable(bool interactable)
    {
        _button.interactable = interactable;
    }

    private void OnClick()
    {
        Clicked?.Invoke(TileId);
    }

    private void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveListener(OnClick);
    }
}
