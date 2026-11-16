
using System;

public class InteractionLoggingMiningVM : InteractionVMBase
{
    private string _interactionInfoText = "벌목하기 (F)";
    public override string InteractionInfoText => _interactionInfoText;
    public const int TotalRounds = 3;
    private const float MinimumSuccessAccuracy = 0.01f;

    private float _startSizeRatio;
    private float _endSizeRatio;
    private float _successStartRatio;
    private float _perfectTolerance;
    private float _shrinkSpeed;

    private bool _isRunning;
    private bool _awaitingMotion;
    private float _accuracySum;
    private float _currentSizeRatio;

    public float StartSizeRatio => _startSizeRatio;
    public float EndSizeRatio => _endSizeRatio;
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

    public void SetInteractionType(string interactionType)
    {
        string text = interactionType == "Mining" ? "채광하기 (F)" : "벌목하기 (F)";
        if (_interactionInfoText == text)
            return;

        _interactionInfoText = text;
        OnPropertyChanged(nameof(InteractionInfoText));
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
        _awaitingMotion = false;
        CompletedRounds = 0;
        LastAccuracy = 0f;
        _accuracySum = 0f;
        CurrentSizeRatio = _startSizeRatio;
        NotifyResultChanged();
    }

    public void BeginGame()
    {
        _awaitingMotion = false;
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

        float nextSizeRatio = CurrentSizeRatio - _shrinkSpeed * deltaTime;
        CurrentSizeRatio = Math.Max(0f, nextSizeRatio);

        if (nextSizeRatio < _endSizeRatio)
            return FinishRound(0f);

        return false;
    }

    public bool Confirm()
    {
        if (!_isRunning)
            return false;

        return FinishRound(CalculateAccuracy());
    }

    public bool TryConfirmAndWaitForMotion()
    {
        if (!_isRunning || _awaitingMotion) return false;
        _awaitingMotion = true;
        FinishRound(CalculateAccuracy(), false);
        return true;
    }

    public bool ResumeAfterMotion()
    {
        if (!_awaitingMotion) return false;
        _awaitingMotion = false;
        if (CompletedRounds >= TotalRounds) return true;
        BeginRound();
        return false;
    }

    private float CalculateAccuracy()
    {
        float size = CurrentSizeRatio;

        if (size > _successStartRatio || size < _endSizeRatio)
            return 0f;

        float perfectMin = 1f - _perfectTolerance;
        float perfectMax = 1f + _perfectTolerance;

        if (size >= perfectMin && size <= perfectMax)
            return 1f;

        if (size > perfectMax)
        {
            return Math.Max(MinimumSuccessAccuracy,
                (_successStartRatio - size) / (_successStartRatio - perfectMax));
        }

        return Math.Max(MinimumSuccessAccuracy,
            (size - _endSizeRatio) / (perfectMin - _endSizeRatio));
    }

    private bool FinishRound(float accuracy, bool advanceImmediately = true)
    {
        _isRunning = false;

        LastAccuracy = accuracy;
        _accuracySum += accuracy;
        CompletedRounds++;

        NotifyResultChanged();

        if (CompletedRounds >= TotalRounds)
            return true;

        if (advanceImmediately) BeginRound();
        return false;
    }

    public void Cancel()
    {
        _isRunning = false;
        _awaitingMotion = false;
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
