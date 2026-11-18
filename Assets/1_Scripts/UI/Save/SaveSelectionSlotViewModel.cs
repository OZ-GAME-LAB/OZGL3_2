public class SaveSelectionSlotViewModel : ViewModelBase
{
    public string SaveId { get; }
    public string DisplayName { get; }
    public string SavedTimeText { get; }

    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;

            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected));
        }
    }

    public SaveSelectionSlotViewModel(
        string saveId,
        string displayName,
        string savedTimeText)
    {
        SaveId = saveId;
        DisplayName = displayName;
        SavedTimeText = savedTimeText;
    }

    public void InvokeOnceOnInit()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(SavedTimeText));
        OnPropertyChanged(nameof(IsSelected));
    }
}