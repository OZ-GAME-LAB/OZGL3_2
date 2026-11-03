using Cysharp.Threading.Tasks;
using UnityEngine;

public class InteractionLoggingMiningObject : InteractableObject
{
    [Header("수축 설정 — 현재는 테스트 값")]
    [SerializeField] private float _startSizeRatio = 3f;
    [SerializeField] private float _endSizeRatio = 0.5f;
    [SerializeField] private float _shrinkSpeed = 1f;

    private readonly InteractionLoggingMiningVM _viewModel = new();

    public override InteractionVMBase ViewModel => _viewModel;

    private Transform[] _playerUIAnchors;
    private Camera _playerCamera;

    private int _uiRequestVersion;

    public override async UniTask InitObject(string id)
    {
        FieldObjectData data = DataManager.Inst.GetData<FieldObjectData>(id);

        if (data == null)
            return;

        _viewModel.Initialize(_startSizeRatio, _endSizeRatio, _shrinkSpeed);

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

    public override void BeginInteraction(Transform[] playerUIAnchors, Camera playerCamera)
    {
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

        _viewModel.BeginRound();
    }

    public override void UpdateInteraction(float deltaTime)
    {
        _viewModel.UpdateRound(deltaTime);
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
}
