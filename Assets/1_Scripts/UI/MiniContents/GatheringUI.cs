using System.ComponentModel;
using UnityEngine;
using UnityEngine.UI;

public class GatheringUI : UIBase
{
    [SerializeField] private Slider _progressSlider;

    private InteractionGatheringVM _viewModel;

    public void Bind(InteractionGatheringVM viewModel)
    {
        Unbind();

        _viewModel = viewModel;

        if (_viewModel == null)
            return;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        RefreshProgress();
    }

    public void Unbind()
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }
    }

    private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InteractionGatheringVM.GatheringProgress))
        {
            RefreshProgress();
        }
    }

    private void RefreshProgress()
    {
        _progressSlider.value = _viewModel.GatheringProgress;
    }

    private void OnDisable()
    {
        Unbind();
    }

}
