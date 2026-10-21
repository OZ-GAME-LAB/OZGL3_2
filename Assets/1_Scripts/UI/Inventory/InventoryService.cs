using System.Collections.Generic;
using UnityEngine;

public class InventoryService
{
    private readonly Dictionary<string, InventoryUIViewModel> _inventoryList = new();

    public IReadOnlyDictionary<string, InventoryUIViewModel> InventoryList => _inventoryList;

    public bool AddItem(string itemDataId)
    {
        if (!DataManager.Inst.ReadData<ItemData>().TryGetValue(itemDataId, out var data))
            return false;

        var itemViewModel = new InventoryUIViewModel
        {
            Id = data.Id,
            Name = data.Name,
            ItemType = data.ItemType,
            Description = data.Description,
            MaxStack = data.MaxStack,
            Price = data.Price,
            SellingPrice = data.SellingPrice,
            IconPath = data.IconPath,
            PrefabPath = data.PrefabPath
        };

        _inventoryList[itemDataId] = itemViewModel;
        return true;
    }

    public void BindInventoryView(InventoryUIView inventoryView)
    {
        if (inventoryView == null)
        {
            Debug.LogError("InventoryUIView가 null입니다.");
            return;
        }
        var inventoryViewModels = new List<InventoryUIViewModel>(_inventoryList.Values);
        inventoryView.Bind(inventoryViewModels);
    }

    //서비스를 가지고 있는 곳에서 실행시켜주기
    public void InitInventoryList()
    {
        _inventoryList.Clear();

        Dictionary<string, ItemData> dataTable = DataManager.Inst.ReadData<ItemData>();
        Debug.Log($"DataManager 아이템 수: {dataTable.Count}");


        foreach (var pair in dataTable)
        {
            ItemData data = pair.Value;

            var itemViewModel = new InventoryUIViewModel
            {
                Id = data.Id,
                Name = data.Name,
                ItemType = data.ItemType,
                Description = data.Description,
                MaxStack = data.MaxStack,
                Price = data.Price,
                SellingPrice = data.SellingPrice,
                IconPath = data.IconPath,
                PrefabPath = data.PrefabPath
            };

            _inventoryList.Add(itemViewModel.Id, itemViewModel);
        }
    }

    public void SetInventoryList(IEnumerable<InventoryUIViewModel> inventory)
    {
        _inventoryList.Clear();

        if (inventory == null)
            return;

        foreach (var itemViewModel in inventory)
        {
            if (itemViewModel == null)
                continue;

            _inventoryList[itemViewModel.Id] = itemViewModel;
        }
    }

    public bool TryGetItem(string itemDataId, out InventoryUIViewModel itemViewModel)
    {
        return _inventoryList.TryGetValue(itemDataId, out itemViewModel);
    }
}