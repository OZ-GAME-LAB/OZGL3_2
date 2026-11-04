using UnityEngine;

public class InteractionFishingVM : InteractionVMBase
{
    public override string InteractionInfoText => "낚시 (F를 눌러 시작)";

    private float _rotationSpeed;
    private bool _isRunning;
    private float _currentAngle;

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

    public void Initialize(float rotationSpeed)
    {
        if (float.IsNaN(rotationSpeed) || float.IsInfinity(rotationSpeed) || rotationSpeed <= 0f)
        {
            throw new System.ArgumentException(
                "회전 속도는 0보다 큰 유효한 숫자여야 합니다.");
        }

        _rotationSpeed = rotationSpeed;
        _isRunning = false;
        CurrentAngle = 0f;
    }

    public void BeginGame()
    {
        CurrentAngle = 0f;
        _isRunning = true;
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
    }
}
