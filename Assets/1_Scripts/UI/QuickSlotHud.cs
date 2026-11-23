using UnityEngine;

public class QuickSlotHud : MonoBehaviour
{
    [Header("퀵슬롯 - 왼쪽부터 1~5")]
    [SerializeField] private QuickSlotSlotUI[] _slots;

    private InventoryService _inventory;

    private void OnEnable()
    {
        TryBind();
    }

    private void LateUpdate()
    {
        // HUD가 서비스보다 먼저 켜진 경우나 서비스가 교체된 경우에만 다시 연결한다.
        InventoryService current = ServiceManager.Inst != null ? ServiceManager.Inst.InventoryService : null;
        if (!ReferenceEquals(_inventory, current))
            TryBind();
    }

    private void TryBind()
    {
        Unbind();
        _inventory = ServiceManager.Inst != null ? ServiceManager.Inst.InventoryService : null;
        if (_inventory != null)
            _inventory.QuickSlotsChanged += RefreshSlots;

        RefreshSlots();
    }

    private void RefreshSlots()
    {
        if (_slots == null)
            return;

        for (int i = 0; i < _slots.Length && i < InventoryService.QuickSlotCount; i++)
        {
            if (_slots[i] != null)
                _slots[i].Bind(_inventory?.GetQuickSlotItem(i), i + 1);
        }
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void Unbind()
    {
        if (_inventory != null)
            _inventory.QuickSlotsChanged -= RefreshSlots;
        _inventory = null;

        if (_slots == null)
            return;

        foreach (QuickSlotSlotUI slot in _slots)
        {
            if (slot != null)
                slot.Unbind();
        }
    }
}
