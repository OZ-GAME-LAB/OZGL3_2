
using System;

public class InteractionLoggingMiningVM : InteractionVMBase
{
    public override string InteractionInfoText => "벌목 / 채광 (F를 눌러 시작)";
    public const int TotalRounds = 3;

    private float _startSizeRatio;
    private float _endSizeRatio;
    private float _successStartRatio;
    private float _perfectTolerance;
    private float _shrinkSpeed;

    private bool _isRunning;
    private float _accuracySum;
    private float _currentSizeRatio;

    public int CompletedRounds { get; private set; }
    public float LastAccuracy { get; private set; }

    public float FinalAccuracy => _accuracySum / TotalRounds;

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

    public void Initialize(float startSizeRatio, float endSizeRatio, float shrinkSpeed, float successStartRatio, float perfectTolerance)
    {
        if (perfectTolerance < 0f ||
            endSizeRatio < 0f ||
            endSizeRatio >= 1f - perfectTolerance ||
            successStartRatio <= 1f + perfectTolerance ||
            startSizeRatio <= successStartRatio ||
            shrinkSpeed <= 0f)
        {
            throw new ArgumentException("수축 크기와 판정 범위를 확인해 주세요.");
        }

        _startSizeRatio = startSizeRatio;
        _endSizeRatio = endSizeRatio;
        _shrinkSpeed = shrinkSpeed;
        _successStartRatio = successStartRatio;
        _perfectTolerance = perfectTolerance;

        _isRunning = false;
        CurrentSizeRatio = _startSizeRatio;
    }

    public void BeginGame()
    {
        CompletedRounds = 0;
        LastAccuracy = 0f;
        _accuracySum = 0f;

        NotifyResultChanged();
        BeginRound();
    }

    private void BeginRound()
    {
        CurrentSizeRatio = _startSizeRatio;
        _isRunning = true;
    }

    public bool UpdateRound(float deltaTime)
    {
        if (!_isRunning || deltaTime <= 0f)
            return false;

        CurrentSizeRatio = Math.Max(
            _endSizeRatio,
            CurrentSizeRatio - _shrinkSpeed * deltaTime);

        if (CurrentSizeRatio <= _endSizeRatio)
            return FinishRound(0f);

        return false;
    }

    public bool Confirm()
    {
        if (!_isRunning)
            return false;

        return FinishRound(CalculateAccuracy());
    }

    private float CalculateAccuracy()
    {
        float size = CurrentSizeRatio;

        if (size >= _successStartRatio || size <= _endSizeRatio)
            return 0f;

        float perfectMin = 1f - _perfectTolerance;
        float perfectMax = 1f + _perfectTolerance;

        if (size >= perfectMin && size <= perfectMax)
            return 1f;

        if (size > perfectMax)
        {
            return (_successStartRatio - size) / (_successStartRatio - perfectMax);
        }

        return (size - _endSizeRatio) / (perfectMin - _endSizeRatio);
    }

    private bool FinishRound(float accuracy)
    {
        _isRunning = false;

        LastAccuracy = accuracy;
        _accuracySum += accuracy;
        CompletedRounds++;

        NotifyResultChanged();

        if (CompletedRounds >= TotalRounds)
            return true;

        BeginRound();
        return false;
    }

    public void Cancel()
    {
        _isRunning = false;
        CurrentSizeRatio = _startSizeRatio;
    }

    private void NotifyResultChanged()
    {
        OnPropertyChanged(nameof(CompletedRounds));
        OnPropertyChanged(nameof(LastAccuracy));
        OnPropertyChanged(nameof(FinalAccuracy));
    }

    public void InvokeOnceOnInit()
    {
        OnPropertyChanged(nameof(CurrentSizeRatio));
        NotifyResultChanged();
    }
}
