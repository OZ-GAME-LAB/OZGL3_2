using System;
using System.Collections.Generic;

// 서버 전용 퍼즐 규칙/진행 보관. 네트워크와 Unity 객체에 의존하지 않습니다.
public sealed class ScenarioPuzzleProgress
{
    private sealed class Session
    {
        public ulong RequestId;
        public string SourceId;
        public ScenarioData Data;
        public SlidingPuzzleModel Model;
        public uint Sequence;
    }

    private readonly ScenarioProgress _progress;
    private readonly Random _random;
    private readonly Dictionary<ulong, Session> _sessions = new Dictionary<ulong, Session>();
    private readonly Dictionary<ulong, Dictionary<string, Session>> _boards =
        new Dictionary<ulong, Dictionary<string, Session>>();

    public ScenarioPuzzleProgress(ScenarioProgress progress, Random random)
    {
        _progress = progress;
        _random = random;
    }

    public bool TryBegin(ulong clientId, ulong requestId, string sourceId, ScenarioData data, out int[] state)
    {
        state = null;
        if (string.IsNullOrWhiteSpace(sourceId) || _sessions.ContainsKey(clientId) ||
            !_progress.TryBeginPuzzle(clientId, requestId, data))
            return false;

        if (!_boards.TryGetValue(clientId, out Dictionary<string, Session> boards))
        {
            boards = new Dictionary<string, Session>();
            _boards.Add(clientId, boards);
        }
        if (!boards.TryGetValue(sourceId, out Session saved))
        {
            saved = new Session { Data = data, Model = new SlidingPuzzleModel() };
            saved.Model.Shuffle(_random);
            boards.Add(sourceId, saved);
        }
        SlidingPuzzleModel model = saved.Model;
        _sessions.Add(clientId, new Session
            { RequestId = requestId, SourceId = sourceId, Data = data, Model = model });
        state = model.GetStateCopy();
        return true;
    }

    public bool TryMove(ulong clientId, ulong requestId, string sourceId, uint sequence, int tileId,
        out int[] state, out string completedId)
    {
        state = null;
        completedId = null;
        if (!_sessions.TryGetValue(clientId, out Session session) || session.RequestId != requestId ||
            session.SourceId != sourceId || sequence == 0 || sequence != session.Sequence + 1 ||
            !_progress.CanStart(session.Data) || !session.Model.TryMove(tileId))
            return false;

        session.Sequence = sequence;
        state = session.Model.GetStateCopy();
        if (!session.Model.IsSolved)
            return true;
        bool accepted = _progress.TryCompletePuzzle(clientId, requestId, out completedId);
        _sessions.Remove(clientId);
        return accepted;
    }

    public void Cancel(ulong clientId, ulong requestId)
    {
        if (!_sessions.TryGetValue(clientId, out Session session) || session.RequestId != requestId)
            return;
        _sessions.Remove(clientId);
        _progress.CancelPuzzle(clientId, requestId);
    }

    public void Disconnect(ulong clientId)
    {
        if (_sessions.TryGetValue(clientId, out Session session))
            Cancel(clientId, session.RequestId);
        _boards.Remove(clientId);
    }

    public void PruneUnavailable()
    {
        var clients = new List<ulong>();
        foreach (var pair in _sessions)
            if (!_progress.CanStart(pair.Value.Data))
                clients.Add(pair.Key);
        foreach (ulong clientId in clients)
            Cancel(clientId, _sessions[clientId].RequestId);
        var sources = new List<string>();
        foreach (var boards in _boards.Values)
        {
            sources.Clear();
            foreach (var pair in boards)
                if (!_progress.CanStart(pair.Value.Data))
                    sources.Add(pair.Key);
            foreach (string source in sources)
                boards.Remove(source);
        }
    }

    public void Clear()
    {
        foreach (var pair in _sessions)
            _progress.CancelPuzzle(pair.Key, pair.Value.RequestId);
        _sessions.Clear();
        _boards.Clear();
    }
}
