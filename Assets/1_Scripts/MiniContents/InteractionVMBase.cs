
public abstract class InteractionVMBase : ViewModelBase
{
    private bool _isAvailable;

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

    public abstract string InteractionInfoText { get; }
}
