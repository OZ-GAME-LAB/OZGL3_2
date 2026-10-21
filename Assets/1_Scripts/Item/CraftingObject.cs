using Cysharp.Threading.Tasks;
using UnityEngine;

public class CraftingObject : InteractableObject
{
    private readonly InteractionCraftingVM _viewModel = new InteractionCraftingVM();

    public override InteractionVMBase ViewModel => _viewModel;

    private CraftingUI _craftingUI;
    private bool _sessionActive;
    private int _openVersion;
    public override bool RequiresServerReservation => false;

    public override async UniTask InitObject(string id)
    {
    }

    public override void BeginInteraction(Transform[] playerUIAnchors, Camera playerCamera)
    {
        if (isActiveAndEnabled == false || _sessionActive)
        {
            return;
        }

        _sessionActive = true;
        int version = ++_openVersion;

        OpenCraftingAsync(version).Forget();
    }

    private async UniTask OpenCraftingAsync(int version)
    {
        if (UIManager.Inst == null)
        {
            Debug.LogError("UIManager가 없습니다.", this);
            RequestInteractionEnd();
            return;
        }

        CraftingUI ui = await UIManager.Inst.OpenCraftingUIAsync();

        // 로딩 중 취소되거나 대상이 사라진 경우
        if (this == null ||
            !_sessionActive ||
            version != _openVersion)
        {
            // 주의: 공용 UI의 소유권 처리가 추가로 필요함.
            // 여기서 무조건 닫으면 다른 제작대의 UI를 닫을 수 있음.
            return;
        }

        if (ui == null)
        {
            RequestInteractionEnd();
            return;
        }

        _craftingUI = ui;
        _openUI = ui;

        _craftingUI.CloseRequested += OnCloseRequested;
    }

    private void OnCloseRequested()
    {
        RequestInteractionEnd();
    }

    public override void CancelInteraction()
    {
        CloseContent();
    }

    public override void CloseContent()
    {
        _sessionActive = false;
        _openVersion++;

        if (_craftingUI == null)
        {
            return;
        }

        _craftingUI.CloseRequested -= OnCloseRequested;

        if (UIManager.Inst != null)
        {
            UIManager.Inst.CloseCraftingUI();
        }

        _craftingUI = null;
        _openUI = null;
    }
}
