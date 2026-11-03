using System.ComponentModel;
using UnityEngine;

public class LoggingMiningUI : UIBase
{
    [SerializeField] private RectTransform _standardCircle;
    [SerializeField] private RectTransform _shrinkageCircle;

    private InteractionLoggingMiningVM _viewModel;

    public void Bind(InteractionLoggingMiningVM viewModel)
    {
        Unbind();

        _viewModel = viewModel;

        if (_viewModel == null)
            return;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        RefreshCircleSize();
    }

    public void Unbind()
    {
        if (_viewModel == null)
            return;

        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = null;
    }

    private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InteractionLoggingMiningVM.CurrentSizeRatio))
        {
            RefreshCircleSize();
        }
    }

    private void RefreshCircleSize()
    {
        if (_viewModel == null)
            return;

        Vector2 size = _standardCircle.rect.size * _viewModel.CurrentSizeRatio;

        _shrinkageCircle.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);

        _shrinkageCircle.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
    }

    private void OnDisable()
    {
        Unbind();
    }
}
