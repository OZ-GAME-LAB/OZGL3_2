using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>배치된 석판과 퍼즐 UI를 연결합니다. ScenarioSource를 지정하면 서버 공유 모드입니다.</summary>
[RequireComponent(typeof(Collider2D))]
public sealed class SlidingPuzzleInteractable : InteractableObject
{
    private sealed class PuzzleViewModel : InteractionVMBase
    {
        public string Text;
        public override string InteractionInfoText => Text;
    }

    [Header("상호작용 안내")]
    [SerializeField] private string _interactionText = "퍼즐 도전하기 (F)";
    [Header("완료 표시")]
    [SerializeField] private GameObject _activateOnComplete;
    [SerializeField] private Collider2D _interactionArea;
    [Header("공유 사건 (비우면 로컬 퍼즐)")]
    [SerializeField] private ScenarioSource _sharedSource;

    private readonly PuzzleViewModel _viewModel = new PuzzleViewModel();
    private SlidingPuzzleModel _model;
    private SlidingPuzzleUI _ui;
    private UIManager _uiManager;
    private CancellationTokenSource _opening;
    private bool _interactionActive;
    private bool _completionPending;
    private SharedSlidingPuzzleSession _shared;
    private bool _initialMarkerActive;
    private bool _initialAreaEnabled;

    public override InteractionVMBase ViewModel => _viewModel;
    public override bool RequiresServerReservation => false;
    public override bool UsesMiniGameInput => true;
    public bool IsSolved => _sharedSource != null ? _shared != null && _shared.IsCompleted :
        _model != null && _model.IsSolved;

    protected override void Awake()
    {
        base.Awake();
        _viewModel.Text = _interactionText;
        if (_interactionArea == null)
            _interactionArea = GetComponent<Collider2D>();
        SetDestroyAfterUse(false);
        _initialMarkerActive = _activateOnComplete != null && _activateOnComplete.activeSelf;
        _initialAreaEnabled = _interactionArea != null && _interactionArea.enabled;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (_sharedSource != null)
        {
            _shared = new SharedSlidingPuzzleSession(_sharedSource);
            _shared.Changed += OnSharedChanged;
            _shared.BoardChanged += OnSharedBoardChanged;
            _shared.Ended += EndInteraction;
        }
        ApplyCompletedState();
        RefreshAvailability();
    }

    public override UniTask InitObject(string id)
    {
        RefreshAvailability();
        return UniTask.CompletedTask;
    }

    public override void BeginInteraction(Transform[] playerUIAnchors, Camera playerCamera, string equipmentId)
    {
        base.BeginInteraction(playerUIAnchors, playerCamera, equipmentId);
        UIManager manager = UIManager.Inst;
        if (_interactionActive || IsSolved || (_sharedSource != null && (_shared == null || !_shared.CanStart)) ||
            manager == null || manager.HasDialogueConflict() ||
            manager.IsOpen(UIType.DialogueUI) || manager.IsLoading(UIType.DialogueUI))
        {
            RequestInteractionEnd();
            return;
        }

        _interactionActive = true;
        _uiManager = manager;
        _opening = new CancellationTokenSource();
        if (_shared != null)
        {
            if (!_shared.TryBegin())
                EndInteraction();
        }
        else
            OpenAsync(manager, _opening.Token).Forget();
    }

    private async UniTask OpenAsync(UIManager manager, CancellationToken token)
    {
        try
        {
            SlidingPuzzleUI ui = await manager.OpenSlidingPuzzleUIAsync(token);
            if (this == null || token.IsCancellationRequested || !_interactionActive)
                return;
            if (ui == null)
            {
                Debug.LogError("퍼즐 UI 프리팹의 SlidingPuzzleUI 연결을 확인해 주세요.", this);
                EndInteraction();
                return;
            }

            _ui = ui;
            if (_shared == null)
                EnsureModel();
            _ui.MoveRequested += OnMoveRequested;
            _ui.CloseRequested += EndInteraction;
            _ui.Hidden += EndInteraction;
            Canvas.ForceUpdateCanvases();
            _ui.SetInputEnabled(_shared == null || _shared.CanMove);
            _ui.ShowState(_shared != null ? _shared.Board : _model.GetStateCopy(), IsSolved);
        }
        catch (OperationCanceledException)
        {
            // 로딩 중 닫힌 요청은 UIManager의 비활성 캐시에만 남습니다.
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            if (!token.IsCancellationRequested)
                EndInteraction();
        }
    }

