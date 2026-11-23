using System;

public partial class InventoryService
{
    public const int QuickSlotCount = 5;

    private readonly InventoryItemViewModel[] _quickSlotItems = new InventoryItemViewModel[QuickSlotCount];

    public event Action QuickSlotsChanged;

    public InventoryItemViewModel GetQuickSlotItem(int index)
    {
        return index >= 0 && index < QuickSlotCount ? _quickSlotItems[index] : null;
    }

    public int GetQuickSlotIndex(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return -1;

        for (int i = 0; i < QuickSlotCount; i++)
        {
            if (_quickSlotItems[i] != null && _quickSlotItems[i].Id == itemId)
                return i;
        }

        return -1;
    }

    public bool CanRegisterQuickSlot(InventoryItemViewModel item)
    {
        if (item == null || item.ItemType != nameof(ItemType.Consumable)
            || !CanUseItem(item) || !OwnsBagItem(item) || GetQuickSlotIndex(item.Id) >= 0)
            return false;

        for (int i = 0; i < QuickSlotCount; i++)
        {
            if (_quickSlotItems[i] == null)
                return true;
        }

        return false;
    }

    public bool TryRegisterQuickSlot(InventoryItemViewModel item)
    {
        if (!CanRegisterQuickSlot(item))
            return false;

        for (int i = 0; i < QuickSlotCount; i++)
        {
            if (_quickSlotItems[i] != null)
                continue;

            _quickSlotItems[i] = item;
            QuickSlotsChanged?.Invoke();
            return true;
        }

        return false;
    }

    public bool RequestUseQuickSlot(int index)
    {
        return RequestUseItem(GetQuickSlotItem(index));
    }

    private void ClearQuickSlotItem(InventoryItemViewModel item)
    {
        for (int i = 0; i < QuickSlotCount; i++)
        {
            if (!ReferenceEquals(_quickSlotItems[i], item))
                continue;

            _quickSlotItems[i] = null;
            QuickSlotsChanged?.Invoke();
            return;
        }
    }

    private void ClearQuickSlots()
    {
        Array.Clear(_quickSlotItems, 0, QuickSlotCount);
        QuickSlotsChanged?.Invoke();
    }
}
