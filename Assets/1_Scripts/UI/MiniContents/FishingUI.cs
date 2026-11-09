using System.ComponentModel;
using UnityEngine;
using UnityEngine.UI;

public class FishingUI : UIBase
{
    [SerializeField] private RectTransform _pointerPivot;
    [SerializeField] private Image _successZone;
    [SerializeField] private Image _fillGauge;

    [System.Serializable]
    private class FailureImages
    {
        public GameObject Remaining;
        public GameObject Failed;
    }
    [SerializeField]
    private FailureImages[] _failureImages = new FailureImages[3];

    private InteractionFishingVM _viewModel;

    public void Bind(InteractionFishingVM viewModel)
    {
        Unbind();

        _viewModel = viewModel;

        if (_viewModel == null)
            return;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        RefreshPointer();
        RefreshSuccessZone();
        RefreshFillGauge();
        RefreshFailures();
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
        switch (e.PropertyName)
        {
            case nameof(InteractionFishingVM.CurrentAngle):
                RefreshPointer();
                break;

            case nameof(InteractionFishingVM.SuccessZoneStartAngle):
            case nameof(InteractionFishingVM.SuccessZoneAngle):
                RefreshSuccessZone();
                break;

            case nameof(InteractionFishingVM.FillProgress):
                RefreshFillGauge();
                break;

            case nameof(InteractionFishingVM.FailureCount):
                RefreshFailures();
                break;
        }
    }

    private void RefreshPointer()
    {
        if (_viewModel == null)
            return;

        _pointerPivot.localRotation = Quaternion.Euler(0f, 0f, -_viewModel.CurrentAngle);
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void RefreshSuccessZone()
    {
        if (_viewModel == null)
            return;

        _successZone.fillAmount = _viewModel.SuccessZoneAngle / 360f;

        _successZone.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -_viewModel.SuccessZoneStartAngle);
    }

    private void RefreshFillGauge()
    {
        if (_viewModel == null)
            return;

        _fillGauge.fillAmount = _viewModel.FillProgress;
    }

    private void RefreshFailures()
    {
        if (_viewModel == null)
            return;

        for (int i = 0; i < _failureImages.Length; i++)
        {
            bool failed = i < _viewModel.FailureCount;

            _failureImages[i].Remaining.SetActive(!failed);
            _failureImages[i].Failed.SetActive(failed);
        }
    }
}
