using System.Collections.Generic;
using UnityEngine;

public class RuneInventoryViewModel : ViewModelBase
{
    private readonly List<RuneSlotViewModel> _inventoryRunes = new();
    private readonly List<RuneSlotViewModel> _equipmentSlots = new();

    public IReadOnlyList<RuneSlotViewModel> InventoryRunes => _inventoryRunes;

    public IReadOnlyList<RuneSlotViewModel> EquipmentSlots => _equipmentSlots;

    public string SelectedRuneId { get; private set; } = "";
    public string Message { get; private set; } = "";

    public RuneInventoryViewModel(int slotCount)
    {
        for (int i = 0; i < slotCount; i++)
        {
            _equipmentSlots.Add(new RuneSlotViewModel(i));
        }
    }

    public void SetInventoryRunes(IEnumerable<string> runeIds)
    {
        _inventoryRunes.Clear();

        foreach (string id in runeIds)
        {
            var slot = new RuneSlotViewModel(-1);
            slot.SetRuneId(id);
            _inventoryRunes.Add(slot);
        }

        SelectRune("");
        OnPropertyChanged(nameof(InventoryRunes));
    }

    public void SelectRune(string runeId)
    {
        SelectedRuneId = runeId ?? "";

        foreach (RuneSlotViewModel slot in _inventoryRunes)
        {
            slot.SetSelected(slot.RuneId == SelectedRuneId);
        }

        OnPropertyChanged(nameof(SelectedRuneId));
    }

    public void SetMessage(string message)
    {
        Message = message ?? "";
        OnPropertyChanged(nameof(Message));
    }

    public void InvokeOnceOnInit()
    {
        OnPropertyChanged(nameof(InventoryRunes));
        OnPropertyChanged(nameof(SelectedRuneId));
        OnPropertyChanged(nameof(Message));
    }

}
