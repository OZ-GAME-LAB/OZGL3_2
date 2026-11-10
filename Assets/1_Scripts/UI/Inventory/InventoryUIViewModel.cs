using ObservableCollections;
using System.Collections.Generic;

public class InventoryUIViewModel : ViewModelBase
{

    public void InvokeOnceOnInit()
    {
        OnPropertyChanged(nameof(TalismanItemViewModel));
        OnPropertyChanged(nameof(FishingItemViewModel));
        OnPropertyChanged(nameof(LoggingItemViewModel));
        OnPropertyChanged(nameof(MiningItemViewModel));
        OnPropertyChanged(nameof(InventoryItemViewModelDictionary));
    }


    private InventoryItemViewModel _talismanItemViewModel;


    public InventoryItemViewModel TalismanItemViewModel
    {
        get => _talismanItemViewModel;
        set
        {
            if (_talismanItemViewModel != value)
            {
                _talismanItemViewModel = value;
                OnPropertyChanged(nameof(TalismanItemViewModel));
            }
        }
    }


    private InventoryItemViewModel _fishingItemViewModel;


    public InventoryItemViewModel FishingItemViewModel
    {
        get => _fishingItemViewModel;
        set
        {
            if (_fishingItemViewModel != value)
            {
                _fishingItemViewModel = value;
                OnPropertyChanged(nameof(FishingItemViewModel));
            }
        }
    }


    private InventoryItemViewModel _loggingItemViewModel;

    public InventoryItemViewModel LoggingItemViewModel
    {
        get => _loggingItemViewModel;
        set
        {
            if (_loggingItemViewModel != value)
            {
                _loggingItemViewModel = value;
                OnPropertyChanged(nameof(LoggingItemViewModel));
            }
        }
    }

    private InventoryItemViewModel _miningItemViewModel;

    public InventoryItemViewModel MiningItemViewModel
    {
        get => _miningItemViewModel;
        set
        {
            if (_miningItemViewModel != value)
            {
                _miningItemViewModel = value;
                OnPropertyChanged(nameof(MiningItemViewModel));
            }
        }
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
