using ObservableCollections;
using System.Collections.Generic;
using System.ComponentModel;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryUIView : UIBase
{
    [SerializeField] private Transform _inventorySlotRoot;
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
    private readonly List<InventorySlotUIView> _inventorySlots = new();

    private readonly List<InventorySlotUIView> _equipmentSlots = new();

    private InventoryItemViewModel _selectedItem;
    private PointerEventData _pointerEventData;
    private EventSystem _pointerEventSystem;
    private readonly List<RaycastResult> _pointerHits = new();

    public bool BlocksGameplayPointer(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (!isActiveAndEnabled || eventSystem == null)
            return false;

        if (_pointerEventData == null || _pointerEventSystem != eventSystem)
        {
            _pointerEventSystem = eventSystem;
            _pointerEventData = new PointerEventData(eventSystem);
        }

        _pointerEventData.Reset();
        _pointerEventData.position = screenPosition;
        _pointerHits.Clear();
        eventSystem.RaycastAll(_pointerEventData, _pointerHits);

        bool blocked = false;
        foreach (RaycastResult hit in _pointerHits)
        {
            if (hit.module is GraphicRaycaster && hit.gameObject != null &&
                hit.gameObject.transform.IsChildOf(transform))
            {
                blocked = true;
                break;
            }
        }

        _pointerHits.Clear();
        return blocked;
    }

    private void OnEnable()
    {
        SetMouseOnlyNavigation(_ItemUseButton);
        SetMouseOnlyNavigation(_ItemAddQuickSlotButton);
        SetMouseOnlyNavigation(_ItemRemoveButton);
        SetMouseOnlyNavigation(_popupCloseButton);

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
        UnsubscribeInventory();
        CloseItemPopup();
        _InventoryUIViewModel = inventory;

        if (_InventoryUIViewModel?.InventoryItemViewModelDictionary != null)
        {
            EnsureSlots();
            _InventoryUIViewModel.PropertyChanged += OnPropertyChanged;
        }

        RefreshEquipmentSlots();
        RefreshSlots();
    }

    private void UnsubscribeInventory()
    {
        if (_InventoryUIViewModel != null)
        {


            _InventoryUIViewModel.PropertyChanged -= OnPropertyChanged;
        }

    }



    private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(InventoryUIViewModel.InventoryItemViewModelDictionary):
                CloseItemPopup();
                RefreshSlots();
                break;
            case nameof(InventoryUIViewModel.TalismanItemViewModel):
            case nameof(InventoryUIViewModel.FishingItemViewModel):
            case nameof(InventoryUIViewModel.LoggingItemViewModel):
            case nameof(InventoryUIViewModel.MiningItemViewModel):
                CloseItemPopup();
                RefreshEquipmentSlots();
                break;

        }
    }

    private void EnsureSlots()
    {
        // UIManager가 창을 재사용하므로 슬롯도 최초 연결 때만 생성한다.
        while (_inventorySlots.Count < _InventoryUIViewModel.InventoryMaxSlotNumber)
        {
            InventorySlotUIView slot = CreateSlot(_inventorySlotRoot);
            if (slot == null)
                return;

            _inventorySlots.Add(slot);
        }

        if (_equipmentSlots.Count == 0)
        {
            CreateEquipmentSlot(_talismanSlotBackground);
            CreateEquipmentSlot(_loggingSlotBackground);
            CreateEquipmentSlot(_fishingSlotBackground);
            CreateEquipmentSlot(_miningSlotBackground);
        }
    }

    private void RefreshSlots()
    {
        int index = 0;
        var items = _InventoryUIViewModel?.InventoryItemViewModelDictionary;

        if (items != null)
        {
            foreach (var pair in items)
            {
                if (pair.Value == null)
                    continue;

                if (index >= _inventorySlots.Count)
                    break;

                _inventorySlots[index].Bind(pair.Value);
                index++;
            }
        }

        for (; index < _inventorySlots.Count; index++)
            _inventorySlots[index].Bind(null);
    }

    private InventorySlotUIView CreateSlot(Transform root)
    {
        if (root == null || _inventorySlotPrefab == null)
            return null;

        var slotObject = Instantiate(_inventorySlotPrefab, root);
        var slotView = slotObject.GetComponent<InventorySlotUIView>();

        if (slotView == null)
        {
            Destroy(slotObject);
            Debug.LogError("InventorySlotUIView가 프리팹에 없습니다.", _inventorySlotPrefab);
            return null;
        }

        slotView.Bind(null);
        slotView.ItemOnClickEvent += ItemOnClick;
        return slotView;
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
        ClearInventorySelection();
        _selectedItem = item;

        // 아이템 정보 표시
        _nameText.text = item.Name;
        _descriptionText.text = item.Description;

        _ItemUseButton.gameObject.SetActive(false);
        _ItemAddQuickSlotButton.gameObject.SetActive(false);
        InventoryService inventory = ServiceManager.Inst.InventoryService;
        if (item.ItemType == nameof(ItemType.Equipment))
        {
            _ItemUseButton.gameObject.SetActive(true);
            _ItemUseText.text = inventory.IsEquippedItem(item) ? "장비 해제" : "장비 장착";
        }
        else if (inventory.CanUseItem(item))
        {
            // 재료도 소비 효과 데이터가 있으면 사용할 수 있습니다.
            _ItemUseButton.gameObject.SetActive(true);
            _ItemUseText.text = "아이템 사용";
        }

        if (item.ItemType == nameof(ItemType.Consumable))
        {
            _ItemAddQuickSlotButton.gameObject.SetActive(true);
            _ItemUseButton.gameObject.SetActive(true);
            _ItemUseText.text = "사용";
            int registeredIndex = inventory.GetQuickSlotIndex(item.Id);
            bool canRegister = inventory.CanRegisterQuickSlot(item);
            _ItemAddQuickSlotButton.interactable = canRegister;
            _ItemAddQuickSlotText.text = registeredIndex >= 0
                ? $"등록됨 ({registeredIndex + 1})"
                : canRegister ? "퀵슬롯" : "등록 불가";
        }

        _ItemUseButton.interactable = item.ItemType == nameof(ItemType.Equipment)
            || inventory.CanUseItem(item);
        _ItemRemoveText.text = "아이템 제거";
        _ItemRemoveButton.interactable = !inventory.IsEquippedItem(item);

        _itemPopupUI.SetActive(true);

        Canvas.ForceUpdateCanvases();
        PositionPopupAtMouse();
    }

    private void PositionPopupAtMouse()
    {
        RectTransform popup = (RectTransform)_itemPopupUI.transform;
        Canvas canvas = popup.GetComponentInParent<Canvas>().rootCanvas;
        RectTransform canvasRect = (RectTransform)canvas.transform;

        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            UnityEngine.InputSystem.Mouse.current.position.ReadValue(),
            camera,
            out Vector2 position))
        {
            return;
        }

        // 중간 부모의 크기 배율까지 반영
        Vector2 size = new Vector2(
            popup.rect.width * Mathf.Abs(
                popup.lossyScale.x / canvasRect.lossyScale.x),
            popup.rect.height * Mathf.Abs(
                popup.lossyScale.y / canvasRect.lossyScale.y));

        Rect area = canvasRect.rect;
        Vector2 pivot = popup.pivot;

        position.x = Mathf.Clamp(position.x,
            area.xMin + size.x * pivot.x,
            area.xMax - size.x * (1f - pivot.x));

        position.y = Mathf.Clamp(position.y,
            area.yMin + size.y * pivot.y,
            area.yMax - size.y * (1f - pivot.y));

        popup.position = canvasRect.TransformPoint(
            new Vector3(position.x, position.y, 0f));
    }

    private void CloseItemPopup()
    {
        ClearInventorySelection();
        _selectedItem = null;

        if (_itemPopupUI != null)
            _itemPopupUI.SetActive(false);
    }

    private void SetMouseOnlyNavigation(Button button)
    {
        if (button == null)
            return;

        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
    }

    private void ClearInventorySelection()
    {
        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
        if (selected != null && selected.transform.IsChildOf(transform))
            eventSystem.SetSelectedGameObject(null);
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

        if (!inventory.RequestUseItem(item))
            return;

        CloseItemPopup();
    }

    // 아이템 퀵슬롯 등록 버튼
    private void OnClickItemAddQuickSlotButton()
    {
        if (_selectedItem == null)
            return;

        if (ServiceManager.Inst.InventoryService.TryRegisterQuickSlot(_selectedItem))
            CloseItemPopup();
    }

    // 아이템 제거 버튼
    private void OnClickItemRemoveButton()
    {
        if (_selectedItem == null)
            return;

        InventoryItemViewModel item = _selectedItem;

        InventoryService inventory = ServiceManager.Inst.InventoryService;
        if (inventory.IsEquippedItem(item) || !inventory.DropItem(item.Id))
            return;

        inventory.RemoveItem(item);
        CloseItemPopup();
    }
    private void RemoveItemRequest(InventoryItemViewModel item)
    {
        ServiceManager.Inst.InventoryService.RemoveItem(item);
    }

    private void OnDestroy()
    {
        UnsubscribeInventory();
        ReleaseSlots(_inventorySlots);
        ReleaseSlots(_equipmentSlots);
    }

    private void ReleaseSlots(List<InventorySlotUIView> slots)
    {
        foreach (var slot in slots)
        {
            if (slot == null)
                continue;

            slot.ItemOnClickEvent -= ItemOnClick;
            slot.Unbind();
        }

        slots.Clear();
    }

    private void RefreshEquipmentSlots()
    {
        if (_equipmentSlots.Count != 4)
            return;

        _equipmentSlots[0].Bind(_InventoryUIViewModel?.TalismanItemViewModel);
        _equipmentSlots[1].Bind(_InventoryUIViewModel?.LoggingItemViewModel);
        _equipmentSlots[2].Bind(_InventoryUIViewModel?.FishingItemViewModel);
        _equipmentSlots[3].Bind(_InventoryUIViewModel?.MiningItemViewModel);
    }

    private void CreateEquipmentSlot(Sprite background)
    {
        InventorySlotUIView slotView = CreateSlot(_EquipmentSlotRoot);
        if (slotView == null)
            return;

        _equipmentSlots.Add(slotView);
        slotView.SetBackground(background);
    }

    private void OnClickPopupCloseButton()
    {
        CloseItemPopup();
    }
}
