using System;
using UnityEngine;

/// <summary>맵에 남아 로컬 상태를 유지합니다. 네트워크 연결 시 이 연결 역할을 교체합니다.</summary>
public sealed class LocalSlidingPuzzleController : MonoBehaviour
{
    [Header("Puzzle UI")]
    [SerializeField] private SlidingPuzzleUI _ui;

    private SlidingPuzzleModel _model;
    private readonly System.Random _random = new System.Random();

    public bool IsSolved => _model != null && _model.IsSolved;
    public event Action Completed;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void Start()
    {
        // 테스트 씬에서 처음부터 켜 둔 UI에도 입력 이벤트를 연결합니다.
        if (_ui.gameObject.activeSelf)
            Open();
    }

    [ContextMenu("Open Puzzle")]
    public void Open()
    {
        EnsureInitialized();
        UnbindUI();
        _ui.MoveRequested += OnMoveRequested;
        _ui.CloseRequested += Close;
        _ui.Hidden += UnbindUI;
        _ui.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        RefreshUI();
    }

    [ContextMenu("Close Puzzle")]
    public void Close()
    {
        UnbindUI();
        if (_ui != null)
            _ui.gameObject.SetActive(false);
    }

    /// <summary>명시적인 새 게임입니다. UI를 열 때는 호출하지 않습니다.</summary>
    public void NewPuzzle()
    {
        EnsureInitialized();
        _model.Shuffle(_random);
        RefreshUI();
    }

    public int[] GetStateCopy()
    {
        EnsureInitialized();
        return _model.GetStateCopy();
    }

    /// <summary>저장 데이터나 외부에서 확정한 상태를 복원하는 연결 지점입니다.</summary>
    public bool TryApplyState(int[] state)
    {
        EnsureInitialized();
        if (!_model.TryApplyState(state))
            return false;
        RefreshUI();
        return true;
    }

    private void EnsureInitialized()
    {
        if (_model != null)
            return;
        _model = new SlidingPuzzleModel();
        _model.Shuffle(_random);
        _ui.Initialize();
    }

    private void OnMoveRequested(int tileId)
    {
        if (!_model.TryMove(tileId))
            return;

        _ui.ShowState(_model.GetStateCopy(), _model.IsSolved, tileId);
        if (_model.IsSolved)
            Completed?.Invoke();
    }

    private void RefreshUI()
    {
        if (_ui.gameObject.activeInHierarchy)
            _ui.ShowState(_model.GetStateCopy(), _model.IsSolved);
    }

    private void UnbindUI()
    {
        if (_ui == null)
            return;
        _ui.MoveRequested -= OnMoveRequested;
        _ui.CloseRequested -= Close;
        _ui.Hidden -= UnbindUI;
    }

    private void OnDisable()
    {
        Close();
    }
}
