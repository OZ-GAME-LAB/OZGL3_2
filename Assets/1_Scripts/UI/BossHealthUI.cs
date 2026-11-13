using System.ComponentModel;
using UnityEngine;
using UnityEngine.UI;

public class BossHealthUI : UIBase
{
    [SerializeField] private Slider _healthSlider;

    public MonsterVM ViewModel { get; private set; }

    public bool Bind(MonsterVM viewModel)
    {
        Unbind();

        if (_healthSlider == null || viewModel == null)
        {
            Debug.LogError("보스 체력 HUD의 Slider와 ViewModel 연결을 확인하세요.", this);
            return false;
        }

        ViewModel = viewModel;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        RefreshHealth();
        return true;
    }

    public void Unbind()
    {
        if (ViewModel == null)
            return;

        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel = null;
    }

    private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MonsterVM.CurHp)
            || e.PropertyName == nameof(MonsterVM.MaxHp)
            || string.IsNullOrEmpty(e.PropertyName))
            RefreshHealth();
    }

    private void RefreshHealth()
    {
        _healthSlider.normalizedValue = ViewModel.MaxHp > 0f
            ? Mathf.Clamp01(ViewModel.CurHp / ViewModel.MaxHp)
            : 0f;
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void OnDestroy()
    {
        Unbind();
    }
}
