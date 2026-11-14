using System.ComponentModel;
using TMPro;
using UnityEngine;

public class InteractableInfoUI : UIBase
{
    [SerializeField] TMP_Text _infoText;

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
        if (!_viewModel.HasRequiredTool)
        {
            _infoText.text = "상호작용 불가\n(관련 도구 없음)";
            return;
        }

        if (_viewModel.IsAvailable)
        {
            _infoText.text = _viewModel.InteractionInfoText;
            return;
        }

        _infoText.text = _viewModel.IsUsedByOtherPlayer
            ? "상호작용 불가\n(다른 유저 사용 중)"
            : "상호작용 불가";
    }

    private void OnDisable()
    {
        Unbind();
    }
}
