using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class InventoryService
{
    private InventoryUIViewModel _inventoryViewModel;

    private int _nextSlotNumber = 1;

   
    public bool AddItem(string itemDataId)
    {
        if (!DataManager.Inst.ReadData<ItemData>().TryGetValue(itemDataId, out var data))
        {
            return false;
        }


        if(_inventoryViewModel.InventoryItemViewModelDictionary != null)
        {
            foreach (var pair in _inventoryViewModel.InventoryItemViewModelDictionary)
            {
                var existingItem = pair.Value;

                if (existingItem.Id != data.Id)
                    continue;

                if (existingItem.Count >= existingItem.MaxStack)
                    return false;

                existingItem.Count++;
                return true;
            }
        }
        var itemViewModel = new InventoryItemViewModel
        {
            Id = data.Id,
            Name = data.Name,
            ItemType = data.ItemType,
            Description = data.Description,
            MaxStack = data.MaxStack,
            Price = data.Price,
            SellingPrice = data.SellingPrice,
            IconPath = data.IconPath,
            PrefabPath = data.PrefabPath,
            Count = 1
        };

        string slotKey = _nextSlotNumber.ToString();
        _inventoryViewModel.InventoryItemViewModelDictionary.Add(slotKey, itemViewModel);
        _nextSlotNumber++;
        
        return true;
    }
    public bool RemoveItem(InventoryItemViewModel item)
    {
        var items = _inventoryViewModel?.InventoryItemViewModelDictionary;
        if (item == null || items == null)
            return false;

        string key = null;

        foreach (var pair in items)
        {
            if (ReferenceEquals(pair.Value, item))
            {
                key = pair.Key;
                break;
            }
        }

        if (key == null)
            return false;

        if (item.Count > 1)
        {
            item.Count--;

        }
        else
        {
            items.Remove(key);
            Debug.Log($"{item.Name}아이템 제거함");
        }

        return true;
    }

    public void UseItem(InventoryItemViewModel item)
    {
        Debug.Log($"{item.Name}아이템 사용됨");
    }

    public void BindInventoryView(InventoryUIView inventoryView)
    {
        if (inventoryView == null)
        {
            Debug.LogError("InventoryUIView가 null입니다.");
            return;
        }
        
        
        inventoryView.Bind(_inventoryViewModel);
    }

    //서비스를 가지고 있는 곳에서 실행시켜주기
    public void InitTestInventoryItem()
    {
        GetInvantoryViewModel();
        Dictionary<string, ItemData> dataTable = DataManager.Inst.ReadData<ItemData>();
        Debug.Log($"DataManager 아이템 수: {dataTable.Count}");

        foreach (var pair in dataTable)
        {
            AddItem(pair.Value.Id);
        }
    }
    public bool TryGetItem(string itemId, out InventoryItemViewModel itemViewModel)
    {
        return _inventoryViewModel.InventoryItemViewModelDictionary.TryGetValue(itemId, out itemViewModel);
    }

    private InventoryUIViewModel GetInvantoryViewModel()
    {
        if( _inventoryViewModel == null)
        {
            _inventoryViewModel = CreateInvantoryViewModel();
        }

        return _inventoryViewModel;
    }

    private InventoryUIViewModel CreateInvantoryViewModel()
    {
        var inventoryViewModel = new InventoryUIViewModel
        {
            TalismanItemViewModel = new(),
            FishingItemViewModel = new(),
            LoggingItemViewModel = new(),
            MiningItemViewModel = new(),
            InventoryItemViewModelDictionary = new()
        };
        return inventoryViewModel;
    }

    //인벤토리 아이템들을 반환하는 메서드 추가
    public IEnumerable<InventoryItemViewModel> GetInventoryItems()
    {
        var viewModel = GetInvantoryViewModel();
        var items = viewModel.InventoryItemViewModelDictionary;

        if (items == null)
        {
            yield break;
        }

        foreach (var pair in items)
        {
            if (pair.Value != null)
            {
                yield return pair.Value;
            }
        }
    }
}