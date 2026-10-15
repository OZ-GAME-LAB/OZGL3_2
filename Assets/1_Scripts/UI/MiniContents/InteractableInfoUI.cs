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
        if (e.PropertyName == nameof(InteractionVMBase.IsAvailable)
            || e.PropertyName == nameof(InteractionVMBase.InteractionInfoText))
        {
            RefreshInfo();
        }
    }

    private void RefreshInfo()
    {
        _infoText.text = _viewModel.IsAvailable ? _viewModel.InteractionInfoText : "상호작용 불가";
    }

    private void OnDisable()
    {
        Unbind();
    }
}
