using Cysharp.Threading.Tasks;
using UnityEngine;

public class InteractionLoggingMiningObject : InteractableObject
{
    [Header("수축 설정 — 현재는 테스트 값")]
    [SerializeField] private float _startSizeRatio = 2.5f;
    [SerializeField] private float _endSizeRatio = 0.2f;
    [SerializeField] private float _shrinkSpeed = 1f;

    [Header("판정 설정")]
    [SerializeField] private float _successStartRatio = 1.6f;
    [SerializeField] private float _perfectTolerance = 0.05f;

    [Header("난이도 설정")]
    [SerializeField, Min(0f)]
    private float _increasePerTier = 0.25f;

    private int _targetTier;
    private bool _loggingMotionInProgress;
    private bool _loggingResultPending;

    private readonly InteractionLoggingMiningVM _viewModel = new();

    public override InteractionVMBase ViewModel => _viewModel;
    public override bool RequiresContentReady => true;
    public bool IsLogging => InteractionType == "Logging";
    public bool IsFinalLoggingJudgment => IsLogging && _viewModel.CompletedRounds >= InteractionLoggingMiningVM.TotalRounds;
    public float LoggingFinalAccuracy => _viewModel.FinalAccuracy;

    private Transform[] _playerUIAnchors;
    private Camera _playerCamera;



    public override bool UsesMiniGameInput => true;

    public override async UniTask InitObject(string id)
    {
        FieldObjectData data = DataManager.Inst.GetData<FieldObjectData>(id);

        if (data == null)
            throw new System.InvalidOperationException("벌목·채광 데이터가 없습니다: " + id);

        _targetTier = data.Tier;
        _viewModel.SetInteractionType(data.InteractionType);

        _viewModel.Initialize(_startSizeRatio, _endSizeRatio, _shrinkSpeed, _successStartRatio, _perfectTolerance);

        if (UsesPlacedVisual)
            return;

        GameObject visual = await ResourceManager.Inst.InstantiateAsync(
            data.PrefabPath,
            transform,
            false,
            true);

        if (visual == null)
        {
            throw new System.InvalidOperationException("외형 생성 실패: " + id);
        }

        RegisterVisual(visual);
    }

    public override void BeginInteraction(Transform[] playerUIAnchors, Camera playerCamera, string equipmentId)
    {
        base.BeginInteraction(playerUIAnchors, playerCamera, equipmentId);

        if (!TryGetInteractionEquipmentData(out _))
        {
            RequestInteractionEnd();
            return;
        }

        ItemData equipmentItem = DataManager.Inst.GetData<ItemData>(InteractionEquipmentId);

        if (equipmentItem == null)
        {
            Debug.LogWarning($"장비의 아이템 데이터를 찾지 못했습니다: {InteractionEquipmentId}", this);

            RequestInteractionEnd();
            return;
        }

        if (_targetTier < 1 || equipmentItem.Tier < 1)
        {
            Debug.LogWarning($"티어는 1 이상이어야 합니다. " + $"대상: {_targetTier}, 장비: {equipmentItem.Tier}", this);

            RequestInteractionEnd();
            return;
        }

        float multiplier = GameUtil.CalculateInteractionDifficultyMultiplier(_targetTier, equipmentItem.Tier, _increasePerTier);

        float shrinkSpeed = _shrinkSpeed * multiplier;

        _viewModel.Initialize(
            _startSizeRatio,
            _endSizeRatio,
            shrinkSpeed,
            _successStartRatio,
            _perfectTolerance);

        _playerUIAnchors = playerUIAnchors;
        _playerCamera = playerCamera;

        PlayContent();
    }

    protected override void PlayContent()
    {
        OpenMiniGameUIAsync().Forget();
    }

    private async UniTask OpenMiniGameUIAsync()
    {
        try
        {
            UIBase ui = await OpenResourceUIAsync(UIType.LoggingMiningUI, _playerUIAnchors, _playerCamera);
            if (ui == null) return;
            if (ui is not LoggingMiningUI miniGameUI)
                throw new System.InvalidOperationException("LoggingMiningUI component is required.");
            miniGameUI.Bind(_viewModel);
            NotifyContentReady();
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
            CloseContent();
            RequestInteractionEnd();
        }
    }

    public override void StartPreparedInteraction()
    {
        _loggingMotionInProgress = false;
        _loggingResultPending = false;
        _viewModel.BeginGame();
    }

    public override void UpdateInteraction(float deltaTime)
    {
        if (_viewModel.UpdateRound(deltaTime))
        {
            FinishMiniGame();
        }
    }

    public override void CancelInteraction()
    {
        _loggingMotionInProgress = false;
        _loggingResultPending = false;
        _viewModel.Cancel();
        CloseContent();
    }

    public override void CloseContent()
    {
        CloseResourceUI();
    }

    public override void ConfirmInteraction()
    {
        if (_openUI == null || IsLogging)
            return;

        if (_viewModel.Confirm())
        {
            FinishMiniGame();
        }
    }

    public bool TryConfirmLoggingSwing()
    {
        if (!IsLogging || _openUI == null || !_viewModel.TryConfirmAndWaitForMotion()) return false;
        _loggingMotionInProgress = true;
        return true;
    }

    public bool ResumeLoggingJudgmentAtImpact()
    {
        // 마지막 결과 표시는 서버의 타격 시점 보상 확정 후 별도로 처리합니다.
        if (!IsLogging || _viewModel.CompletedRounds >= InteractionLoggingMiningVM.TotalRounds) return false;
        _viewModel.ResumeAfterMotion();
        return true;
    }

    public void CompleteLoggingSwing()
    {
        if (!IsLogging) return;
        _loggingMotionInProgress = false;
        if (_viewModel.ResumeAfterMotion() || _loggingResultPending)
        {
            _loggingResultPending = false;
            FinishMiniGame();
        }
    }

    public void ShowLoggingCompletionAtImpact()
    {
        if (!IsFinalLoggingJudgment) return;
        _loggingMotionInProgress = false;
        _loggingResultPending = false;
        FinishMiniGame();
    }

    private void FinishMiniGame()
    {
        // 회수 중 다음 라운드가 시간 초과되어도 마지막 모션은 자르지 않습니다.
        if (IsLogging && _loggingMotionInProgress)
        {
            _loggingResultPending = true;
            return;
        }
        float accuracy = _viewModel.FinalAccuracy;

        Debug.Log($"벌목·채광 종료: 평균 정확도 {accuracy:P1}", this);

        if (_openUI is LoggingMiningUI resultUI)
        {
            resultUI.ShowFinalResult();

            _openUI = null;
        }
        else
        {
            CloseContent();
        }

        if (accuracy <= 0f)
        {
            RequestInteractionEnd();
            return;
        }

        NotifyCompleted(accuracy);
    }

}
