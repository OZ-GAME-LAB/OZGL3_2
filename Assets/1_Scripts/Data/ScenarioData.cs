using System;

[Serializable]
public class ScenarioData : GameDataBase
{
    // 기존 데이터의 빈 값은 Dialogue로 해석합니다.
    public string CompletionType;
    public string DialogueId;
    public string RequiredCompletedScenarioId;
    public string BlockedByCompletedScenarioId;
    public bool CompleteWorldOnFinish;
    public bool PlayOncePerPlayer;

    public bool IsDialogue => string.IsNullOrEmpty(CompletionType) || CompletionType == "Dialogue";
    public bool IsWorldEvent => CompletionType == "WorldEvent";
    public bool IsSlidingPuzzle => CompletionType == "SlidingPuzzle";
}
