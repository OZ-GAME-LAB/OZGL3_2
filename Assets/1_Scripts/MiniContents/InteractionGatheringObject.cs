using Cysharp.Threading.Tasks;
using UnityEngine;

public class InteractionGatheringObject : InteractableObject
{
    private readonly InteractionGatheringVM _viewModel = new();

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
    }

    public override void BeginInteraction()
    {
        // 필요한 초기화 및 서버 승인 이후 실행
        _viewModel.BeginGathering();
        PlayContent();
    }

    public override void UpdateInteraction(float deltaTime)
    {
        if (_viewModel.UpdateGathering(deltaTime))
        {
            // 서버에 채집 완료 요청 및 처리 추가 필요

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
