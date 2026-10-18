using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUIView : UIBase
{
    [SerializeField] private Transform _inventorySlotRoot;
    [SerializeField] private Button _closeButton;
    [SerializeField] private GameObject _inventorySlotPrefab;

    private readonly Dictionary<InventorySlotUIView, ItemData> _inventorySlots = new();

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

    public void Bind(List<ItemData> inventoryDataList)
    {
        ClearInventorySlots();

        if (inventoryDataList == null)
            return;

        foreach (var itemData in inventoryDataList)
            AddItembyInventory(itemData);
    }

    public void AddItembyInventory(ItemData itemData)
    {
        if (itemData == null)
            return;

        var slotObject = Instantiate(_inventorySlotPrefab, _inventorySlotRoot);
        var slotView = slotObject.GetComponent<InventorySlotUIView>();

        if (slotView == null)
        {
            Destroy(slotObject);
            Debug.LogError("InventorySlotUIView가 프리팹에 없습니다.", _inventorySlotPrefab);
            return;
        }

        slotView.Bind(itemData);
        slotView.ItemUsed += OnItemUsed;
        _inventorySlots.Add(slotView, itemData);
    }

    private void OnItemUsed(InventorySlotUIView usedSlot, ItemData usedItem)
    {
        if (!_inventorySlots.Remove(usedSlot))
            return;

        usedSlot.ItemUsed -= OnItemUsed;
        Destroy(usedSlot.gameObject);

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