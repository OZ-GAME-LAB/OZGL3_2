
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

    // 자식 VM이 자기 상호작용에 맞는 안내 문구를 제공한다.
    public abstract string InteractionInfoText { get; }
}
