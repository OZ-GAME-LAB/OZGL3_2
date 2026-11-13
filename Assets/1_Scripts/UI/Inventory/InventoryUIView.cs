using ObservableCollections;
using System.Collections.Generic;
using System.ComponentModel;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUIView : UIBase
{
    [SerializeField] private Transform _inventorySlotRoot;
    [SerializeField] private Button _closeButton;
    [SerializeField] private GameObject _inventorySlotPrefab;

    [SerializeField] private Transform _EquipmentSlotRoot;

    [SerializeField] private GameObject _itemPopupUI;

    [SerializeField] private Button _ItemUseButton;
    [SerializeField] private Button _ItemAddQuickSlotButton;
    [SerializeField] private Button _ItemRemoveButton;

    [SerializeField] private TMP_Text _ItemUseText;
    [SerializeField] private TMP_Text _ItemAddQuickSlotText;
    [SerializeField] private TMP_Text _ItemRemoveText;

    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _descriptionText;


    InventoryUIViewModel _InventoryUIViewModel;
    private readonly Dictionary<InventorySlotUIView, InventoryItemViewModel> _inventorySlots = new();

    private readonly List<InventorySlotUIView> _equipmentSlots = new();

    private InventoryItemViewModel _selectedItem;

    private void OnEnable()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(OnClickCloseButton);

        }

        if (_ItemUseButton!= null)
        {
        _ItemUseButton.onClick.AddListener(OnClickItemUseButton);
        }
        if (_ItemAddQuickSlotButton != null)
        {
            _ItemAddQuickSlotButton.onClick.AddListener(OnClickItemAddQuickSlotButton);
        }
        if (_ItemRemoveButton != null)
        {
            _ItemRemoveButton.onClick.AddListener(OnClickItemRemoveButton);
        }

        CloseItemPopup();
    }

    private void OnDisable()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(OnClickCloseButton);
        }

        if (_ItemUseButton != null)
        {
            _ItemUseButton.onClick.RemoveListener(OnClickItemUseButton);
        }
        if (_ItemAddQuickSlotButton != null)
        {
            _ItemAddQuickSlotButton.onClick.RemoveListener(OnClickItemAddQuickSlotButton);
        }
        if (_ItemRemoveButton != null)
        {
            _ItemRemoveButton.onClick.RemoveListener(OnClickItemRemoveButton);
        }

        CloseItemPopup();
    }

    public void Bind(InventoryUIViewModel inventory)
    {
        if (_InventoryUIViewModel != null)
        {
            if (_InventoryUIViewModel.InventoryItemViewModelDictionary != null)
            {
                _InventoryUIViewModel.InventoryItemViewModelDictionary.CollectionChanged
                    -= OnInventoryChanged;
            }

            _InventoryUIViewModel.PropertyChanged -= OnPropertyChanged;
        }

        ClearInventorySlots();
        ClearEquipmentSlots();
        _InventoryUIViewModel = inventory;


        if (_InventoryUIViewModel?.InventoryItemViewModelDictionary == null)
            return;

        _InventoryUIViewModel.InventoryItemViewModelDictionary.CollectionChanged += OnInventoryChanged;

        _InventoryUIViewModel.PropertyChanged += OnPropertyChanged;


        RebuildEquipmentSlots();

        RebuildSlots();
    }

    private void OnInventoryChanged(in NotifyCollectionChangedEventArgs<KeyValuePair<string, InventoryItemViewModel>> e)
    {
        RebuildSlots();
    }

    private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(InventoryUIViewModel.TalismanItemViewModel):
            case nameof(InventoryUIViewModel.FishingItemViewModel):
            case nameof(InventoryUIViewModel.LoggingItemViewModel):
            case nameof(InventoryUIViewModel.MiningItemViewModel):
                RebuildEquipmentSlots();
                break;

        }


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
        slotView.ItemOnClickEvent += ItemOnClick;
        _inventorySlots.Add(slotView, item);
    }

    private void ItemOnClick(InventorySlotUIView slot, InventoryItemViewModel item)
    {
        if (slot == null || item == null)
            return;

        OpenItemPopup(slot, item);
    }

    private void OpenItemPopup(InventorySlotUIView slot, InventoryItemViewModel item)
    {
        _selectedItem = item;

        // 아이템 정보 표시
        _nameText.text = item.Name;
        _descriptionText.text = item.Description;

        // 아이템 타입에 따른 사용 버튼 텍스트 설정
        if (item.ItemType == nameof(ItemType.Equipment))
        {
            if (ServiceManager.Inst.InventoryService.IsEquippedItem(item))
            {
                _ItemUseText.text = "장비 해제";
            }
            else
            {
                _ItemUseText.text = "장비 장착";
            }
        }
        else
        {
            _ItemUseText.text = "아이템 사용";
        }

        _ItemAddQuickSlotText.text = "아이템 퀵슬롯 등록";
        _ItemRemoveText.text = "아이템 제거";


        _itemPopupUI.SetActive(true);
    }

    private void CloseItemPopup()
    {
        _selectedItem = null;

        if (_itemPopupUI != null)
            _itemPopupUI.SetActive(false);
    }

    // 아이템 사용 / 장착 버튼
    private void OnClickItemUseButton()
    {
        if (_selectedItem == null)
            return;

        InventoryItemViewModel item = _selectedItem;

        if (item.ItemType == nameof(ItemType.Equipment))
        {
            // 장비 아이템 장착
            if (!ServiceManager.Inst.InventoryService.TryEquipItem(item))
            {
                Debug.LogWarning($"{item.Name} 은 장착 아이템이 아닙니다");
                return;
            }
        }
        else
        {
            // 일반 아이템 사용
            ServiceManager.Inst.InventoryService.UseItem(item);
        }

        CloseItemPopup();
        RemoveItemRequest(item);
    }

    // 아이템 퀵슬롯 등록 버튼
    private void OnClickItemAddQuickSlotButton()
    {
        if (_selectedItem == null)
            return;

        // TODO: 아이템 퀵슬롯 등록 로직
    }

    // 아이템 제거 버튼
    private void OnClickItemRemoveButton()
    {
        if (_selectedItem == null)
            return;

        // TODO: 아이템 제거 로직
    }
    private void RemoveItemRequest(InventoryItemViewModel item)
    {
        ServiceManager.Inst.InventoryService.RemoveItem(item);
    }

    private void ClearInventorySlots()
    {
        foreach (var slot in _inventorySlots.Keys)
        {
            if (slot == null)
                continue;

            slot.ItemOnClickEvent -= ItemOnClick;
            Destroy(slot.gameObject);
        }

        _inventorySlots.Clear();
    }

    private void RebuildEquipmentSlots()
    {
        ClearEquipmentSlots();
        CreateEquipmentSlot(_EquipmentSlotRoot, _InventoryUIViewModel.TalismanItemViewModel);
        CreateEquipmentSlot(_EquipmentSlotRoot, _InventoryUIViewModel.LoggingItemViewModel);
        CreateEquipmentSlot(_EquipmentSlotRoot, _InventoryUIViewModel.FishingItemViewModel);
        CreateEquipmentSlot(_EquipmentSlotRoot, _InventoryUIViewModel.MiningItemViewModel);
    }

    private void CreateEquipmentSlot(Transform root, InventoryItemViewModel inventoryItemViewModel)
    {
        if (root == null)
            return;

        var slotObject = Instantiate(_inventorySlotPrefab, root);
        var slotView = slotObject.GetComponent<InventorySlotUIView>();

        if (slotView == null)
        {
            Destroy(slotObject);
            Debug.LogError("InventorySlotUIView가 프리팹에 없습니다.", _inventorySlotPrefab);
            return;
        }

        slotView.Bind(inventoryItemViewModel);
        slotView.ItemOnClickEvent += ItemOnClick;
        _equipmentSlots.Add(slotView);
    }

    private void ClearEquipmentSlots()
    {
        foreach (var slot in _equipmentSlots)
        {
            if (slot != null)
            {
                slot.ItemOnClickEvent -= ItemOnClick;
                Destroy(slot.gameObject);
            }
        }

        _equipmentSlots.Clear();
    }

    private void OnClickCloseButton()
    {
        UIManager.Inst.CloseInventoryUI();
    }
}