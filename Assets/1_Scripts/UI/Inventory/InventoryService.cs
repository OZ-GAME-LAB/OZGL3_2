using System.Collections.Generic;
using UnityEngine;

public class InventoryService
{
    private InventoryUIViewModel _inventoryViewModel;

    private int _nextSlotNumber = 1;

    public Dictionary<EquipmentSlot, string> GetEquipmentItemsId()
    {
        var inventory = GetInvantoryViewModel();

        return new Dictionary<EquipmentSlot, string>
        {
            [EquipmentSlot.Talisman] = inventory.TalismanItemViewModel?.Id,
            [EquipmentSlot.FishingRod] = inventory.FishingItemViewModel?.Id,
            [EquipmentSlot.Axe] = inventory.LoggingItemViewModel?.Id,
            [EquipmentSlot.PickAxe] = inventory.MiningItemViewModel?.Id
        };
    }

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
        var itemViewModel = CreateInventoryItemViewModel(data);

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

    public bool TryEquipItem(InventoryItemViewModel item)
    {
        if (item == null || string.IsNullOrEmpty(item.Id))
            return false;

        if (!DataManager.Inst.ReadData<EquipmentData>()
            .TryGetValue(item.Id, out var equipmentData))
        {
            return false;
        }

        var inventory = GetInvantoryViewModel();

        InventoryItemViewModel equippedItem;

        // 교체할 슬롯의 기존 장비 확인
        switch (equipmentData.EquipmentSlot)
        {
            case EquipmentSlot.Axe:
                equippedItem = inventory.LoggingItemViewModel;
                break;

            case EquipmentSlot.FishingRod:
                equippedItem = inventory.FishingItemViewModel;
                break;

            case EquipmentSlot.PickAxe:
                equippedItem = inventory.MiningItemViewModel;
                break;

            case EquipmentSlot.Talisman:
                equippedItem = inventory.TalismanItemViewModel;
                break;

            default:
                return false;
        }

        if (ReferenceEquals(equippedItem, item))
            return UnequipItem(item);

        // 기존 장비를 인벤토리에 반환
        if (!string.IsNullOrEmpty(equippedItem?.Id))
        {
            if (!AddItem(equippedItem.Id))
                return false;
        }

        // 새 장비 장착
        switch (equipmentData.EquipmentSlot)
        {
            case EquipmentSlot.Axe:
                inventory.LoggingItemViewModel = item;
                break;

            case EquipmentSlot.FishingRod:
                inventory.FishingItemViewModel = item;
                break;

            case EquipmentSlot.PickAxe:
                inventory.MiningItemViewModel = item;
                break;

            case EquipmentSlot.Talisman:
                inventory.TalismanItemViewModel = item;
                break;
        }

        return true;
    }
    
    public bool UnequipItem(InventoryItemViewModel item)
    {
        if (item == null || string.IsNullOrEmpty(item.Id))
            return false;

        var inventory = GetInvantoryViewModel();

        EquipmentSlot slot;

        if (ReferenceEquals(inventory.TalismanItemViewModel, item))
            slot = EquipmentSlot.Talisman;
        else if (ReferenceEquals(inventory.FishingItemViewModel, item))
            slot = EquipmentSlot.FishingRod;
        else if (ReferenceEquals(inventory.LoggingItemViewModel, item))
            slot = EquipmentSlot.Axe;
        else if (ReferenceEquals(inventory.MiningItemViewModel, item))
            slot = EquipmentSlot.PickAxe;
        else
            return false;

        if (!AddItem(item.Id))
            return false;

        switch (slot)
        {
            case EquipmentSlot.Talisman:
                inventory.TalismanItemViewModel = new InventoryItemViewModel();
                break;

            case EquipmentSlot.FishingRod:
                inventory.FishingItemViewModel = new InventoryItemViewModel();
                break;

            case EquipmentSlot.Axe:
                inventory.LoggingItemViewModel = new InventoryItemViewModel();
                break;

            case EquipmentSlot.PickAxe:
                inventory.MiningItemViewModel = new InventoryItemViewModel();
                break;
        }

        return true;
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

    private InventoryItemViewModel CreateInventoryItemViewModel(ItemData data)
    {
        var itemViewModel = new InventoryItemViewModel
        {

            Id = data.Id,
            Name = data.Name,
            ItemType = data.ItemType,
            Tier = data.Tier,
            Description = data.Description,
            MaxStack = data.MaxStack,
            Price = data.Price,
            SellingPrice = data.SellingPrice,
            IconPath = data.IconPath,
            PrefabPath = data.PrefabPath,
            Count = 1
        };
        return itemViewModel;
    }

    public bool IsEquippedItem(InventoryItemViewModel item)
    {
        if (item == null || string.IsNullOrEmpty(item.Id))
            return false;

        var inventory = GetInvantoryViewModel();

        return ReferenceEquals(inventory.TalismanItemViewModel, item)
            || ReferenceEquals(inventory.FishingItemViewModel, item)
            || ReferenceEquals(inventory.LoggingItemViewModel, item)
            || ReferenceEquals(inventory.MiningItemViewModel, item);
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