using System;
using System.ComponentModel;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SaveSelectionSlotUI : MonoBehaviour
{
    [SerializeField] private TMP_Text Text_Name;
    [SerializeField] private TMP_Text Text_SavedTime;
    [SerializeField] private Button Button_Select;
    [SerializeField] private GameObject SelectedMark;

    private string _pendingDeleteId;

    private SaveSelectionSlotViewModel _viewModel;
    private Action<string> _onSelected;

    private void Awake()
    {
        Button_Select.onClick.AddListener(OnClickSelect);
    }

    public void Bind(
        SaveSelectionSlotViewModel viewModel,
        Action<string> onSelected)
    {
        Unbind();

        _viewModel = viewModel;
        _onSelected = onSelected;

        if (_viewModel == null)
            return;

        _viewModel.PropertyChanged += OnPropertyChanged;
        _viewModel.InvokeOnceOnInit();
    }

    public void Unbind()
    {
        if (_viewModel != null)
            _viewModel.PropertyChanged -= OnPropertyChanged;

        _viewModel = null;
        _onSelected = null;

        if (SelectedMark != null)
            SelectedMark.SetActive(false);
    }

    private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (_viewModel == null)
            return;

        switch (e.PropertyName)
        {
            case nameof(SaveSelectionSlotViewModel.DisplayName):
                Text_Name.text = _viewModel.DisplayName;
                break;

            case nameof(SaveSelectionSlotViewModel.SavedTimeText):
                Text_SavedTime.text = _viewModel.SavedTimeText;
                break;

            case nameof(SaveSelectionSlotViewModel.IsSelected):
                if (SelectedMark != null)
                    SelectedMark.SetActive(_viewModel.IsSelected);
                break;
        }
    }

    private void OnClickSelect()
    {
        if (_viewModel != null)
            _onSelected?.Invoke(_viewModel.SaveId);
    }

    private void OnDestroy()
    {
        Unbind();

        if (Button_Select != null)
        {
            Button_Select.onClick.RemoveListener(OnClickSelect);
        }
    }
}
