using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class CraftingUITest : MonoBehaviour
{
    [SerializeField] private Button _openButton;

    private CraftingUI _craftingUI;
    private bool _isOpening;

    private void Awake()
    {
        if (_openButton == null)
        {
            Debug.LogError("제작 UI 테스트 버튼을 연결해 주세요.", this);
            return;
        }

        _openButton.onClick.AddListener(OnClickOpen);
    }

    private void OnClickOpen()
    {
        OpenAsync().Forget();
    }

    private async UniTask OpenAsync()
    {
        if (_isOpening)
        {
            return;
        }

        if (UIManager.Inst == null ||
            DataManager.Inst == null ||
            NetworkManager.Inst == null ||
            NetworkManager.Inst.CraftingService == null ||
            ResourceManager.Inst == null)
        {
            Debug.LogError(
                "UIManager, DataManager, NetworkManager의 CraftingService, " +
                "ResourceManager가 준비되어 있어야 합니다.",
                this);
            return;
        }

        _isOpening = true;
        _openButton.interactable = false;

        try
        {
            CraftingUI ui = await UIManager.Inst.OpenCraftingUIAsync();

            if (this == null)
            {
                return;
            }

            if (ui == null)
            {
                Debug.LogError("제작 UI를 열지 못했습니다.", this);
                return;
            }

            // 다시 열 때 이벤트가 중복 연결되지 않도록 정리
            if (_craftingUI != null)
            {
                _craftingUI.CloseRequested -= OnCloseRequested;
            }

            _craftingUI = ui;
            _craftingUI.CloseRequested += OnCloseRequested;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            if (this != null)
            {
                _isOpening = false;

                if (_openButton != null)
                {
                    _openButton.interactable = true;
                }
            }
        }
    }

    private void OnCloseRequested()
    {
        if (_craftingUI == null)
        {
            return;
        }

        _craftingUI.CloseRequested -= OnCloseRequested;
        _craftingUI = null;

        if (UIManager.Inst != null)
        {
            UIManager.Inst.CloseCraftingUI();
        }
    }

    private void OnDestroy()
    {
        if (_openButton != null)
        {
            _openButton.onClick.RemoveListener(OnClickOpen);
        }

        if (_craftingUI != null)
        {
            _craftingUI.CloseRequested -= OnCloseRequested;
        }
    }
}