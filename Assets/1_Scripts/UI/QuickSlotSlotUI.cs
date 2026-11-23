using System.ComponentModel;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuickSlotSlotUI : MonoBehaviour
{
    [Header("슬롯 표시")]
    [SerializeField] private TMP_Text _keyText;
    [SerializeField] private TMP_Text _countText;
    [SerializeField] private Image _icon;

    private InventoryItemViewModel _item;
    private int _iconVersion;

    public void Bind(InventoryItemViewModel item, int keyNumber)
    {
        if (_keyText != null)
            _keyText.text = keyNumber.ToString();

        if (_item != null && ReferenceEquals(_item, item))
            return;

        Unbind();
        _item = item;
        if (_item == null)
            return;

        _item.PropertyChanged += OnItemChanged;
        RefreshCount();
        RefreshIcon();
    }

    public void Unbind()
    {
        _iconVersion++;
        if (_item != null)
            _item.PropertyChanged -= OnItemChanged;
        _item = null;

        if (_countText != null)
            _countText.text = string.Empty;
        if (_icon != null)
        {
            _icon.sprite = null;
            _icon.enabled = false;
        }
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void OnItemChanged(object sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(InventoryItemViewModel.Count))
            RefreshCount();
        else if (args.PropertyName == nameof(InventoryItemViewModel.IconPath))
            RefreshIcon();
    }

    private void RefreshCount()
    {
        if (_countText != null)
            _countText.text = _item != null && _item.Count > 0 ? _item.Count.ToString() : string.Empty;
    }

    private void RefreshIcon()
    {
        int version = ++_iconVersion;
        if (_icon == null)
            return;

        _icon.sprite = null;
        _icon.enabled = false;
        if (_item != null && !string.IsNullOrEmpty(_item.IconPath) && ResourceManager.Inst != null)
            LoadIconAsync(_item.IconPath, version).Forget();
    }

    private async UniTaskVoid LoadIconAsync(string path, int version)
    {
        Sprite sprite = await ResourceManager.Inst.LoadAsset<Sprite>(path);
        if (this != null)
            ApplyLoadedIcon(sprite, version);
    }

    private void ApplyLoadedIcon(Sprite sprite, int version)
    {
        if (version != _iconVersion || _item == null || _icon == null || !isActiveAndEnabled)
            return;

        _icon.sprite = sprite;
        _icon.enabled = sprite != null;
    }
}
