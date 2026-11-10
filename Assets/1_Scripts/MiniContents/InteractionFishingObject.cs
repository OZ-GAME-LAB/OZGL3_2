using Cysharp.Threading.Tasks;
using UnityEngine;

public class InteractionFishingObject : InteractableObject
{
    [Header("회전 설정 — 현재는 테스트 값")]
    [SerializeField, Min(0.01f)]
    private float _rotationSpeed = 180f;

    [Header("난이도 설정")]
    [SerializeField, Min(0f)]
    private float _increasePerTier = 0.25f;

    private string _selectedFishId;

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

        if (!TryGetInteractionEquipmentData(out _))
        {
            RequestInteractionEnd();
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedFishId))
        {
            Debug.LogWarning("서버에서 전달받은 물고기 ID가 없습니다.", this);

            RequestInteractionEnd();
            return;
        }

        ItemData fishData = DataManager.Inst.GetData<ItemData>(_selectedFishId);

        ItemData equipmentItem = DataManager.Inst.GetData<ItemData>(InteractionEquipmentId);

        if (fishData == null || equipmentItem == null)
        {
            Debug.LogWarning("물고기 또는 장비의 아이템 데이터를 확인해 주세요.", this);

            RequestInteractionEnd();
            return;
        }

        if (fishData.Tier < 1 || equipmentItem.Tier < 1)
        {
            Debug.LogWarning($"티어는 1 이상이어야 합니다. 물고기: {fishData.Tier}, 장비: {equipmentItem.Tier}", this);

            RequestInteractionEnd();
            return;
        }

        float multiplier = GameUtil.CalculateInteractionDifficultyMultiplier(fishData.Tier, equipmentItem.Tier, _increasePerTier);

        float rotationSpeed = _rotationSpeed * multiplier;

        _viewModel.Initialize(rotationSpeed);

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

    public void SetSelectedFishId(string fishId)
    {
        _selectedFishId = fishId;
    }
}
