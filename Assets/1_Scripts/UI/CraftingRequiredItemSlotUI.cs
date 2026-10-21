using Cysharp.Threading.Tasks;
using System.ComponentModel;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CraftingRequiredItemSlotUI : UIBase
{
    [SerializeField] private Image Image_ItemIcon;
    [SerializeField] private TMP_Text Text_Count;

    private CraftingRequiredItemSlotViewModel _slotVM;

    private void OnDestroy()
    {
        Unbind();
    }

    public void BindReqSlotVM(CraftingRequiredItemSlotViewModel slotVm)
    {
        Unbind();
        _slotVM = slotVm;
        _slotVM.PropertyChanged += OnPropertyChanged_View;
        _slotVM.InvokeOnceOnInit();
    }

    public void Unbind()
    {
        if (_slotVM != null)
        {
            _slotVM.PropertyChanged -= OnPropertyChanged_View;
        }

        _slotVM = null;
    }

    private void OnPropertyChanged_View(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(CraftingRequiredItemSlotViewModel.ItemDataId):
                RefreshItemInfo();
                break;

            case nameof(CraftingRequiredItemSlotViewModel.RequiredCount):
            case nameof(CraftingRequiredItemSlotViewModel.OwnedCount):
                RefreshCount();
                break;

            case nameof(CraftingRequiredItemSlotViewModel.IsEnough):
                RefreshColor();
                break;
        }
    }

    private void RefreshItemInfo()
    {
        var itemData = DataManager.Inst.GetData<ItemData>(_slotVM.ItemDataId);

        LoadItemIconAsync(itemData.IconPath).Forget();
    }

    private async UniTaskVoid LoadItemIconAsync(string iconPath)
    {
        if (ResourceManager.Inst == null)
        {
            return;
        }

        Sprite loadedSprite = await ResourceManager.Inst.LoadSprite(iconPath);

        if (loadedSprite == null || Image_ItemIcon == null)
        {
            return;
        }

        Image_ItemIcon.sprite = loadedSprite;
    }

    private void RefreshCount()
    {
        Text_Count.text = $"{_slotVM.OwnedCount} / {_slotVM.RequiredCount}";
    }

    private void RefreshColor()
    {
        Text_Count.color = _slotVM.IsEnough ? Color.white : Color.red;
    }
}
