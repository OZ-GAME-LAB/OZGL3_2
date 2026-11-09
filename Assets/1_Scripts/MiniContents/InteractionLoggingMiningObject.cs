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

        if (!TryGetInteractionEquipmentData(out EquipmentData equipmentData))
        {
            RequestInteractionEnd();
            return;
        }

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
