using System.ComponentModel;
using TMPro;
using UnityEngine;

public class InteractableInfoUI : UIBase
{
    [SerializeField] TMP_Text _infoText;

    [Header("안내 색상")]
    [SerializeField] private Color _availableColor = Color.white;
    [SerializeField] private Color _unavailableColor = new Color(1f, 0.65f, 0.65f, 1f);

    private InteractionVMBase _viewModel;

    public void Bind(InteractionVMBase viewModel)
    {
        Unbind();

        _viewModel = viewModel;

        if (_viewModel == null)
            return;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        RefreshInfo();
    }

    public void Unbind()
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }

        _infoText.text = string.Empty;
    }

    private void OnViewModelPropertyChanged(
        object sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InteractionVMBase.HasRequiredTool)
            || e.PropertyName == nameof(InteractionVMBase.IsAvailable)
            || e.PropertyName == nameof(InteractionVMBase.IsUsedByOtherPlayer)
            || e.PropertyName == nameof(InteractionVMBase.InteractionInfoText))
        {
            RefreshInfo();
        }
    }

    private void RefreshInfo()
    {
        _infoText.color = _viewModel.HasRequiredTool && _viewModel.IsAvailable
            ? _availableColor
            : _unavailableColor;

        if (!_viewModel.HasRequiredTool)
        {
            _infoText.text = "도구 필요";
            return;
        }

        if (_viewModel.IsAvailable)
        {
            _infoText.text = _viewModel.InteractionInfoText;
            return;
        }

        _infoText.text = _viewModel.IsUsedByOtherPlayer
            ? "사용 중"
            : "상호작용 불가";
    }

    private void OnDisable()
    {
        Unbind();
    }
}
