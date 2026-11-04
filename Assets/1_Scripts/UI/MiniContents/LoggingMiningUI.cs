using System.ComponentModel;
using UnityEngine;

public class LoggingMiningUI : UIBase
{
    [SerializeField] private RectTransform _standardCircle;
    [SerializeField] private RectTransform _shrinkageCircle;

    private InteractionLoggingMiningVM _viewModel;

    [Header("결과 표시")]
    [SerializeField, Min(0f)] private float _resultDisplaySeconds = 1f;

    private bool _isShowingResult;
    private float _resultRemainingSeconds;

    [System.Serializable]
    private class RoundImages
    {
        public GameObject NotProceeding;
        public GameObject Success;
        public GameObject Fail;
    }

    [SerializeField] private RoundImages[] _roundImages = new RoundImages[3];

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

        RefreshCircleSize();
        ResetRoundImages();
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
        else if (e.PropertyName == nameof(InteractionLoggingMiningVM.CompletedRounds))
        {
            RefreshRoundResult();
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
        _isShowingResult = false;
        _resultRemainingSeconds = 0f;
        Unbind();
    }

    private void ResetRoundImages()
    {
        foreach (RoundImages images in _roundImages)
        {
            images.NotProceeding.SetActive(true);
            images.Success.SetActive(false);
            images.Fail.SetActive(false);
        }
    }

    private void RefreshRoundResult()
    {
        if (_viewModel.CompletedRounds == 0)
        {
            ResetRoundImages();
            return;
        }

        int index = _viewModel.CompletedRounds - 1;

        if (index < 0 || index >= _roundImages.Length)
            return;

        RoundImages images = _roundImages[index];
        bool succeeded = _viewModel.LastAccuracy > 0f;

        images.NotProceeding.SetActive(false);
        images.Success.SetActive(succeeded);
        images.Fail.SetActive(!succeeded);
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
