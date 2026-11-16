using System.ComponentModel;
using UnityEngine;

public class GatheringUI : UIBase
{
    [SerializeField] private GatheringProgressGraphic _hud;

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
        if (_hud != null) _hud.enabled = false;
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
        if (_viewModel != null && _hud != null)
            _hud.SetProgress(_viewModel.GatheringProgress);
    }

    public void ShowProgress()
    {
        if (_viewModel == null || _hud == null)
            throw new System.InvalidOperationException("채집 UI가 준비되지 않았습니다.");
        RefreshProgress();
        _hud.enabled = true;
    }

    private void OnDisable()
    {
        Unbind();
    }

}
