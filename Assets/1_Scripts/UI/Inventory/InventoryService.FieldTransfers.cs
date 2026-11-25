using System;
using System.Collections.Generic;
using Unity.Netcode;

// 수량 변경 후보를 저장한 뒤 기존 VM에 적용한다. 저장 실패 시 가방과 퀵슬롯은 그대로다.
public partial class InventoryService
{
    private InventoryItemViewModel _dropReservation;
    private int _dropQuantity;

    public bool CanDrop(InventoryItemViewModel item)
    {
        return item != null && _dropReservation == null && item.Count > 0 && OwnsBagItem(item)
            && !IsEquippedItem(item) && !IsItemUsePending(item.Id);
    }

    public bool RequestDrop(InventoryItemViewModel item, int quantity)
    {
        var player = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        if (!CanDrop(item) || quantity < 1 || quantity > item.Count || player == null ||
            !player.TryGetComponent(out PlayerNetworkStats stats) ||
            !TryCreateSavedItem(item, EquipmentSlot.None, out var selected, out _))
            return false;

        _dropReservation = item;
        _dropQuantity = quantity;
        if (stats.RequestFieldDrop(selected.InstanceId, item.Id, quantity)) return true;
        ReleaseDrop();
        return false;
    }

    internal void ReleaseDrop()
    {
        _dropReservation = null;
        _dropQuantity = 0;
    }

    // 0은 수용 불가, false는 저장 준비/기록 실패로 재시도한다.
    internal bool TrySaveFieldExchange(string token, string instanceId, string itemId, int quantity,
        out int accepted, out string error)
    {
        accepted = 0;
        error = null;
        if (!DataManager.Inst.ReadData<ItemData>().TryGetValue(itemId, out var data) ||
            quantity < 1 || quantity > data.MaxStack)
            return true;
        if (!TryCaptureSaveItems(out var snapshot, out error)) return false;

        var bag = GetInvantoryViewModel();
        var counts = new Dictionary<InventoryItemViewModel, int>();
        InventoryItemViewModel added = null;
        string newId = null;
        string removedSlot = null;
        bool rune = IsRuneItem(data);
        if (!string.IsNullOrEmpty(instanceId))
        {
            if (_dropReservation == null || _dropReservation.Id != itemId || _dropQuantity != quantity ||
                !OwnsBagItem(_dropReservation) || IsEquippedItem(_dropReservation) ||
                IsItemUsePending(itemId) || _dropReservation.Count < quantity ||
                !_saveItemIds.TryGetValue(_dropReservation, out var id) || id != instanceId)
                return true;
            var saved = snapshot.Find(entry => entry.InstanceId == instanceId);
            if (saved == null || saved.EquippedSlot != EquipmentSlot.None) return true;
            accepted = quantity;
            saved.Count -= quantity;
            counts.Add(_dropReservation, saved.Count);
            if (saved.Count == 0)
            {
                snapshot.Remove(saved);
                foreach (var pair in bag.InventoryItemViewModelDictionary)
                    if (pair.Value == _dropReservation) { removedSlot = pair.Key; break; }
            }
        }
        else
        {
            var byId = new Dictionary<string, SavedItemData>();
            foreach (var entry in snapshot) byId.Add(entry.InstanceId, entry);
            bool existingRune = false;
            var candidates = rune ? GetRuneItems() : GetInventoryItems();
            foreach (var item in candidates)
            {
                if (item.Id != itemId) continue;
                existingRune = rune;
                if (item == _dropReservation || IsEquippedItem(item)) continue;
                int take = Math.Min(quantity - accepted, data.MaxStack - item.Count);
                if (take > 0)
                {
                    int next = item.Count + take;
                    counts.Add(item, next);
                    byId[_saveItemIds[item]].Count = next;
                    accepted += take;
                }
                if (rune || accepted == quantity) break;
            }
            bool hasSlot = rune ? !existingRune : bag.InventoryItemViewModelDictionary.Count < bag.InventoryMaxSlotNumber;
            if (accepted < quantity && hasSlot)
            {
                added = CreateInventoryItemViewModel(data);
                added.SetCountWithoutNotification(quantity - accepted);
                newId = Guid.NewGuid().ToString("N");
                snapshot.Add(new SavedItemData { InstanceId = newId, ItemId = itemId, Count = added.Count,
                    EquippedSlot = EquipmentSlot.None });
                accepted = quantity;
            }
        }
        if (accepted == 0) return true;
        var receipt = new FieldTransferReceipt { Token = token, Count = accepted };
        if (!SaveManager.Inst.TrySaveFieldTransfer(snapshot, receipt, out error)) return false;

        foreach (var change in counts) change.Key.SetCountWithoutNotification(change.Value);
        if (removedSlot != null)
        {
            _saveItemIds.Remove(_dropReservation);
            bag.InventoryItemViewModelDictionary.Remove(removedSlot);
            ClearQuickSlotItem(_dropReservation);
        }
        if (added != null)
        {
            _saveItemIds.Add(added, newId);
            if (rune) _runeItems.Add(added);
            else bag.InventoryItemViewModelDictionary.Add((_nextSlotNumber++).ToString(), added);
        }
        foreach (var item in counts.Keys) item.NotifyCountChanged();
        if (rune) RuneInventoryChanged?.Invoke();
        return true;
    }
}
