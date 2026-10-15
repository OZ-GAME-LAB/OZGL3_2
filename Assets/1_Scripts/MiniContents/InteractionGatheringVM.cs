
public class InteractionGatheringVM : InteractionVMBase
{
    public void InvokeOnceOnInit()
    {
        OnPropertyChanged(nameof(MaxProgressSeconds));
        OnPropertyChanged(nameof(GatheringProgress));
    }

    private float _gatheringDurationSeconds;
    public float MaxProgressSeconds
    {
        get => _gatheringDurationSeconds;
        set
        {
            if (_gatheringDurationSeconds != value)
            {
                _gatheringDurationSeconds = value;
                OnPropertyChanged(nameof(MaxProgressSeconds));
            }
        }
    }

    private float _gatheringProgress;
    public float GatheringProgress
    {
        get => _gatheringProgress;
        set
        {
            if (_gatheringProgress != value)
            {
                _gatheringProgress = value;
                OnPropertyChanged(nameof(GatheringProgress));
            }
        }
    }

    private float _gatheringSeconds;
    private bool _isGathering;
    public override string InteractionInfoText => "채집 (F 길게 누르기)";

    public void InitGatheringVM(string id)
    {
        // 데이터 매니저로부터 설정
    }

    public void BeginGathering()
    {
        _gatheringSeconds = 0f;
        GatheringProgress = 0f;
        _isGathering = _gatheringDurationSeconds > 0f;
    }

    public bool UpdateGathering(float deltaTime)
    {
        if (!_isGathering || deltaTime <= 0f)
            return false;

        _gatheringSeconds += deltaTime;

        GatheringProgress = System.Math.Clamp(
            _gatheringSeconds / _gatheringDurationSeconds,
            0f,
            1f);

        if (GatheringProgress < 1f)
            return false;

        _isGathering = false;
        return true;
    }

    public void CancelGathering()
    {
        if (!_isGathering)
            return;

        _isGathering = false;
        _gatheringSeconds = 0f;
        GatheringProgress = 0f;
    }


}
