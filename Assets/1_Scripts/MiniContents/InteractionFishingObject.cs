using Cysharp.Threading.Tasks;
using UnityEngine;

public class InteractionFishingObject : InteractableObject
{
    [Header("회전 설정 — 현재는 테스트 값")]
    [SerializeField, Min(0.01f)]
    private float _rotationSpeed = 360f;

    [Header("판정 영역")]
    [SerializeField, Range(1f, 90f)] private float _successZoneAngle = 54f;
    [SerializeField, Range(1f, 10f)] private float _pointerAngleWidth = 4f;

    [Header("난이도 설정")]
    [SerializeField, Min(0f)]
    private float _increasePerTier = 0.25f;

    private string _selectedFishId;

    private readonly InteractionFishingVM _viewModel = new();

    public override InteractionVMBase ViewModel => _viewModel;
    public override bool RequiresContentReady => true;
    public override bool UsesMiniGameInput => true;

    private Transform[] _playerUIAnchors;
    private Camera _playerCamera;



    public override UniTask InitObject(string id)
    {
        _viewModel.Initialize(_rotationSpeed, _successZoneAngle, _pointerAngleWidth);

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

        _viewModel.Initialize(rotationSpeed, _successZoneAngle, _pointerAngleWidth);

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
            UIBase ui = await OpenResourceUIAsync(UIType.FishingUI, _playerUIAnchors, _playerCamera);
            if (ui == null) return;
            if (ui is not FishingUI fishingUI)
                throw new System.InvalidOperationException("FishingUI component is required.");
            fishingUI.Bind(_viewModel);
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
        if (_openUI is not FishingUI fishingUI)
            throw new System.InvalidOperationException("낚시 판정 UI가 준비되지 않았습니다.");
        _viewModel.BeginGame();
        fishingUI.ShowJudgment();
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
        CloseResourceUI();
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
            Debug.Log("경고 상태에서 다시 실패하여 물고기를 놓쳤습니다.", this);
            RequestInteractionEnd();
        }
    }

    public void SetSelectedFishId(string fishId)
    {
        _selectedFishId = fishId;
    }
}
