using System.ComponentModel;
using UnityEngine;

public class FishingUI : UIBase
{
    [SerializeField] private FishingHudGraphic _hud;
    private InteractionFishingVM _viewModel;

    public void Bind(InteractionFishingVM viewModel)
    {
        Unbind();
        _viewModel = viewModel;
        if (_viewModel == null) return;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        RefreshHUD();
    }

    public void Unbind()
    {
        if (_hud != null) _hud.enabled = false;
        if (_viewModel == null) return;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = null;
    }

    public void ShowJudgment()
    {
        if (_viewModel == null || _hud == null)
            throw new System.InvalidOperationException("낚시 판정 UI가 준비되지 않았습니다.");
        RefreshHUD();
        _hud.enabled = true;
    }

    private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        RefreshHUD();
    }

    private void RefreshHUD()
    {
        if (_viewModel == null || _hud == null) return;
        _hud.SetState(_viewModel.CurrentAngle, _viewModel.SuccessZoneStartAngle,
            _viewModel.SuccessZoneAngle, _viewModel.PointerAngleWidth,
            _viewModel.FillProgress, _viewModel.IsWarning);
    }

    private void OnDisable()
    {
        Unbind();
    }
}
