using Cysharp.Threading.Tasks;
using UnityEngine;

public class InteractionGatheringObject : InteractableObject
{
    private readonly InteractionGatheringVM _viewModel = new();
    public override InteractionVMBase ViewModel => _viewModel;
    public override bool RequiresContentReady => true;

    private Transform[] _playerUIAnchors;
    private Camera _playerCamera;

    public override async UniTask InitObject(string id)
    {
        if (!DataManager.Inst.ReadData<FieldObjectData>().ContainsKey(id))
        {
            throw new System.InvalidOperationException("채집 데이터가 없습니다: " + id);
        }    


        _viewModel.InitGatheringVM(id);

        if (UsesPlacedVisual)
            return;

        GameObject meshPrefab = await ResourceManager.Inst.InstantiateAsync(
            DataManager.Inst.ReadData<FieldObjectData>()[id].PrefabPath,
            this.transform,
            false,
            true);

        if(meshPrefab == null)
        {
            throw new System.InvalidOperationException("채집 외형 생성 실패: " + id);
        }

        RegisterVisual(meshPrefab);
    }

    protected override void PlayContent()
    {
        OpenMiniGameUIAsync().Forget();
    }

    private async UniTask OpenMiniGameUIAsync()
    {
        try
        {
            UIBase ui = await OpenResourceUIAsync(UIType.GatheringUI, _playerUIAnchors, _playerCamera);
            if (ui == null) return;
            if (ui is not GatheringUI gatheringUI)
                throw new System.InvalidOperationException("GatheringUI component is required.");
            gatheringUI.Bind(_viewModel);
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
        if (_openUI is not GatheringUI gatheringUI)
            throw new System.InvalidOperationException("채집 UI가 준비되지 않았습니다.");
        _viewModel.BeginGathering();
        gatheringUI.ShowProgress();
    }

    public override void BeginInteraction(Transform[] playerUIAnchors, Camera playerCamera, string equipmentId)
    {
        base.BeginInteraction(playerUIAnchors, playerCamera, equipmentId);

        _playerUIAnchors = playerUIAnchors;
        _playerCamera = playerCamera;

        PlayContent();
    }

    public override void UpdateInteraction(float deltaTime)
    {
        if (_viewModel.UpdateGathering(deltaTime))
        {
            // 완료는 VM에서 한 번만 반환하며, 기존 서버 요청으로 보상을 처리합니다.
            NotifyCompleted();

            CloseContent();
        }
    }

    public override void CancelInteraction()
    {
        _viewModel.CancelGathering();
        CloseContent();
    }

    public override void CloseContent()
    {
        CloseResourceUI();
    }


}
