using Cysharp.Threading.Tasks;
using UnityEngine;

public class InteractionFishingObject : InteractableObject
{
    [Header("회전 설정 — 현재는 테스트 값")]
    [SerializeField, Min(0.01f)]
    private float _rotationSpeed = 180f;

    private readonly InteractionFishingVM _viewModel = new();

    public override InteractionVMBase ViewModel => _viewModel;
    public override bool UsesMiniGameInput => true;

    private Transform[] _playerUIAnchors;
    private Camera _playerCamera;

    private int _uiRequestVersion;

    public override UniTask InitObject(string id)
    {
        _viewModel.Initialize(_rotationSpeed);

        return UniTask.CompletedTask;
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

        // TODO : 추후 equipmentData 또는 같은 ID의 ItemData에서
        // TODO : 장비 티어를 읽고 회전 속도를 계산할 위치.

        PlayContent();
    }

    protected override void PlayContent()
    {
        int requestVersion = ++_uiRequestVersion;
        OpenMiniGameUIAsync(requestVersion).Forget();
    }

    private async UniTask OpenMiniGameUIAsync(int requestVersion)
    {
        UIBase ui = await UIManager.Inst.OpenMainRootAsync(UIType.FishingUI);

        if (this == null || requestVersion != _uiRequestVersion)
        {
            if (ui != null)
                UIManager.Inst.Close(UIType.FishingUI);

            return;
        }

        if (ui is not FishingUI fishingUI)
        {
            Debug.LogError("FishingUI 생성 또는 컴포넌트를 확인해 주세요.", this);

            if (ui != null)
                UIManager.Inst.Close(UIType.FishingUI);

            RequestInteractionEnd();
            return;
        }

        _openUI = fishingUI;

        if (!fishingUI.TryGetComponent(out InteractionUIPositioner positioner))
        {
            Debug.LogError("FishingUI에 InteractionUIPositioner가 필요합니다.", fishingUI);

            CloseContent();
            RequestInteractionEnd();
            return;
        }

        positioner.SetTargets(_playerUIAnchors, _playerCamera);

        fishingUI.Bind(_viewModel);

        float startAngle = UnityEngine.Random.Range(0f, 360f);
        _viewModel.BeginGame(startAngle);
    }

    public override void UpdateInteraction(float deltaTime)
    {
        _viewModel.UpdateGame(deltaTime);
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

        UIManager.Inst.Close(UIType.FishingUI);
        _openUI = null;
    }

    public override void ConfirmInteraction()
    {
        if (_openUI == null)
            return;

        bool isFinished = _viewModel.Confirm();

        if (!isFinished)
            return;

        CloseContent();

        if (_viewModel.IsSucceeded)
        {
            Debug.Log("낚시 성공", this);
            NotifyCompleted();
        }
        else
        {
            Debug.Log("누적 실패 3회: 물고기를 놓쳤습니다.", this);
            RequestInteractionEnd();
        }
    }
}
