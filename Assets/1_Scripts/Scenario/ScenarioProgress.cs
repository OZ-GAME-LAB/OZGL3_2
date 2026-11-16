using System.Collections.Generic;

// Unity 객체와 분리된 공유 진행 규칙입니다. 변경 요청은 서버에서만 호출합니다.
public sealed class ScenarioProgress
{
    private struct Session
    {
        public ulong RequestId;
        public ScenarioData Data;
    }

    private readonly HashSet<string> _completed = new HashSet<string>();
    private readonly Dictionary<ulong, Session> _sessions = new Dictionary<ulong, Session>();

    public bool IsCompleted(string id)
    {
        return !string.IsNullOrEmpty(id) && _completed.Contains(id);
    }

    public bool CanStart(ScenarioData data)
    {
        return data != null && !string.IsNullOrWhiteSpace(data.Id) &&
            (!data.CompleteWorldOnFinish || !IsCompleted(data.Id)) &&
            (string.IsNullOrEmpty(data.RequiredCompletedScenarioId) ||
                IsCompleted(data.RequiredCompletedScenarioId)) &&
            !IsCompleted(data.BlockedByCompletedScenarioId);
    }

    public bool TryBegin(ulong clientId, ulong requestId, ScenarioData data)
    {
        return data != null && data.IsDialogue && TryBeginSession(clientId, requestId, data);
    }

    public bool TryBeginPuzzle(ulong clientId, ulong requestId, ScenarioData data)
    {
        return data != null && data.IsSlidingPuzzle && data.CompleteWorldOnFinish &&
            TryBeginSession(clientId, requestId, data);
    }

    private bool TryBeginSession(ulong clientId, ulong requestId, ScenarioData data)
    {
        if (requestId == 0 || !CanStart(data) || _sessions.ContainsKey(clientId))
            return false;

        _sessions.Add(clientId, new Session { RequestId = requestId, Data = data });
        return true;
    }

    public bool TryComplete(ulong clientId, ulong requestId, out string completedWorldId)
    {
        return TryCompleteSession(clientId, requestId, false, out completedWorldId);
    }

    // 검증된 퍼즐 성공에서만 호출합니다. 대화 완료 RPC는 이 경로를 사용하지 않습니다.
    public bool TryCompletePuzzle(ulong clientId, ulong requestId, out string completedWorldId)
    {
        return TryCompleteSession(clientId, requestId, true, out completedWorldId);
    }

    private bool TryCompleteSession(ulong clientId, ulong requestId, bool puzzle, out string completedWorldId)
    {
        completedWorldId = null;
        if (!_sessions.TryGetValue(clientId, out Session session) || session.RequestId != requestId ||
            session.Data.IsSlidingPuzzle != puzzle)
            return false;

        _sessions.Remove(clientId);
        if (!CanStart(session.Data))
            return false;

        if (session.Data.CompleteWorldOnFinish)
        {
            _completed.Add(session.Data.Id);
            completedWorldId = session.Data.Id;
        }
        return true;
    }

    public void Cancel(ulong clientId, ulong requestId)
    {
        if (_sessions.TryGetValue(clientId, out Session session) && session.RequestId == requestId && session.Data.IsDialogue)
            _sessions.Remove(clientId);
    }

    public void CancelPuzzle(ulong clientId, ulong requestId)
    {
        if (_sessions.TryGetValue(clientId, out Session session) && session.RequestId == requestId && session.Data.IsSlidingPuzzle)
            _sessions.Remove(clientId);
    }

    public void Disconnect(ulong clientId)
    {
        _sessions.Remove(clientId);
    }

    public void ApplyCompleted(string id)
    {
        if (!string.IsNullOrEmpty(id))
            _completed.Add(id);
    }

    public void Clear()
    {
        _sessions.Clear();
        _completed.Clear();
    }
}
