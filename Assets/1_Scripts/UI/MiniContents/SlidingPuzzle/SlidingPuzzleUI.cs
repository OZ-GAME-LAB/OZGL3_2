using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class SlidingPuzzleUI : UIBase
{
    [Header("Image And References")]
    [SerializeField] private Texture2D _puzzleImage;
    [SerializeField] private RectTransform _board;
    [SerializeField] private SlidingPuzzleTileUI[] _tiles;
    [SerializeField] private Button _closeButton;

    [Header("Layout And Motion")]
    [SerializeField, Min(0f)] private float _gap = 4f;
    [SerializeField, Min(0f)] private float _moveDuration = 0.15f;

    [SerializeField] private AudioClip _moveSound;
    [SerializeField, Range(0f, 1f)] private float _moveVolume = 0.18f;
    private bool _initialized;
    private bool _solved;
    private bool _inputEnabled = true;
    private Coroutine _moveRoutine;

    public bool IsAnimating { get; private set; }
    public event Action<int> MoveRequested;
    public event Action CloseRequested;
    public event Action Hidden;

    public void Initialize()
    {
        if (_initialized)
            return;

        for (int i = 0; i < _tiles.Length; i++)
        {
            _tiles[i].Initialize(i + 1, _puzzleImage);
            _tiles[i].Clicked += OnTileClicked;
        }
        _closeButton.onClick.AddListener(OnCloseClicked);
        _initialized = true;
    }

    /// <summary>외부에서 확정한 상태를 표시합니다. 규칙이나 상태를 변경하지 않습니다.</summary>
    public void ShowState(int[] state, bool solved, int movedTileId = 0)
    {
        Initialize();
        CancelMotion();
        _solved = solved;
        if (movedTileId > 0 && _moveSound != null)
            SoundManager.Inst?.PlaySfx(_moveSound, _moveVolume);
        Vector2 start = Vector2.zero;
        Vector2 destination = Vector2.zero;
        RectTransform movingTile = null;
        Vector2 cellSize = (_board.rect.size - Vector2.one * (_gap * 4f)) / 3f;

        for (int index = 0; index < state.Length; index++)
        {
            int tileId = state[index];
            if (tileId == 0)
                continue;

            RectTransform tile = _tiles[tileId - 1].RectTransform;
            tile.sizeDelta = cellSize;
            Vector2 position = GetCellPosition(index, cellSize);
            if (tileId == movedTileId && _moveDuration > 0f && isActiveAndEnabled)
            {
                movingTile = tile;
                start = tile.anchoredPosition;
                destination = position;
            }
            else
            {
                tile.anchoredPosition = position;
            }
        }

        if (movingTile != null)
        {
            IsAnimating = true;
            RefreshInteraction();
            _moveRoutine = StartCoroutine(MoveTile(movingTile, start, destination));
        }
        else
        {
            RefreshInteraction();
        }
    }

    private Vector2 GetCellPosition(int index, Vector2 cellSize)
    {
        float x = -_board.rect.width * 0.5f + _gap + cellSize.x * 0.5f;
        float y = _board.rect.height * 0.5f - _gap - cellSize.y * 0.5f;
        return new Vector2(x + index % 3 * (cellSize.x + _gap),
            y - index / 3 * (cellSize.y + _gap));
    }

    private IEnumerator MoveTile(RectTransform tile, Vector2 start, Vector2 destination)
    {
        float elapsed = 0f;
        float duration = _moveDuration;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - (1f - progress) * (1f - progress);
            tile.anchoredPosition = Vector2.LerpUnclamped(start, destination, eased);
            yield return null;
        }

        tile.anchoredPosition = destination;
        _moveRoutine = null;
        IsAnimating = false;
        RefreshInteraction();
    }

    public void SetInputEnabled(bool enabled)
    {
        _inputEnabled = enabled;
        if (_initialized)
            RefreshInteraction();
    }

    private void RefreshInteraction()
    {
        for (int i = 0; i < _tiles.Length; i++)
            _tiles[i].SetInteractable(_inputEnabled && !_solved && !IsAnimating);
    }

    private void OnTileClicked(int tileId)
    {
        if (isActiveAndEnabled && _inputEnabled && !IsAnimating && !_solved)
            MoveRequested?.Invoke(tileId);
    }

    private void OnCloseClicked()
    {
        CloseRequested?.Invoke();
    }

    private void CancelMotion()
    {
        if (_moveRoutine != null)
            StopCoroutine(_moveRoutine);
        _moveRoutine = null;
        IsAnimating = false;
    }

    private void OnDisable()
    {
        CancelMotion();
        _inputEnabled = true;
        Hidden?.Invoke();
    }

    private void OnDestroy()
    {
        if (!_initialized)
            return;
        for (int i = 0; i < _tiles.Length; i++)
        {
            if (_tiles[i] != null)
                _tiles[i].Clicked -= OnTileClicked;
        }
        if (_closeButton != null)
            _closeButton.onClick.RemoveListener(OnCloseClicked);
    }
}
