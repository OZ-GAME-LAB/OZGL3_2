
using System;

public class InteractionLoggingMiningVM : InteractionVMBase
{
    public override string InteractionInfoText => "벌목 / 채광 (F 길게 누르기)";

    private float _startSizeRatio;
    private float _endSizeRatio;
    private float _shrinkSpeed;
    private bool _isRunning;

    private float _currentSizeRatio;

    public float CurrentSizeRatio
    {
        get => _currentSizeRatio;
        private set
        {
            if (_currentSizeRatio == value)
                return;

            _currentSizeRatio = value;
            OnPropertyChanged(nameof(CurrentSizeRatio));
        }
    }

    public void Initialize(float startSizeRatio, float endSizeRatio, float shrinkSpeed)
    {
        if (startSizeRatio <= endSizeRatio || endSizeRatio < 0f || shrinkSpeed <= 0f)
        {
            throw new ArgumentException("수축 크기와 속도를 확인해 주세요.");
        }

        _startSizeRatio = startSizeRatio;
        _endSizeRatio = endSizeRatio;
        _shrinkSpeed = shrinkSpeed;

        _isRunning = false;
        CurrentSizeRatio = _startSizeRatio;
    }

    public void BeginRound()
    {
        CurrentSizeRatio = _startSizeRatio;
        _isRunning = _shrinkSpeed > 0f;
    }

    public void UpdateRound(float deltaTime)
    {
        if (!_isRunning || deltaTime <= 0f)
            return;

        CurrentSizeRatio = Math.Max(_endSizeRatio, CurrentSizeRatio - _shrinkSpeed * deltaTime);

        if (CurrentSizeRatio <= _endSizeRatio)
            _isRunning = false;
    }

    public void Cancel()
    {
        _isRunning = false;
        CurrentSizeRatio = _startSizeRatio;
    }

    public void InvokeOnceOnInit()
    {
        OnPropertyChanged(nameof(CurrentSizeRatio));
    }
}