    private void EnsureModel()
    {
        if (_model != null)
            return;
        _model = new SlidingPuzzleModel();
        _model.Shuffle(new System.Random());
    }

    private void OnMoveRequested(int tileId)
    {
        if (!_interactionActive || _ui == null || _ui.IsAnimating)
            return;
        if (_shared != null)
        {
            _shared.TryMove(tileId);
            return;
        }
        if (!_model.TryMove(tileId))
            return;

        _ui.ShowState(_model.GetStateCopy(), _model.IsSolved, tileId);
        if (!_model.IsSolved)
            return;

        _completionPending = true;
        RefreshAvailability();
    }

    public override void UpdateInteraction(float deltaTime)
    {
        _shared?.Tick(Time.unscaledDeltaTime);
        // 마지막 타일의 이동을 보여준 뒤 기존 상호작용 종료 흐름으로 복귀합니다.
        if (_completionPending && _ui != null && !_ui.IsAnimating)
            EndInteraction();
    }

    private void EndInteraction()
    {
        if (!_interactionActive)
            return;
        CancelInteraction();
        RequestInteractionEnd();
    }

    public override void CancelInteraction()
    {
        _interactionActive = false;
        _shared?.Cancel();
        _completionPending = false;
        ApplyCompletedState();
        if (_opening != null)
        {
            _opening.Cancel();
            _opening.Dispose();
            _opening = null;
        }
        if (_ui != null)
        {
            _ui.MoveRequested -= OnMoveRequested;
            _ui.CloseRequested -= EndInteraction;
            _ui.Hidden -= EndInteraction;
        }
        if (_uiManager != null && _uiManager.IsOpen(UIType.SlidingPuzzleUI))
            _uiManager.Close(UIType.SlidingPuzzleUI);
        _ui = null;
        _uiManager = null;
        RefreshAvailability();
    }

    private void RefreshAvailability()
    {
        _viewModel.IsAvailable = isActiveAndEnabled && !IsSolved &&
            (_sharedSource == null || (_shared != null && _shared.CanStart));
    }

    private void OnSharedChanged()
    {
        ApplyCompletedState();
        RefreshAvailability();
        if (_ui != null)
            _ui.SetInputEnabled(_shared.CanMove);
    }

    private void OnSharedBoardChanged(int tileId)
    {
        if (!_interactionActive)
            return;
        if (_ui == null)
            OpenAsync(_uiManager, _opening.Token).Forget();
        else
            _ui.ShowState(_shared.Board, IsSolved, tileId);
    }

    private void ApplyCompletedState()
    {
        if (!IsSolved && _sharedSource == null)
            return;
        // 석판 본체는 남기고 완료 표시와 상호작용 감지 영역만 변경합니다.
        if (_activateOnComplete != null && _activateOnComplete != gameObject &&
            _activateOnComplete.transform.IsChildOf(transform))
            _activateOnComplete.SetActive(IsSolved || _initialMarkerActive);
        if (_interactionArea != null && _interactionArea.isTrigger &&
            (_interactionArea.transform == transform || _interactionArea.transform.IsChildOf(transform)))
            _interactionArea.enabled = !IsSolved && _initialAreaEnabled;
    }

    protected override void OnDisable()
    {
        EndInteraction();
        if (_shared != null)
        {
            _shared.Changed -= OnSharedChanged;
            _shared.BoardChanged -= OnSharedBoardChanged;
            _shared.Ended -= EndInteraction;
            _shared.Dispose();
            _shared = null;
        }
        base.OnDisable();
    }
}
