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
    [SerializeField] private Button _popupCloseButton;

    [Header("장비 슬롯 배경")]
    [SerializeField] private Sprite _talismanSlotBackground;
    [SerializeField] private Sprite _loggingSlotBackground;
    [SerializeField] private Sprite _fishingSlotBackground;
    [SerializeField] private Sprite _miningSlotBackground;

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

        if (_popupCloseButton != null)
        {
            _popupCloseButton.onClick.AddListener(OnClickPopupCloseButton);
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

        if (_popupCloseButton != null)
        {
            _popupCloseButton.onClick.RemoveListener(OnClickPopupCloseButton);
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

        _inventorySlots.Add(slotView, item);
        slotView.Bind(item);
        slotView.ItemOnClickEvent += ItemOnClick;
    }

    private void ItemOnClick(InventorySlotUIView slot, InventoryItemViewModel item)
    {
        if (slot == null || item == null || item.Name == null)
        {
            CloseItemPopup();
            return;

        }

        OpenItemPopup(slot, item);
    }

    private void OpenItemPopup(InventorySlotUIView slot, InventoryItemViewModel item)
    {
        _selectedItem = item;

        // 아이템 정보 표시
        _nameText.text = item.Name;
        _descriptionText.text = item.Description;

        _ItemUseButton.gameObject.SetActive(false);
        _ItemAddQuickSlotButton.gameObject.SetActive(false);
        // 아이템 타입에 따른 사용 버튼 텍스트 설정

        switch (item.ItemType)
        {
            case nameof(ItemType.Equipment):
                _ItemUseButton.gameObject.SetActive(true);

                bool isEquipped =
                    ServiceManager.Inst.InventoryService.IsEquippedItem(item);

                _ItemUseText.text = isEquipped ? "장비 해제" : "장비 장착";
                break;

            case nameof(ItemType.Consumable):
                _ItemUseButton.gameObject.SetActive(true);
                _ItemAddQuickSlotButton.gameObject.SetActive(true);

                _ItemUseText.text = "아이템 사용";
                _ItemAddQuickSlotText.text = "아이템 퀵슬롯 등록";
                break;
        }

        _ItemRemoveText.text = "아이템 제거";

        _itemPopupUI.SetActive(true);

        _itemPopupUI.transform.position =
            UnityEngine.InputSystem.Mouse.current.position.ReadValue();
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
        InventoryService inventory = ServiceManager.Inst.InventoryService;

        if (item.ItemType == nameof(ItemType.Equipment))
        {
            bool wasEquipped = inventory.IsEquippedItem(item);

            // 장비 아이템 장착
            if (inventory.TryEquipItem(item) == false)
            {
                Debug.LogWarning($"{item.Name}은 장착 아이템이 아닙니다 장착 상태를 변경하지 못했습니다.");
                return;
            }

            CloseItemPopup();

            // 장착할 때만 가방에서 제거.
            // 해제할 때는 UnequipItem이 가방 반환까지 처리함.
            if (wasEquipped == false)
            {
                RemoveItemRequest(item);
            }

            return;
        }

        inventory.UseItem(item);

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

        InventoryItemViewModel item = _selectedItem;

        CloseItemPopup();
        RemoveItemRequest(item);
    }
    private void RemoveItemRequest(InventoryItemViewModel item)
    {
        ServiceManager.Inst.InventoryService.RemoveItem(item.Id);
    }

    private void ClearInventorySlots()
    {
        foreach (var slot in _inventorySlots.Keys)
        {
            if (slot == null)
                continue;

            slot.ItemOnClickEvent -= ItemOnClick;
            slot.Unbind();
            Destroy(slot.gameObject);
        }

        _inventorySlots.Clear();
    }

    private void RebuildEquipmentSlots()
    {
        ClearEquipmentSlots();

        CreateEquipmentSlot(
            _EquipmentSlotRoot,
            _InventoryUIViewModel.TalismanItemViewModel,
            _talismanSlotBackground);

        CreateEquipmentSlot(
            _EquipmentSlotRoot,
            _InventoryUIViewModel.LoggingItemViewModel,
            _loggingSlotBackground);

        CreateEquipmentSlot(
            _EquipmentSlotRoot,
            _InventoryUIViewModel.FishingItemViewModel,
            _fishingSlotBackground);

        CreateEquipmentSlot(
            _EquipmentSlotRoot,
            _InventoryUIViewModel.MiningItemViewModel,
            _miningSlotBackground);
    }

    private void CreateEquipmentSlot(
        Transform root,
        InventoryItemViewModel inventoryItemViewModel,
        Sprite background)
    {
        if (root == null)
            return;

        var slotObject = Instantiate(_inventorySlotPrefab, root);
        var slotView = slotObject.GetComponent<InventorySlotUIView>();

        if (slotView == null)
        {
            Destroy(slotObject);
            Debug.LogError(
                "InventorySlotUIView가 프리팹에 없습니다.",
                _inventorySlotPrefab);
            return;
        }

        _equipmentSlots.Add(slotView);

        slotView.Bind(inventoryItemViewModel);
        slotView.SetBackground(background);

        slotView.ItemOnClickEvent += ItemOnClick;
    }

    private void ClearEquipmentSlots()
    {
        foreach (var slot in _equipmentSlots)
        {
            if (slot != null)
            {
                slot.ItemOnClickEvent -= ItemOnClick;
                slot.Unbind();
                Destroy(slot.gameObject);
            }
        }

        _equipmentSlots.Clear();
    }

    private void OnClickCloseButton()
    {
        UIManager.Inst.CloseInventoryUI();
    }

    private void OnClickPopupCloseButton()
    {
        CloseItemPopup();
    }
}