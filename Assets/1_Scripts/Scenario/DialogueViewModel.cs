public sealed class DialogueViewModel : ViewModelBase
{
    public string SpeakerName { get; private set; } = string.Empty;
    public string Text { get; private set; } = string.Empty;
    public bool CanAdvance { get; private set; }
    public string NpcSpriteAddress { get; private set; } = string.Empty;

    // 현재 줄 번호는 서비스만 소유하고, ViewModel에는 표시 결과만 전달합니다.
    public void ShowLine(DialogueLineData line, bool canAdvance)
    {
        SpeakerName = line != null ? line.SpeakerName ?? string.Empty : string.Empty;
        Text = line != null ? line.Text ?? string.Empty : string.Empty;
        NpcSpriteAddress = line != null ? line.NpcSpriteAddress ?? string.Empty : string.Empty;
        CanAdvance = canAdvance;
        OnPropertyChanged(string.Empty);
    }

    public void SetCanAdvance(bool canAdvance)
    {
        if (CanAdvance == canAdvance)
            return;
        CanAdvance = canAdvance;
        OnPropertyChanged(nameof(CanAdvance));
    }
}
