using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public partial class InventoryService
{
    private InventoryUIViewModel _inventoryViewModel;

    private readonly Dictionary<InventoryItemViewModel, string> _saveItemIds = new();

    private int _nextSlotNumber = 1;
    private InventoryItemViewModel _pendingUseItem;

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

    public string GetEquipmentItemId(EquipmentSlot slot)
    {
        var inventory = GetInvantoryViewModel();

        return slot switch
        {
            EquipmentSlot.Talisman => inventory.TalismanItemViewModel?.Id,
            EquipmentSlot.FishingRod => inventory.FishingItemViewModel?.Id,
            EquipmentSlot.Axe => inventory.LoggingItemViewModel?.Id,
            EquipmentSlot.PickAxe => inventory.MiningItemViewModel?.Id,
            _ => null
        };
    }

    public bool AddItem(string itemDataId)
    {
        GetInvantoryViewModel();
        if (!DataManager.Inst.ReadData<ItemData>().TryGetValue(itemDataId, out var data))
        {
            return false;
        }

        if (IsRuneItem(data))
            return AddRuneItem(data);

        if (_inventoryViewModel.InventoryItemViewModelDictionary != null)
        {
            foreach (var pair in _inventoryViewModel.InventoryItemViewModelDictionary)
            {
                var existingItem = pair.Value;

                if (existingItem.Id != data.Id)
                    continue;

                if (existingItem.Count >= existingItem.MaxStack || existingItem == _dropReservation)
                    continue;

                existingItem.Count++;
                return true;
            }
        }

        if (_inventoryViewModel.InventoryItemViewModelDictionary.Count >= _inventoryViewModel.InventoryMaxSlotNumber)
        {
            Debug.Log($"인벤토리 갯수가 {_inventoryViewModel.InventoryMaxSlotNumber}개를 초과했습니다");

            return false;
        }

        var itemViewModel = CreateInventoryItemViewModel(data);

        string slotKey = _nextSlotNumber.ToString();
        _inventoryViewModel.InventoryItemViewModelDictionary.Add(slotKey, itemViewModel);
        _nextSlotNumber++;

        return true;
    }

    internal bool TryCraft(Dictionary<string, int> materials, ItemData result)
    {
        if (result == null || result.MaxStack < 1 || materials.Count == 0)
            return false;

        var bag = GetInvantoryViewModel();
        var remaining = new Dictionary<string, int>(materials);
        var next = new ObservableCollections.ObservableDictionary<string, InventoryItemViewModel>();
        var changes = new Dictionary<InventoryItemViewModel, int>();
        InventoryItemViewModel outputStack = null;
        bool isRune = IsRuneItem(result);
        foreach (var pair in bag.InventoryItemViewModelDictionary)
        {
            var item = pair.Value;
            int count = item.Count;
            if (item != _dropReservation && !IsEquippedItem(item) && !IsItemUsePending(item.Id) && remaining.TryGetValue(item.Id, out int needed))
            {
                int used = Math.Min(needed, count);
                count -= used;
                if (used == needed) remaining.Remove(item.Id);
                else remaining[item.Id] = needed - used;
                changes[item] = count;
            }

            if (count == 0) continue;
            next.Add(pair.Key, item);
            if (outputStack == null && item != _dropReservation && item.Id == result.Id && count < item.MaxStack)
                outputStack = item;
        }
        if (remaining.Count != 0)
            return false;

        if (isRune)
        {
            foreach (var rune in _runeItems)
                if (rune.Id == result.Id) { outputStack = rune; break; }
            if (outputStack != null && outputStack.Count >= outputStack.MaxStack)
                return false;
        }
        if (outputStack != null)
        {
            changes[outputStack] = changes.TryGetValue(outputStack, out int after) ? after + 1 : outputStack.Count + 1;
        }
        else
        {
            if (!isRune && next.Count >= bag.InventoryMaxSlotNumber)
                return false;
            var item = CreateInventoryItemViewModel(result);
            if (isRune) _runeItems.Add(item);
            else next.Add((_nextSlotNumber++).ToString(), item);
        }

        bool slotsChanged = false;
        foreach (var change in changes)
        {
            change.Key.SetCountWithoutNotification(change.Value);
            if (change.Value != 0) continue;
            _saveItemIds.Remove(change.Key);
            for (int i = 0; i < QuickSlotCount; i++)
            {
                if (!ReferenceEquals(_quickSlotItems[i], change.Key)) continue;
                _quickSlotItems[i] = null;
                slotsChanged = true;
            }
        }
        bag.InventoryItemViewModelDictionary = next;
        foreach (var item in changes.Keys) item.NotifyCountChanged();
        if (slotsChanged) QuickSlotsChanged?.Invoke();
        if (isRune) RuneInventoryChanged?.Invoke();
        return true;
    }

    public bool RemoveItem(string itemId)
    {
        var items = _inventoryViewModel?.InventoryItemViewModelDictionary;

        if (string.IsNullOrEmpty(itemId) || items == null || IsItemUsePending(itemId))
            return false;

        InventoryItemViewModel targetItem = null;

        foreach (var pair in items)
        {
            if (pair.Value != null && pair.Value.Id == itemId)
            {
                targetItem = pair.Value;
                break;
            }
        }

        return RemoveItem(targetItem);
    }

    // 같은 ID의 저장 스택이 여러 개여도 선택한 객체만 차감한다.
    public bool RemoveItem(InventoryItemViewModel targetItem)
    {
        if (targetItem == null || targetItem == _dropReservation || IsItemUsePending(targetItem.Id))
            return false;

        var items = _inventoryViewModel?.InventoryItemViewModelDictionary;
        if (items == null)
            return false;

        string slotKey = null;
        foreach (var pair in items)
        {
            if (ReferenceEquals(pair.Value, targetItem))
            {
                slotKey = pair.Key;
                break;
            }
        }

        if (slotKey == null)
            return false;

        if (targetItem.Count > 1)
        {
            targetItem.Count--;
        }
        else
        {
            ClearQuickSlotItem(targetItem);
            items.Remove(slotKey);
            _saveItemIds.Remove(targetItem);
            Debug.Log($"{targetItem.Name}아이템 제거함");
        }

        return true;
    }

    public bool CanUseItem(InventoryItemViewModel item)
    {
        return item != null && item.Count > 0 && !string.IsNullOrWhiteSpace(item.Id)
            && DataManager.Inst != null
            && DataManager.Inst.ReadData<ConsumableData>().TryGetValue(item.Id, out ConsumableData data)
            && data != null && data.EffectType == nameof(ConsumableEffectType.RestoreHp)
            && !float.IsNaN(data.EffectValue) && !float.IsInfinity(data.EffectValue)
            && data.EffectValue != 0f;
    }

    public bool IsItemUsePending(string itemId)
    {
        return _pendingUseItem != null && _pendingUseItem.Id == itemId;
    }

    // 반환값은 요청 접수 여부입니다. 수량은 서버의 성공 응답을 받은 뒤에만 차감합니다.
    public bool RequestUseItem(InventoryItemViewModel item)
    {
        if (_pendingUseItem != null || item == _dropReservation || !CanUseItem(item) || !OwnsBagItem(item)
            || ServiceManager.Inst == null || ServiceManager.Inst.PlayerService == null)
            return false;

        _pendingUseItem = item;
        if (!ServiceManager.Inst.PlayerService.RequestUseConsumable(item.Id, CompleteItemUse))
        {
            _pendingUseItem = null;
            return false;
        }

        return true;
    }

    private bool OwnsBagItem(InventoryItemViewModel item)
    {
        var items = _inventoryViewModel?.InventoryItemViewModelDictionary;
        if (items == null)
            return false;

        foreach (var pair in items)
        {
            if (ReferenceEquals(pair.Value, item))
                return true;
        }

        return false;
    }

    private void CompleteItemUse(bool applied)
    {
        InventoryItemViewModel item = _pendingUseItem;
        _pendingUseItem = null;
        if (!applied || item == null || !OwnsBagItem(item))
            return;

        if (RemoveItem(item))
            Debug.Log($"{item.Name}아이템 사용됨");
    }

    public void BindInventoryView(InventoryUIView inventoryView)
    {
        if (inventoryView == null)
        {
            Debug.LogError("InventoryUIView가 null입니다.");
            return;
        }

        inventoryView.Bind(GetInvantoryViewModel());
    }

    public bool TryEquipItem(InventoryItemViewModel item)
    {
        if (item == null || item == _dropReservation || string.IsNullOrEmpty(item.Id) || IsItemUsePending(item.Id))
            return false;

        if (!DataManager.Inst.ReadData<EquipmentData>().TryGetValue(item.Id, out var equipmentData))
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


        ItemData itemData = DataManager.Inst.GetData<ItemData>(item.Id);

        if (itemData == null)
            return false;

        // 기존 장비를 인벤토리에 반환
        if (!string.IsNullOrEmpty(equippedItem?.Id))
        {
            if (!AddItem(equippedItem.Id))
                return false;
        }

        // 가방 스택과 장착 슬롯이 같은 객체를 공유하지 않도록
        // 장비 슬롯용 아이템을 별도로 생성.
        // 장착 슬롯에는 별도의 1개짜리 객체 사용.
        item = CreateInventoryItemViewModel(itemData);
        item.Count = 1;

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
                CheckTalismanEffectType(inventory.TalismanItemViewModel,true);
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
                CheckTalismanEffectType(inventory.TalismanItemViewModel, false);
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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

#endif
    public bool TryGetItem(string itemId, out InventoryItemViewModel itemViewModel)
    {
        return _inventoryViewModel.InventoryItemViewModelDictionary.TryGetValue(itemId, out itemViewModel);
    }

    private InventoryUIViewModel GetInvantoryViewModel()
    {
        if (_inventoryViewModel == null)
        {
            _inventoryViewModel = CreateInvantoryViewModel();
        }

        return _inventoryViewModel;
    }

    private InventoryUIViewModel CreateInvantoryViewModel()
    {
        var inventoryViewModel = new InventoryUIViewModel
        {
            InventoryMaxSlotNumber = 24,
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

    private bool CheckTalismanEffectType(InventoryItemViewModel item, bool isEquipment)
    {
        if (!DataManager.Inst.ReadData<EquipmentData>().TryGetValue(item.Id, out var equipmentData))
        {
            return false;
        }


        switch (equipmentData.EquipmentEffectType)
        {
            case EquipmentEffectType.None:
                break;
            case EquipmentEffectType.InstantHeal:
                break;
            case EquipmentEffectType.StatusDamage:
                break;
            case EquipmentEffectType.ReducePoisonDamageTaken:
                ServiceManager.Inst.PlayerService.SetPlayerPoisonProtectionAmulet(isEquipment);
                break;

            default:
                return false;

        }
        return true;
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

    public void InitializeEmptyInventory()
    {
        GetInvantoryViewModel();
    }

    public bool TryCaptureSaveItems(out List<SavedItemData> savedItems, out string error)
    {
        savedItems = null;
        error = null;

        if (_pendingUseItem != null)
        {
            error = "아이템 사용 결과를 기다리고 있습니다. 잠시 후 다시 저장해 주세요.";
            return false;
        }

        if (DataManager.Inst == null)
        {
            error = "DataManager가 준비되지 않았습니다.";
            return false;
        }

        var inventory = GetInvantoryViewModel();

        var result = new List<SavedItemData>();
        var capturedObjects = new HashSet<InventoryItemViewModel>();

        // 장비를 먼저 기록해서 가방과 같은 참조가 남아 있더라도
        // 동일한 객체를 두 번 저장하지 않음.
        EquipmentSlot[] equipmentSlots =
        {
            EquipmentSlot.Talisman,
            EquipmentSlot.FishingRod,
            EquipmentSlot.Axe,
            EquipmentSlot.PickAxe
        };

        foreach (EquipmentSlot slot in equipmentSlots)
        {
            InventoryItemViewModel item = GetSaveEquipmentItem(inventory, slot);

            if (item == null || string.IsNullOrWhiteSpace(item.Id))
                continue;

            if (!capturedObjects.Add(item))
            {
                error = "동일한 아이템이 여러 장비 슬롯에 등록되어 있습니다.";
                return false;
            }

            if (!TryCreateSavedItem(item, slot, out var saved, out error))
                return false;

            result.Add(saved);
        }

        foreach (var pair in inventory.InventoryItemViewModelDictionary)
        {
            InventoryItemViewModel item = pair.Value;

            if (item == null)
            {
                error = "가방에 비어 있는 아이템 데이터가 있습니다.";
                return false;
            }

            if (!capturedObjects.Add(item))
                continue;

            if (!TryCreateSavedItem(item, EquipmentSlot.None, out var saved, out error))
            {
                return false;
            }

            result.Add(saved);
        }

        foreach (InventoryItemViewModel item in _runeItems)
        {
            if (!TryCreateSavedItem(item, EquipmentSlot.None, out var saved, out error))
                return false;

            result.Add(saved);
        }

        savedItems = result;
        return true;
    }

    private bool TryCreateSavedItem(
        InventoryItemViewModel item,
        EquipmentSlot slot,
        out SavedItemData saved,
        out string error)
    {
        saved = null;
        error = null;

        if (string.IsNullOrWhiteSpace(item.Id))
        {
            error = "아이템 ID가 비어 있습니다.";
            return false;
        }

        ItemData data = DataManager.Inst.GetData<ItemData>(item.Id);

        if (data == null ||
            data.MaxStack <= 0 ||
            item.Count <= 0 ||
            item.Count > data.MaxStack)
        {
            error = $"아이템 정보 또는 수량이 올바르지 않습니다: {item.Id}";
            return false;
        }

        if (slot != EquipmentSlot.None)
        {
            EquipmentData equipment =
                DataManager.Inst.GetData<EquipmentData>(item.Id);

            if (item.Count != 1 ||
                equipment == null ||
                equipment.EquipmentSlot != slot)
            {
                error = $"장비 슬롯 또는 수량이 올바르지 않습니다: {item.Id}";
                return false;
            }
        }

        if (!_saveItemIds.TryGetValue(item, out string instanceId))
        {
            instanceId = Guid.NewGuid().ToString("N");
            _saveItemIds.Add(item, instanceId);
        }

        saved = new SavedItemData
        {
            InstanceId = instanceId,
            ItemId = item.Id,
            Count = item.Count,
            EquippedSlot = slot
        };

        return true;
    }

    public bool TryRestoreSaveItems(
        List<SavedItemData> savedItems,
        out string error)
    {
        error = null;

        if (_pendingUseItem != null)
        {
            error = "아이템 사용 결과를 기다리는 동안 인벤토리를 불러올 수 없습니다.";
            return false;
        }

        if (savedItems == null || DataManager.Inst == null)
        {
            error = "인벤토리 저장 데이터 또는 DataManager가 없습니다.";
            return false;
        }

        var inventory = GetInvantoryViewModel();

        // 기존 인벤토리를 지우기 전에 전체 데이터를 검사하고 준비.
        var bagItems = new List<InventoryItemViewModel>();
        var runeItems = new List<InventoryItemViewModel>();
        var equipmentItems =
            new Dictionary<EquipmentSlot, InventoryItemViewModel>();

        var restoredIds =
            new Dictionary<InventoryItemViewModel, string>();

        var usedIds = new HashSet<string>();

        foreach (SavedItemData saved in savedItems)
        {
            if (saved == null ||
                string.IsNullOrWhiteSpace(saved.InstanceId) ||
                string.IsNullOrWhiteSpace(saved.ItemId) ||
                !usedIds.Add(saved.InstanceId))
            {
                error = "저장 아이템의 ID가 비어 있거나 중복되었습니다.";
                return false;
            }

            ItemData data =
                DataManager.Inst.GetData<ItemData>(saved.ItemId);

            if (data == null ||
                data.MaxStack <= 0 ||
                saved.Count <= 0 ||
                saved.Count > data.MaxStack)
            {
                error = $"복원할 아이템 정보 또는 수량이 잘못되었습니다: {saved.ItemId}";
                return false;
            }

            InventoryItemViewModel item = CreateInventoryItemViewModel(data);

            item.Count = saved.Count;

            if (saved.EquippedSlot == EquipmentSlot.None)
            {
                if (IsRuneItem(data))
                    runeItems.Add(item);
                else
                    bagItems.Add(item);
            }
            else
            {
                if (!IsSupportedSaveEquipmentSlot(saved.EquippedSlot))
                {
                    error = $"지원하지 않는 장비 슬롯입니다: {saved.EquippedSlot}";
                    return false;
                }

                EquipmentData equipment = DataManager.Inst.GetData<EquipmentData>(saved.ItemId);

                if (saved.Count != 1 ||
                    equipment == null ||
                    equipment.EquipmentSlot != saved.EquippedSlot ||
                    equipmentItems.ContainsKey(saved.EquippedSlot))
                {
                    error = $"장비 저장 데이터가 올바르지 않습니다: {saved.ItemId}";
                    return false;
                }

                equipmentItems.Add(saved.EquippedSlot, item);
            }

            restoredIds.Add(item, saved.InstanceId);
        }

        if (bagItems.Count > inventory.InventoryMaxSlotNumber)
        {
            error = "저장된 아이템 슬롯 수가 인벤토리 최대 크기를 초과합니다.";
            return false;
        }

        // 검사가 끝난 뒤 적용.
        // VM과 ObservableDictionary를 교체하지 않아 기존 UI 연결을 유지.
        ClearQuickSlots();
        inventory.InventoryItemViewModelDictionary.Clear();

        inventory.TalismanItemViewModel = GetRestoredEquipment(equipmentItems, EquipmentSlot.Talisman);
        inventory.FishingItemViewModel = GetRestoredEquipment(equipmentItems, EquipmentSlot.FishingRod);
        inventory.LoggingItemViewModel = GetRestoredEquipment(equipmentItems, EquipmentSlot.Axe);
        inventory.MiningItemViewModel = GetRestoredEquipment(equipmentItems, EquipmentSlot.PickAxe);

        _nextSlotNumber = 1;

        foreach (InventoryItemViewModel item in bagItems)
        {
            inventory.InventoryItemViewModelDictionary.Add(_nextSlotNumber.ToString(), item);

            _nextSlotNumber++;
        }

        _saveItemIds.Clear();

        foreach (var pair in restoredIds)
        {
            _saveItemIds.Add(pair.Key, pair.Value);
        }

        _runeItems.Clear();
        _runeItems.AddRange(runeItems);
        RuneInventoryChanged?.Invoke();

        return true;
    }

    private static InventoryItemViewModel GetSaveEquipmentItem(InventoryUIViewModel inventory, EquipmentSlot slot)
    {
        return slot switch
        {
            EquipmentSlot.Talisman => inventory.TalismanItemViewModel,
            EquipmentSlot.FishingRod => inventory.FishingItemViewModel,
            EquipmentSlot.Axe => inventory.LoggingItemViewModel,
            EquipmentSlot.PickAxe => inventory.MiningItemViewModel,
            _ => null
        };
    }

    private static InventoryItemViewModel GetRestoredEquipment(Dictionary<EquipmentSlot, InventoryItemViewModel> equipment, EquipmentSlot slot)
    {
        return equipment.TryGetValue(slot, out var item) ? item : new InventoryItemViewModel();
    }

    private static bool IsSupportedSaveEquipmentSlot(EquipmentSlot slot)
    {
        return slot == EquipmentSlot.Talisman
            || slot == EquipmentSlot.FishingRod
            || slot == EquipmentSlot.Axe
            || slot == EquipmentSlot.PickAxe;
    }

    // 로컬 플레이어가 생성되고 PlayerService 연결이 끝난 뒤 호출.
    public void ReapplyEquipmentEffectsAfterLoad()
    {
        var inventory = GetInvantoryViewModel();

        // 이전 캐릭터의 부적 효과가 남지 않도록 해제 후 재적용.
        ServiceManager.Inst.PlayerService .SetPlayerPoisonProtectionAmulet(false);

        InventoryItemViewModel talisman = inventory.TalismanItemViewModel;

        if (talisman != null && !string.IsNullOrWhiteSpace(talisman.Id))
        {
            CheckTalismanEffectType(talisman, true);
        }
    }

}
