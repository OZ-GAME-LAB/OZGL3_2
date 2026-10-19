using System;
using System.ComponentModel;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventorySlotUIView : MonoBehaviour
{
    [SerializeField] private Button _useButton;
    [SerializeField] private TMP_Text _itemName;

    private InventoryUIViewModel _viewModel;
    public event Action<InventorySlotUIView, InventoryUIViewModel> ItemUsed;

    public void Bind(InventoryUIViewModel viewModel)
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
            ItemUsed?.Invoke(this, _viewModel);
    }
}