using UnityEngine;

public class InteractionFishingVM : InteractionVMBase
{
    public override string InteractionInfoText => "낚시 (F를 눌러 시작)";

    private float _rotationSpeed;
    private bool _isRunning;
    private float _currentAngle;

    private float _fillPerSuccess;

    public float SuccessZoneStartAngle { get; private set; }
    public float SuccessZoneAngle { get; private set; }

    public float FillProgress { get; private set; }
    public int FailureCount { get; private set; }

    public bool IsSucceeded => FillProgress >= 1f;
    public bool IsFailed => FailureCount >= 3;

    public float CurrentAngle
    {
        get => _currentAngle;
        private set
        {
            if (_currentAngle == value)
                return;

            _currentAngle = value;
            OnPropertyChanged(nameof(CurrentAngle));
        }
    }

    public void Initialize(float rotationSpeed, float successZoneAngle = 54f, float fillPerSuccess = 0.25f)
    {
        if (float.IsNaN(rotationSpeed) || float.IsInfinity(rotationSpeed) || rotationSpeed <= 0f)
        {
            rotationSpeed = 180f;
            Debug.LogError("회전 속도는 0보다 큰 유효한 숫자여야 합니다.(임시 설정 180)");

        }

        if (float.IsNaN(successZoneAngle) || float.IsInfinity(successZoneAngle) || successZoneAngle <= 0f || successZoneAngle > 360f)
        {
            successZoneAngle = 54f;
            Debug.LogError("성공 구간은 0도 초과, 360도 이하여야 합니다.(임시 설정 54)");

        }

        if (float.IsNaN(fillPerSuccess) || float.IsInfinity(fillPerSuccess) || fillPerSuccess <= 0f || fillPerSuccess > 1f)
        {
            fillPerSuccess = 0.25f;
            Debug.LogError("성공당 게이지 증가량은 0 초과, 1 이하여야 합니다. 임시 설정 0.25f");
        }

        _rotationSpeed = rotationSpeed;
        SuccessZoneAngle = successZoneAngle;
        _fillPerSuccess = fillPerSuccess;

        _isRunning = false;
        CurrentAngle = 0f;
    }

    public void BeginGame(float successZoneStartAngle = 0f)
    {
        SuccessZoneStartAngle = ((successZoneStartAngle % 360f) + 360f) % 360f;

        CurrentAngle = 0f;
        FillProgress = 0f;
        FailureCount = 0;

        _isRunning = true;

        InvokeOnceOnInit();
    }

    public bool Confirm()
    {
        if (!_isRunning)
            return false;

        float angleFromStart = (CurrentAngle - SuccessZoneStartAngle + 360f) % 360f;

        bool isInSuccessZone = angleFromStart <= SuccessZoneAngle;

        if (isInSuccessZone)
        {
            FillProgress = System.Math.Min(1f, FillProgress + _fillPerSuccess);
        }
        else
        {
            FailureCount++;
        }

        bool isFinished = IsSucceeded || IsFailed;

        if (isFinished)
            _isRunning = false;

        OnPropertyChanged(nameof(FillProgress));
        OnPropertyChanged(nameof(FailureCount));

        return isFinished;
    }

    public void UpdateGame(float deltaTime)
    {
        if (!_isRunning || deltaTime <= 0f)
            return;

        CurrentAngle = (CurrentAngle + _rotationSpeed * deltaTime) % 360f;
    }

    public void Cancel()
    {
        _isRunning = false;
    }

    public void InvokeOnceOnInit()
    {
        OnPropertyChanged(nameof(CurrentAngle));
        OnPropertyChanged(nameof(SuccessZoneStartAngle));
        OnPropertyChanged(nameof(SuccessZoneAngle));
        OnPropertyChanged(nameof(FillProgress));
        OnPropertyChanged(nameof(FailureCount));
    }
}
