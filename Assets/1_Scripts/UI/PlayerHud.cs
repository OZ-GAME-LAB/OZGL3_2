using System.ComponentModel;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHud : UIBase
{
    [SerializeField] private Slider _healthSlider;
    [SerializeField] private TMP_Text _healthText;


    private PlayerStatViewModel _viewModel;

    private void OnEnable()
    {
        Bind();
    }

    private void OnDisable()
    {
        Unbind();
    }


    private bool Bind()
    {
        Unbind();

        if (!NetworkManager.Singleton.LocalClient.PlayerObject.TryGetComponent<PlayerNetworkStats>(out var stats))
        {
            return false;
        }

        _viewModel = stats.ViewModel;

        _viewModel.PropertyChanged += OnPropertyChanged;
        _viewModel.InvokeOnceOnInit();

        return true;
    }

    private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {

            case nameof(_viewModel.CurHp):
            case nameof(_viewModel.MaxHp): // MaxHp 변화에도 대응
                UpdateHealthBar();
                break;
        }
    }


    private void UpdateHealthBar()
    {
        if (_healthSlider == null || _viewModel == null)
            return;

        _healthSlider.minValue = 0f;
        _healthSlider.maxValue = _viewModel.MaxHp;
        _healthSlider.value = _viewModel.CurHp;
        _healthText.text = _viewModel.CurHp.ToString();
    }

    private void Unbind()
    {
        if (_viewModel != null)
            _viewModel.PropertyChanged -= OnPropertyChanged;

        _viewModel = null;
    }
}
