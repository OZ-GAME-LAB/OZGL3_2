using System.Collections.Generic;
using System.ComponentModel;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RuneInventoryUI : UIBase
{
    [Header("장착 슬롯 - 왼쪽부터 0, 1, 2")]
    [SerializeField] private RuneSlotUI[] _equipmentSlots;

    [Header("인벤토리 룬 목록")]
    [SerializeField] private RuneSlotUI Prefab_RuneSlot;
    [SerializeField] private Transform Transform_RuneListRoot;

    [Header("버튼 및 안내")]
    [SerializeField] private Button Button_ClearSelection;
    [SerializeField] private Button Button_Close;
    [SerializeField] private TMP_Text Text_Message;

    private readonly List<RuneSlotUI> _inventorySlots = new();

    private RuneEquipmentService _service;
    private RuneInventoryViewModel _viewModel;

    private void Awake()
    {
        Button_ClearSelection.onClick.AddListener(OnClearSelection);
        Button_Close.onClick.AddListener(OnClose);
    }

    // UI를 열 때 자신의 플레이어 Service를 전달.
    public void Bind(RuneEquipmentService service, InventoryService inventory)
    {
        Unbind();

        if (service == null || service.IsSpawned == false || service.IsOwner == false)
        {
            Debug.LogError("자신의 RuneEquipmentService가 필요합니다.", this);
            return;
        }

        if (_equipmentSlots == null || _equipmentSlots.Length != RuneEquipmentService.SlotCount)
        {
            Debug.LogError("장착 슬롯 3개를 순서대로 연결하세요.", this);
            return;
        }

        foreach (RuneSlotUI slot in _equipmentSlots)
        {
            if (slot == null)
            {
                Debug.LogError("장착 슬롯 참조가 비어 있습니다.", this);
                return;
            }
        }

        _service = service;

        _service.RefreshInventory(inventory);
        _viewModel = _service.ViewModel;
        _viewModel.PropertyChanged += OnPropertyChanged;

        for (int i = 0; i < _equipmentSlots.Length; i++)
        {
            _equipmentSlots[i].Bind(_viewModel.EquipmentSlots[i], OnEquipmentSlotClicked);
        }

        _viewModel.InvokeOnceOnInit();
    }

    private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(RuneInventoryViewModel.InventoryRunes):
                CreateInventorySlots();
                break;

            case nameof(RuneInventoryViewModel.Message):
                if (Text_Message != null)
                    Text_Message.text = _viewModel.Message;
                break;
        }
    }

    private void CreateInventorySlots()
    {
        ClearInventorySlots();

        foreach (RuneSlotViewModel slotVM in _viewModel.InventoryRunes)
        {
            RuneSlotUI slot = Instantiate(Prefab_RuneSlot, Transform_RuneListRoot);

            slot.Bind(slotVM, OnInventorySlotClicked);
            _inventorySlots.Add(slot);
        }
    }

    private void OnInventorySlotClicked(RuneSlotViewModel slot)
    {
        if (_service != null)
        {
            _service.SelectRune(slot.RuneId);
        }
    }

    private void OnEquipmentSlotClicked(RuneSlotViewModel slot)
    {
        if (_service != null)
        {
            _service.RequestSlotClick(slot.SlotIndex);
        }
    }

    private void OnClearSelection()
    {
        if (_service != null)
        {
            _service.ClearSelection();
        }
    }

    private void OnClose()
    {
        gameObject.SetActive(false);
    }

    private void ClearInventorySlots()
    {
        foreach (RuneSlotUI slot in _inventorySlots)
        {
            if (slot == null)
            {
                continue;
            }

            slot.Unbind();
            slot.gameObject.SetActive(false);
            Destroy(slot.gameObject);
        }

        _inventorySlots.Clear();
    }

    private void Unbind()
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnPropertyChanged;
        }

        if (_equipmentSlots != null)
        {
            foreach (RuneSlotUI slot in _equipmentSlots)
            {
                if (slot != null)
                {
                    slot.Unbind();
                }
            }
        }

        ClearInventorySlots();

        _viewModel = null;
        _service = null;
    }

    private void OnDisable()
    {
        Unbind();
    }
}