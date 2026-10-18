public class CraftingRequiredItemSlotViewModel : ViewModelBase
{
    public void InvokeOnceOnInit()
    {
        OnPropertyChanged(nameof(ItemDataId));
        OnPropertyChanged(nameof(RequiredCount));
        OnPropertyChanged(nameof(OwnedCount));
        OnPropertyChanged(nameof(IsEnough));
    }

    public string _itemDataId; 
    public string ItemDataId
    {
        get => _itemDataId;
        set
        {
            if (_itemDataId == value)
            {
                return;
            }

            _itemDataId = value;
            OnPropertyChanged(nameof(ItemDataId));
        }
    }

    public int _requiredCount;
    public int RequiredCount
    {
        get => _requiredCount;
        set
        {
            if (_requiredCount == value)
            {
                return;
            }

            _requiredCount = value;
            OnPropertyChanged(nameof(RequiredCount));
            OnPropertyChanged(nameof(IsEnough));
        }
    }

    private int _ownedCount;
    public int OwnedCount
    {
        get => _ownedCount;
        set
        {
            if (_ownedCount == value)
            {
                return;
            }

            _ownedCount = value;
            OnPropertyChanged(nameof(OwnedCount));
            OnPropertyChanged(nameof(IsEnough));
        }
    }

    public bool IsEnough => OwnedCount >= RequiredCount;
}