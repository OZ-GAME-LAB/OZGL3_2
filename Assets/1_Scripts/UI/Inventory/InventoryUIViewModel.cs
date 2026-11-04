using ObservableCollections;
using System.Collections.Generic;

public class InventoryUIViewModel : ViewModelBase
{

    public void InvokeOnceOnInit()
    {
        OnPropertyChanged(nameof(InventoryItemViewModelDictionary));
    }


    private ObservableDictionary<string, InventoryItemViewModel> _inventoryItemViewModelDictionary;

    public ObservableDictionary<string, InventoryItemViewModel> InventoryItemViewModelDictionary
    {
        get => _inventoryItemViewModelDictionary;
        set
        {
            if (_inventoryItemViewModelDictionary == value)
            {
                return;
            }

            if (_inventoryItemViewModelDictionary != null)
            {
                _inventoryItemViewModelDictionary.CollectionChanged -= OnInventoryChanged;
            }

            _inventoryItemViewModelDictionary = value;

            if (_inventoryItemViewModelDictionary != null)
            {
                _inventoryItemViewModelDictionary.CollectionChanged += OnInventoryChanged;
            }

            OnPropertyChanged(nameof(InventoryItemViewModelDictionary));
        }
    }
    private void OnInventoryChanged(in NotifyCollectionChangedEventArgs<KeyValuePair<string, InventoryItemViewModel>> e)
    {
        OnPropertyChanged(nameof(InventoryItemViewModelDictionary));
    }
}
