using Cysharp.Threading.Tasks;
using UnityEngine;

public class InteractionLoggingMiningObject : InteractableObject
{
    [Header("수축 설정 — 현재는 테스트 값")]
    [SerializeField] private float _startSizeRatio = 3f;
    [SerializeField] private float _endSizeRatio = 0.5f;
    [SerializeField] private float _shrinkSpeed = 1f;

    [Header("판정 설정")]
    [SerializeField] private float _successStartRatio = 2f;
    [SerializeField] private float _perfectTolerance = 0.05f;

    [Header("난이도 설정")]
    [SerializeField, Min(0f)]
    private float _increasePerTier = 0.25f;

    private int _targetTier;

    private readonly InteractionLoggingMiningVM _viewModel = new();

    public override InteractionVMBase ViewModel => _viewModel;

    private Transform[] _playerUIAnchors;
    private Camera _playerCamera;

    private int _uiRequestVersion;

    public override bool UsesMiniGameInput => true;

    public override async UniTask InitObject(string id)
    {
        FieldObjectData data = DataManager.Inst.GetData<FieldObjectData>(id);

        if (data == null)
            return;

        _targetTier = data.Tier;

        _viewModel.Initialize(_startSizeRatio, _endSizeRatio, _shrinkSpeed, _successStartRatio, _perfectTolerance);

        GameObject visual = await ResourceManager.Inst.InstantiateAsync(
            data.PrefabPath,
            transform,
            false,
            true);

        if (visual == null)
        {
            Debug.LogError($"외형 생성 실패: {id}", this);
            return;
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
        int requestVersion = ++_uiRequestVersion;
        OpenMiniGameUIAsync(requestVersion).Forget();
    }

    private async UniTask OpenMiniGameUIAsync(int requestVersion)
    {
        UIBase ui = await UIManager.Inst.OpenMainRootAsync(UIType.LoggingMiningUI);

        if (this == null || requestVersion != _uiRequestVersion)
        {
            if (ui != null)
                UIManager.Inst.Close(UIType.LoggingMiningUI);

            return;
        }

        if (ui is not LoggingMiningUI miniGameUI)
        {
            Debug.LogError("LoggingMiningUI 생성 또는 컴포넌트를 확인해 주세요.", this);

            if (ui != null)
                UIManager.Inst.Close(UIType.LoggingMiningUI);

            RequestInteractionEnd();
            return;
        }

        _openUI = miniGameUI;

        if (!miniGameUI.TryGetComponent(out InteractionUIPositioner positioner))
        {
            Debug.LogError("InteractionUIPositioner가 필요합니다.", miniGameUI);
            RequestInteractionEnd();
            return;
        }

        positioner.SetTargets(_playerUIAnchors, _playerCamera);

        miniGameUI.Bind(_viewModel);

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
        _viewModel.Cancel();
        CloseContent();
    }

    public override void CloseContent()
    {
        _uiRequestVersion++;

        if (_openUI == null)
            return;

        UIManager.Inst.Close(UIType.LoggingMiningUI);
        _openUI = null;
    }

    public override void ConfirmInteraction()
    {
        if (_openUI == null)
            return;

        if (_viewModel.Confirm())
        {
            FinishMiniGame();
        }
    }

    private void FinishMiniGame()
    {
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

    private void CompleteAfterResult()
    {
        float accuracy = _viewModel.FinalAccuracy;

        CloseContent();

        if (accuracy <= 0f)
        {
            RequestInteractionEnd();
            return;
        }

        NotifyCompleted(accuracy);
    }
}
