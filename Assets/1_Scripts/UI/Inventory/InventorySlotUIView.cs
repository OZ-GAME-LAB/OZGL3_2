using Cysharp.Threading.Tasks;
using System;
using System.ComponentModel;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventorySlotUIView : MonoBehaviour
{
    [SerializeField] private Button _useButton;
    [SerializeField] private TMP_Text _itemName;
    [SerializeField] private TMP_Text _itemCount;
    [SerializeField] private Image Image_ItemIcon;
    [SerializeField] private Image Image_BackGround;

    private InventoryItemViewModel _viewModel;
    private int _iconLoadVersion;
    public event Action<InventorySlotUIView, InventoryItemViewModel> ItemOnClickEvent;

    private void Awake()
    {
        if (_useButton == null)
            return;

        Navigation navigation = _useButton.navigation;
        navigation.mode = Navigation.Mode.None;
        _useButton.navigation = navigation;
    }

    public void Bind(InventoryItemViewModel viewModel)
    {
        if (_viewModel != null && ReferenceEquals(_viewModel, viewModel))
            return;

        Unbind();

        _viewModel = viewModel;

        if (_viewModel == null || string.IsNullOrEmpty(_viewModel.Id))
        {
            _viewModel = null;
            return;
        }

        _viewModel.PropertyChanged += OnItemPropertyChanged;
        _viewModel.InvokeOnceOnInit();

    }
    public void SetBackground(Sprite background)
    {
        if (Image_BackGround == null || background == null)
            return;

        Image_BackGround.sprite = background;
    }

    private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(_viewModel.Name):
                {
                    _itemName.text = _viewModel.Name;

                    break;
                }
            case nameof(_viewModel.Count):
                {
                    if(_viewModel.ItemType == nameof(ItemType.Equipment) || _viewModel.Count == 0){
                        _itemCount.text = null;
                    }
                    else
                    {
                        _itemCount.text = _viewModel.Count.ToString();

                    }

                    break;
                }
            case nameof(_viewModel.IconPath):
                {
                    int version = ++_iconLoadVersion;
                    Image_ItemIcon.enabled = false;
                    Image_ItemIcon.sprite = null;

                    if (!string.IsNullOrEmpty(_viewModel.IconPath))
                        LoadItemIconAsync(_viewModel.IconPath, version).Forget();

                    break;
                }
        }

        
    }



    private void OnEnable()
    {
        if (_useButton != null)
            _useButton.onClick.AddListener(OnClickUseButton);
    }

    private void OnDisable()
    {
        if (_useButton != null)
            _useButton.onClick.RemoveListener(OnClickUseButton);
    }

    private void OnDestroy()
    {
        Unbind();

    }

    public void Unbind()
    {
        // 이전 아이템의 비동기 로딩 결과가 재사용한 슬롯을 덮지 못하게 한다.
        _iconLoadVersion++;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnItemPropertyChanged;
            _viewModel = null;
        }

        if (_itemName != null)
            _itemName.text = string.Empty;
        if (_itemCount != null)
            _itemCount.text = string.Empty;
        if (Image_ItemIcon != null)
        {
            Image_ItemIcon.sprite = null;
            Image_ItemIcon.enabled = false;
        }
    }

    private void OnClickUseButton()
    {
        if (_viewModel != null)
            ItemOnClickEvent?.Invoke(this, _viewModel);
    }



    private async UniTaskVoid LoadItemIconAsync(string iconPath, int version)
    {
        Sprite sprite = await ResourceManager.Inst.LoadAsset<Sprite>(iconPath);

        if (this == null || version != _iconLoadVersion || Image_ItemIcon == null)
            return;

        Image_ItemIcon.sprite = sprite;
        Image_ItemIcon.enabled = sprite != null;
    }
}
