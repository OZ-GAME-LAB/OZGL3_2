using UnityEngine;

public class RuneSlotViewModel : ViewModelBase
{
    // 장착 슬롯은 0~2, 인벤토리 목록은 -1.
    public int SlotIndex { get; }

    private string _runeId;
    public string RuneId => _runeId;

    private bool _isSelected;
    public bool IsSelected => _isSelected;

    public RuneSlotViewModel(int slotIndex)
    {
        SlotIndex = slotIndex;
    }

    public void SetRuneId(string runeId)
    {
        runeId ??= ""; // runeId가 null이면 빈 문자열로 초기화

        if (_runeId == runeId)
        {
            return;
        }

        _runeId = runeId;
        OnPropertyChanged(nameof(RuneId));
    }

    public void SetSelected(bool selected)
    {
        if (_isSelected == selected)
        {
            return;
        }

        _isSelected = selected;
        OnPropertyChanged(nameof(IsSelected));
    }

    public void InvokeOnceOnInit()
    {
        OnPropertyChanged(nameof(RuneId));
        OnPropertyChanged(nameof(IsSelected));
    }

}
