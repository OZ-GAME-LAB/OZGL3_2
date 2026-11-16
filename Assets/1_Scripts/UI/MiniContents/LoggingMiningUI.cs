using System.ComponentModel;
using UnityEngine;

public class LoggingMiningUI : UIBase
{
    [SerializeField] private ResourceTimingGraphic _hud;

    private InteractionLoggingMiningVM _viewModel;

    [Header("결과 표시")]
    [SerializeField, Min(0f)] private float _resultDisplaySeconds = 1f;

    private bool _isShowingResult;
    private float _resultRemainingSeconds;

    private void Update()
    {
        if (!_isShowingResult)
            return;

        _resultRemainingSeconds -= Time.unscaledDeltaTime;

        if (_resultRemainingSeconds <= 0f)
        {
            TryCloseResult();
        }
    }

    public void Bind(InteractionLoggingMiningVM viewModel)
    {
        _isShowingResult = false;
        _resultRemainingSeconds = 0f;

        Unbind();

        _viewModel = viewModel;

        if (_viewModel == null)
            return;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        RefreshHUD();
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
        if (e.PropertyName == nameof(InteractionLoggingMiningVM.CurrentSizeRatio) ||
            e.PropertyName == nameof(InteractionLoggingMiningVM.CompletedRounds))
            RefreshHUD();
    }

    private void RefreshHUD()
    {
        if (_viewModel == null || _hud == null)
            return;

        _hud.SetState(_viewModel.CurrentSizeRatio, _viewModel.StartSizeRatio,
            _viewModel.EndSizeRatio,
            (float)_viewModel.CompletedRounds / InteractionLoggingMiningVM.TotalRounds);
    }

    private void OnDisable()
    {
        _isShowingResult = false;
        _resultRemainingSeconds = 0f;
        Unbind();
    }

    public void ShowFinalResult()
    {
        Unbind();

        _isShowingResult = true;
        _resultRemainingSeconds = _resultDisplaySeconds;
    }

    public bool TryCloseResult()
    {
        if (!_isShowingResult)
            return false;

        _isShowingResult = false;
        _resultRemainingSeconds = 0f;

        UIManager.Inst.Close(UIType.LoggingMiningUI);
        return true;
    }
}
