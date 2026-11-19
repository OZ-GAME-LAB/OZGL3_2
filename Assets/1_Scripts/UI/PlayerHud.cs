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
    private PlayerNetworkStats _boundStats;

    private void OnEnable()
    {
        Bind();
    }

    private void LateUpdate()
    {
        // 연결된 동안에는 체력을 매 프레임 읽지 않는다.
        // 이전 플레이어가 사라졌거나 아직 VM에 연결되지 않았을 때만 다시 시도한다.
        if (_boundStats == null || _boundStats.IsSpawned == false || _viewModel == null)
        {
            Bind();
        }
    }

    private void Bind()
    {
        NetworkManager network = NetworkManager.Singleton;

        if (network == null || network.IsClient == false || network.LocalClient == null)
        {
            return;
        }

        NetworkObject player = network.LocalClient.PlayerObject;

        if (player == null || player.IsSpawned == false)
        {
            return;
        }

        if (player.TryGetComponent(out PlayerNetworkStats stats) == false || stats.IsSpawned == false || stats.ViewModel == null)
        {
            return;
        }

        if (_boundStats == stats && _viewModel == stats.ViewModel)
        {
            return;
        }

        Unbind();

        _boundStats = stats;
        _viewModel = stats.ViewModel;
        _viewModel.PropertyChanged += OnPropertyChanged;

        UpdateHealthBar();
    }

    private void Unbind()
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnPropertyChanged;
        }

        _viewModel = null;
        _boundStats = null;
    }

    private void OnDisable()
    {
        Unbind();
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
}
