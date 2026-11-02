using System;
using System.ComponentModel;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RuneSlotUI : UIBase
{
    [SerializeField] private Button Button_Select;
    [SerializeField] private Image Image_Icon;
    [SerializeField] private TMP_Text Text_Name;
    [SerializeField] private GameObject Object_Selected;

    private RuneSlotViewModel _viewModel;
    private Action<RuneSlotViewModel> _onClicked;
    private int _iconVersion;

    private void Awake()
    {
        Button_Select.onClick.AddListener(OnClick);
    }

    public void Bind(RuneSlotViewModel viewModel, Action<RuneSlotViewModel> onClicked)
    {
        Unbind();

        _viewModel = viewModel;
        _onClicked = onClicked;

        if (_viewModel == null)
        {
            return;
        }

        _viewModel.PropertyChanged += OnPropertyChanged;
        _viewModel.InvokeOnceOnInit();
    }

    public void Unbind()
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnPropertyChanged;
        }

        _viewModel = null;
        _onClicked = null;
        _iconVersion++;

        if (Image_Icon != null)
        {
            Image_Icon.sprite = null;
            Image_Icon.enabled = false;
        }

        if (Object_Selected != null)
        {
            Object_Selected.SetActive(false);
        }

        if (Text_Name != null)
        {
            Text_Name.text = "";
        }
    }

    private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(RuneSlotViewModel.RuneId):
                RefreshRune();
                break;

            case nameof(RuneSlotViewModel.IsSelected):
                if (Object_Selected != null)
                {
                    Object_Selected.SetActive(_viewModel.IsSelected);
                }
                break;
        }
    }

    private void RefreshRune()
    {
        int version = ++_iconVersion;
        string runeId = _viewModel.RuneId;

        if (Image_Icon != null)
        {
            Image_Icon.sprite = null;
            Image_Icon.enabled = false;
        }

        if (Text_Name != null)
        {
            Text_Name.text = "빈 슬롯";
        }

        if (string.IsNullOrEmpty(runeId) || DataManager.Inst == null)
        {
            return;
        }

        ItemData data = DataManager.Inst.GetData<ItemData>(runeId);

        if (Text_Name != null)
        {
            Text_Name.text = data != null ? data.Name : runeId;
        }

        if (data == null || string.IsNullOrEmpty(data.IconPath))
        {
            return;
        }

        LoadIconAsync(data.IconPath, version).Forget();
    }

    private async UniTask LoadIconAsync(string path, int version)
    {
        if (ResourceManager.Inst == null)
        {
            return;
        }

        Sprite sprite = await ResourceManager.Inst.LoadSprite(path);

        if (this == null ||_viewModel == null || version != _iconVersion || Image_Icon == null)
        {
            return;
        }

        Image_Icon.sprite = sprite;
        Image_Icon.enabled = sprite != null;
    }

    private void OnClick()
    {
        if (_viewModel != null)
        {
            _onClicked?.Invoke(_viewModel);
        }
    }

    private void OnDestroy()
    {
        Unbind();
    }
}