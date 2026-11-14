
public abstract class InteractionVMBase : ViewModelBase
{
    private bool _isAvailable;
    private bool _isUsedByOtherPlayer;
    private bool _hasRequiredTool = true;

    public bool IsAvailable
    {
        get => _isAvailable;
        set
        {
            if (_isAvailable == value)
                return;

            _isAvailable = value;
            OnPropertyChanged(nameof(IsAvailable));
        }
    }

    public bool IsUsedByOtherPlayer
    {
        get => _isUsedByOtherPlayer;
        set
        {
            if (_isUsedByOtherPlayer == value)
                return;

            _isUsedByOtherPlayer = value;
            OnPropertyChanged(nameof(IsUsedByOtherPlayer));
        }
    }

    public bool HasRequiredTool
    {
        get => _hasRequiredTool;
        set
        {
            if (_hasRequiredTool == value)
                return;

            _hasRequiredTool = value;
            OnPropertyChanged(nameof(HasRequiredTool));
        }
    }

    public abstract string InteractionInfoText { get; }
}
