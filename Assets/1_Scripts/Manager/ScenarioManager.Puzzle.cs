using System;
using Unity.Collections;
using Unity.Netcode;

public enum ScenarioPuzzleReply
{
    Started,
    Moved,
    Completed,
    Rejected
}

// ScenarioManager의 퍼즐 전용 통신입니다. 보드/성공 규칙은 ScenarioPuzzleProgress가 담당합니다.
public sealed partial class ScenarioManager
{
    private ScenarioPuzzleProgress _puzzles;
    public event Action<string, ulong, uint, ScenarioPuzzleReply, int[]> PuzzleReplyReceived;

    public void RequestPuzzleStart(string sourceId, ulong requestId)
    {
        if (IsSpawned && IsClient)
            StartPuzzleServerRpc(sourceId, requestId);
    }

    public void RequestPuzzleMove(string sourceId, ulong requestId, uint sequence, int tileId)
    {
        if (IsSpawned && IsClient)
            MovePuzzleServerRpc(sourceId, requestId, sequence, tileId);
    }

    public void RequestPuzzleCancel(ulong requestId)
    {
        if (IsSpawned && IsClient)
            CancelPuzzleServerRpc(requestId);
    }

    private bool TryGetPuzzleSource(ulong clientId, string sourceId, out ScenarioData data)
    {
        data = null;
        return IsReady && NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) &&
            ScenarioSource.TryGet(sourceId, out ScenarioSource source) && source.ContainsPlayer(client.PlayerObject) &&
            TryGetScenario(source.ScenarioId, out data) && data.IsSlidingPuzzle && data.CompleteWorldOnFinish;
    }

    [ServerRpc(RequireOwnership = false)]
    private void StartPuzzleServerRpc(string sourceId, ulong requestId, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        int[] board = null;
        bool accepted = TryGetPuzzleSource(clientId, sourceId, out ScenarioData data) &&
            _puzzles.TryBegin(clientId, requestId, sourceId, data, out board);
        SendPuzzleReply(clientId, sourceId, requestId, 0,
            accepted ? ScenarioPuzzleReply.Started : ScenarioPuzzleReply.Rejected, board);
    }

    [ServerRpc(RequireOwnership = false)]
    private void MovePuzzleServerRpc(string sourceId, ulong requestId, uint sequence, int tileId,
        ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        int[] board = null;
        string completedId = null;
        bool accepted = TryGetPuzzleSource(clientId, sourceId, out _) &&
            _puzzles.TryMove(clientId, requestId, sourceId, sequence, tileId, out board, out completedId);
        if (!accepted)
            _puzzles.Cancel(clientId, requestId);
        ScenarioPuzzleReply reply = !accepted ? ScenarioPuzzleReply.Rejected :
            completedId == null ? ScenarioPuzzleReply.Moved : ScenarioPuzzleReply.Completed;
        SendPuzzleReply(clientId, sourceId, requestId, sequence, reply, board);
        // 뒤늦게 참가한 플레이어도 완료 상태를 받도록 목록에 남깁니다.
        if (accepted && completedId != null)
            _completedIds.Add(new FixedString128Bytes(completedId));
    }

    [ServerRpc(RequireOwnership = false)]
    private void CancelPuzzleServerRpc(ulong requestId, ServerRpcParams rpcParams = default)
    {
        _puzzles.Cancel(rpcParams.Receive.SenderClientId, requestId);
    }

    private void SendPuzzleReply(ulong clientId, string sourceId, ulong requestId, uint sequence,
        ScenarioPuzzleReply reply, int[] board)
    {
        ClientRpcParams parameters = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        };
        ReceivePuzzleReplyClientRpc(sourceId, requestId, sequence, reply, board ?? Array.Empty<int>(), parameters);
    }

    [ClientRpc]
    private void ReceivePuzzleReplyClientRpc(string sourceId, ulong requestId, uint sequence,
        ScenarioPuzzleReply reply, int[] board, ClientRpcParams rpcParams = default)
    {
        PuzzleReplyReceived?.Invoke(sourceId, requestId, sequence, reply, board);
    }
}
