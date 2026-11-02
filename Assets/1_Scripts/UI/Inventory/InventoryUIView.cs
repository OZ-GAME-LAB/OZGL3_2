using ObservableCollections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUIView : UIBase
{
    [SerializeField] private Transform _inventorySlotRoot;
    [SerializeField] private Button _closeButton;
    [SerializeField] private GameObject _inventorySlotPrefab;


    InventoryUIViewModel _InventoryUIViewModel;
    private readonly Dictionary<InventorySlotUIView, InventoryItemViewModel> _inventorySlots = new();

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

    public void Bind(InventoryUIViewModel inventory)
    {
        if (_InventoryUIViewModel?.InventoryItemViewModelDictionary != null)
        {
            _InventoryUIViewModel.InventoryItemViewModelDictionary.CollectionChanged -= OnInventoryChanged;
        }

        ClearInventorySlots();
        _InventoryUIViewModel = inventory;

        if (_InventoryUIViewModel?.InventoryItemViewModelDictionary == null)
            return;

        _InventoryUIViewModel.InventoryItemViewModelDictionary.CollectionChanged += OnInventoryChanged;

        RebuildSlots();
    }

    private void OnInventoryChanged(in NotifyCollectionChangedEventArgs<KeyValuePair<string, InventoryItemViewModel>> e)
    {
        RebuildSlots();
    }

    private void RebuildSlots()
    {
        ClearInventorySlots();

        foreach (var pair in _InventoryUIViewModel.InventoryItemViewModelDictionary)
        {
            if (pair.Value != null)
                MakeItemSlot(pair.Value);
        }
    }

    private void MakeItemSlot(InventoryItemViewModel item)
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

    private void OnItemUsed(InventorySlotUIView slot, InventoryItemViewModel item)
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