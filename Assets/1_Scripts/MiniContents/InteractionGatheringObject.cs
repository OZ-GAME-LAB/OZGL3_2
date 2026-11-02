using Cysharp.Threading.Tasks;
using UnityEngine;

public class InteractionGatheringObject : InteractableObject
{
    private readonly InteractionGatheringVM _viewModel = new();
    public override InteractionVMBase ViewModel => _viewModel;

    private Transform[] _playerUIAnchors;
    private Camera _playerCamera;

    public override async UniTask InitObject(string id)
    {
        if (!DataManager.Inst.ReadData<FieldObjectData>().ContainsKey(id))
        {
            Debug.Log($"ID값 {id}은 InteractionGatheringData에 존재하지 않습니다.");
            return;
        }    


        _viewModel.InitGatheringVM(id);

        GameObject meshPrefab = await ResourceManager.Inst.InstantiateAsync(
            DataManager.Inst.ReadData<FieldObjectData>()[id].PrefabPath,
            this.transform,
            false,
            true);

        if(meshPrefab == null)
        {
            Debug.Log($"{id} ID 값을 가진 MeshPrefab이 생성되지 않았습니다.");
            return;
        }

        RegisterVisual(meshPrefab);
    }

    protected override void PlayContent()
    {
        OpenGatheringUIAsync().Forget();
    }

    private async UniTask OpenGatheringUIAsync()
    {
        UIBase ui = await UIManager.Inst.OpenMainRootAsync(UIType.GatheringUI);

        if (ui == null)
            return;

        if (ui is not GatheringUI gatheringUI)
        {
            Debug.LogError("GatheringUI 프리팹에 GatheringUI 컴포넌트가 필요합니다.");
            return;
        }

        _openUI = gatheringUI;
        gatheringUI.Bind(_viewModel);

        if (gatheringUI.TryGetComponent(out InteractionUIPositioner positioner))
        {
            positioner.SetTargets(_playerUIAnchors, _playerCamera);
        }
        else
        {
            Debug.LogError("GatheringUI에 InteractionUIPositioner가 필요합니다.");
        }
    }

    public override void BeginInteraction(Transform[] playerUIAnchors, Camera playerCamera)
    {
        _playerUIAnchors = playerUIAnchors;
        _playerCamera = playerCamera;

        _viewModel.BeginGathering();
        PlayContent();
    }

    public override void UpdateInteraction(float deltaTime)
    {
        if (_viewModel.UpdateGathering(deltaTime))
        {
            // 서버에 채집 완료 요청 및 처리 추가 필요
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
        if (_openUI == null)
            return;

        UIManager.Inst.Close(UIType.GatheringUI);
        _openUI = null;
    }


}
