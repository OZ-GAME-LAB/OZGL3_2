using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUIView : UIBase
{
    [SerializeField] private Transform _inventorySlotRoot;
    [SerializeField] private Button _closeButton;
    [SerializeField] private GameObject _inventorySlotPrefab;

    private readonly Dictionary<InventorySlotUIView, InventoryUIViewModel> _inventorySlots = new();

    private void OnEnable()
    {
        if (_closeButton != null)
            _closeButton.onClick.AddListener(OnClickCloseButton);
    }

    private void OnDisable()
    {
        if (_closeButton != null)
            _closeButton.onClick.RemoveListener(OnClickCloseButton);
    }

    public void Bind(List<InventoryUIViewModel> inventory)
    {
        ClearInventorySlots();

        if (inventory == null)
            return;

        foreach (var item in inventory)
        {
            if (item == null)
                continue;

            MakeItemSlot(item);
        }
    }

    public void AddItembyInventory(InventoryUIViewModel item)
    {
        if (item == null)
            return;

        MakeItemSlot(item);
    }

    private void MakeItemSlot(InventoryUIViewModel item)
    {
        var slotObject = Instantiate(_inventorySlotPrefab, _inventorySlotRoot);
        var slotView = slotObject.GetComponent<InventorySlotUIView>();

        if (slotView == null)
        {
            Destroy(slotObject);
            Debug.LogError("InventorySlotUIView가 프리팹에 없습니다.", _inventorySlotPrefab);
            return;
        }

        slotView.Bind(item);
        slotView.ItemUsed += OnItemUsed;
        _inventorySlots.Add(slotView, item);
    }

    private void OnItemUsed(InventorySlotUIView slot, InventoryUIViewModel item)
    {
        if (!_inventorySlots.Remove(slot))
            return;

        slot.ItemUsed -= OnItemUsed;
        Destroy(slot.gameObject);
    }

    private void ClearInventorySlots()
    {
        foreach (var slot in _inventorySlots.Keys)
        {
            if (slot == null)
                continue;

            slot.ItemUsed -= OnItemUsed;
            Destroy(slot.gameObject);
        }

        _inventorySlots.Clear();
    }

    private void OnClickCloseButton()
    {
        UIManager.Inst.CloseInventoryUI();
    }
}