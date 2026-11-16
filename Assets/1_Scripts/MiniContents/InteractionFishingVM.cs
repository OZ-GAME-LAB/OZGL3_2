using UnityEngine;

public class InteractionFishingVM : InteractionVMBase
{
    public override string InteractionInfoText => "낚시하기 (F)";
    private const int FullGaugeSteps = 4;
    private const float ZoneSeparation = 8f;
    private const float PointerSeparation = 12f;
    private readonly float[] _candidateCenters = new float[360];
    private float _rotationSpeed;
    private bool _isRunning;
    private bool _canConfirm;
    private float _currentAngle;
    private int _gaugeSteps;

    public float SuccessZoneStartAngle { get; private set; }
    public float SuccessZoneAngle { get; private set; }
    public float PointerAngleWidth { get; private set; }
    public float FillProgress => (float)_gaugeSteps / FullGaugeSteps;
    public int FailureCount { get; private set; }
    public bool IsWarning { get; private set; }
    public bool IsSucceeded => _gaugeSteps == FullGaugeSteps;
    public bool IsFailed { get; private set; }

    public float CurrentAngle
    {
        get => _currentAngle;
        private set
        {
            if (_currentAngle == value) return;
            _currentAngle = value;
            OnPropertyChanged(nameof(CurrentAngle));
        }
    }

    public void Initialize(float rotationSpeed, float successZoneAngle = 54f, float pointerAngleWidth = 4f)
    {
        _rotationSpeed = IsFinite(rotationSpeed) && rotationSpeed > 0f ? rotationSpeed : 360f;
        // Leave enough circumference for a new zone that overlaps neither the old zone nor the pointer.
        SuccessZoneAngle = IsFinite(successZoneAngle) ? Mathf.Clamp(successZoneAngle, 1f, 90f) : 54f;
        PointerAngleWidth = IsFinite(pointerAngleWidth) ? Mathf.Clamp(pointerAngleWidth, 1f, 10f) : 4f;
        _isRunning = false;
        _canConfirm = false;
        ResetProgress();
        SuccessZoneStartAngle = 0f;
        CurrentAngle = 0f;
        InvokeOnceOnInit();
    }

    public void BeginGame()
    {
        ResetProgress();
        CurrentAngle = 0f;
        SelectNextZone(false);
        _isRunning = true;
        _canConfirm = true;
        InvokeOnceOnInit();
    }

    public bool Confirm()
    {
        if (!_isRunning || !_canConfirm) return false;
        _canConfirm = false;
        float zoneCenter = SuccessZoneStartAngle + SuccessZoneAngle * 0.5f;
        float distance = Mathf.Abs(Mathf.DeltaAngle(CurrentAngle, zoneCenter));
        bool succeeded = distance <= (SuccessZoneAngle + PointerAngleWidth) * 0.5f;

        if (succeeded)
        {
            _gaugeSteps++;
            IsWarning = false;
        }
        else
        {
            FailureCount++;
            if (_gaugeSteps > 0) _gaugeSteps--;
            else if (IsWarning) IsFailed = true;
            else IsWarning = true;
        }

        bool finished = IsSucceeded || IsFailed;
        if (finished) _isRunning = false;
        else SelectNextZone(true);
        InvokeOnceOnInit();
        return finished;
    }

    public void UpdateGame(float deltaTime)
    {
        if (!_isRunning || !IsFinite(deltaTime) || deltaTime <= 0f) return;
        CurrentAngle = Mathf.Repeat(CurrentAngle + _rotationSpeed * deltaTime, 360f);
        _canConfirm = true;
    }

    public void Cancel()
    {
        _isRunning = false;
        _canConfirm = false;
    }

    private void ResetProgress()
    {
        _gaugeSteps = 0;
        FailureCount = 0;
        IsWarning = false;
        IsFailed = false;
    }

    private void SelectNextZone(bool excludePrevious)
    {
        float previousCenter = SuccessZoneStartAngle + SuccessZoneAngle * 0.5f;
        float pointerClearance = (SuccessZoneAngle + PointerAngleWidth) * 0.5f + PointerSeparation;
        int count = 0;
        for (int center = 0; center < 360; center++)
        {
            if (excludePrevious && Mathf.Abs(Mathf.DeltaAngle(center, previousCenter)) <= SuccessZoneAngle + ZoneSeparation)
                continue;
            if (Mathf.Abs(Mathf.DeltaAngle(center, CurrentAngle)) <= pointerClearance)
                continue;
            _candidateCenters[count++] = center;
        }
        // Width limits above guarantee candidates; bounded enumeration avoids random retry loops.
        float selectedCenter = _candidateCenters[Random.Range(0, count)];
        SuccessZoneStartAngle = Mathf.Repeat(selectedCenter - SuccessZoneAngle * 0.5f, 360f);
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public void InvokeOnceOnInit()
    {
        OnPropertyChanged(nameof(CurrentAngle));
        OnPropertyChanged(nameof(SuccessZoneStartAngle));
        OnPropertyChanged(nameof(SuccessZoneAngle));
        OnPropertyChanged(nameof(PointerAngleWidth));
        OnPropertyChanged(nameof(FillProgress));
        OnPropertyChanged(nameof(FailureCount));
        OnPropertyChanged(nameof(IsWarning));
        OnPropertyChanged(nameof(IsFailed));
    }
}
