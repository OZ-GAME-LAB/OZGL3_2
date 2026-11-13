using Cysharp.Threading.Tasks;
using System;
using System.ComponentModel;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventorySlotUIView : MonoBehaviour
{
    [SerializeField] private Button _useButton;
    [SerializeField] private TMP_Text _itemName;
    [SerializeField] private TMP_Text _itemCount;
    [SerializeField] private Image Image_ItemIcon;

    private InventoryItemViewModel _viewModel;
    public event Action<InventorySlotUIView, InventoryItemViewModel> ItemOnClickEvent;

    public void Bind(InventoryItemViewModel viewModel)
    {
        Unbind();

        _viewModel = viewModel;

        if (_viewModel != null)
            _viewModel.PropertyChanged += OnItemPropertyChanged;
        _viewModel.InvokeOnceOnInit();

    }

    private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(_viewModel.Name):
                {
                    _itemName.text = _viewModel.Name;

                    break;
                }
            case nameof(_viewModel.Count):
                {
                    if(_viewModel.ItemType == nameof(ItemType.Equipment) || _viewModel.Count == 0){
                        _itemCount.text = null;
                    }
                    else
                    {
                        _itemCount.text = _viewModel.Count.ToString();

                    }

                    break;
                }
            case nameof(_viewModel.IconPath):
                {
                    Image_ItemIcon.sprite = null;

                    if (!string.IsNullOrEmpty(_viewModel.IconPath))
                        LoadItemIconAsync(_viewModel.IconPath).Forget();

                    break;
                }
        }

        
    }



    private void OnEnable()
    {
        if (_useButton != null)
            _useButton.onClick.AddListener(OnClickUseButton);
    }

    private void OnDisable()
    {
        if (_useButton != null)
            _useButton.onClick.RemoveListener(OnClickUseButton);
    }

    private void OnDestroy()
    {
        Unbind();

    }

    private void Unbind()
    {
        if (_viewModel != null)
            _viewModel.PropertyChanged -= OnItemPropertyChanged;

        _viewModel = null;
    }

    private void OnClickUseButton()
    {
        if (_viewModel != null)
            ItemOnClickEvent?.Invoke(this, _viewModel);
    }



    private async UniTaskVoid LoadItemIconAsync(string iconPath)
    {
        await GameUtil.LoadAndSetSpriteImage(Image_ItemIcon, _viewModel.IconPath);

    }
}