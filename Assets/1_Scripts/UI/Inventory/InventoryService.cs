using System.Collections.Generic;
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
        if (item == null || _inventoryViewModel?.InventoryItemViewModelDictionary == null)
            return false;

        foreach (var pair in _inventoryViewModel.InventoryItemViewModelDictionary)
        {
            // 전달된 객체와 딕셔너리에 저장된 객체가 같은 인스턴스인지 확인
            if (!ReferenceEquals(pair.Value, item))
                continue;

            if (pair.Value.Count > 1)
            {
                pair.Value.Count--;
            }
            else
            {
                _inventoryViewModel.InventoryItemViewModelDictionary.Remove(pair.Key);
            }

            return true;
        }

        // 이 서비스의 인벤토리에 없는 아이템
        return false;
    }

    public void BindInventoryView(InventoryUIView inventoryView)
    {
        if (inventoryView == null)
        {
            Debug.LogError("InventoryUIView가 null입니다.");
            return;
        }
        GetInvantoryViewModel();
        InitTestInventoryItem();
        inventoryView.Bind(_inventoryViewModel);
    }

    //서비스를 가지고 있는 곳에서 실행시켜주기
    public void InitTestInventoryItem()
    {

        Dictionary<string, ItemData> dataTable = DataManager.Inst.ReadData<ItemData>();
        Debug.Log($"DataManager 아이템 수: {dataTable.Count}");

        foreach (var pair in dataTable)
        {
            AddItem(pair.Value.Id);
        }
    }
    public bool TryGetItem(string itemDataId, out InventoryItemViewModel itemViewModel)
    {
        return _inventoryViewModel.InventoryItemViewModelDictionary.TryGetValue(itemDataId, out itemViewModel);
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
            InventoryItemViewModelDictionary = new()
        };
        return inventoryViewModel;
    }
}