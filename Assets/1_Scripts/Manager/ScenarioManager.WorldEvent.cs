using Unity.Collections;

public sealed partial class ScenarioManager
{
    // 게임 세계의 검증된 서버 코드에서만 완료합니다. 클라이언트 완료 RPC와 분리합니다.
    public bool CompleteWorldEventOnServer(string id)
    {
        if (!IsServer || !IsSpawned || !TryGetScenario(id, out ScenarioData data) ||
            !data.IsWorldEvent || !data.CompleteWorldOnFinish) return false;
        if (IsCompleted(id)) return true;
        if (!CanStart(data)) return false;
        _completedIds.Add(new FixedString128Bytes(id));
        return true;
    }
}
