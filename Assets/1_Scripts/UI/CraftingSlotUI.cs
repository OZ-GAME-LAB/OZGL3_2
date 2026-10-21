using Cysharp.Threading.Tasks;
using NUnit.Framework.Interfaces;
using System;
using System.ComponentModel;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CraftingSlotUI : UIBase
{
    [SerializeField] private Image Image_ItemIcon;
    [SerializeField] private TMP_Text Text_ItemName;
    [SerializeField] private Button Button_Selected;

    private CraftingSlotViewModel _slotVM;
    private Action<string> _onSelected;

    private void Awake()
    {
        Button_Selected.onClick.AddListener(OnClickSelected);
    }

    private void OnDestroy()
    {
        Unbind();
    }

    public void BindSlotVM(CraftingSlotViewModel slotVm, Action<string> onSelected)
    {
        Unbind();

        _slotVM = slotVm;
        _onSelected = onSelected;

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
        _onSelected = null;
    }

    private void OnPropertyChanged_View(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CraftingSlotViewModel.ItemDataId))
        {
            return;
        }

        if (_slotVM == null)
        {
            return;
        }

        var itemData = DataManager.Inst.GetData<ItemData>(_slotVM.ItemDataId);

        if (Text_ItemName != null)
        {
            Text_ItemName.text = itemData != null ? itemData.Name : "";
        }

        if (Image_ItemIcon != null)
        {
            Image_ItemIcon.sprite = null;
        }

        if (itemData == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(itemData.IconPath))
        {
            LoadItemIconAsync(itemData.IconPath).Forget();
        }
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

    private void OnClickSelected()
    {
        if (_slotVM == null)
        {
            return;
        }

        _onSelected?.Invoke(_slotVM.RecipeDataId);
    }

}
