using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public enum ScenarioReply
{
    Started,
    Completed,
    Rejected
}

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed partial class ScenarioManager : NetworkBehaviour
{
    public static ScenarioManager Inst { get; private set; }
    public static event Action AvailabilityChanged;
    public event Action ProgressChanged;
    public event Action<ulong, ScenarioReply> ReplyReceived;

    [Header("요청 대기")]
    [SerializeField, Min(1f)] private float _requestTimeout = 10f;
    public float RequestTimeout => _requestTimeout;
    public bool IsReady { get; private set; }

    private NetworkList<FixedString128Bytes> _completedIds;
    private readonly ScenarioProgress _progress = new ScenarioProgress();
    private readonly Dictionary<string, List<DialogueLineData>> _dialogues = new Dictionary<string, List<DialogueLineData>>();
    private Dictionary<string, ScenarioData> _scenarios;

    private void Awake()
    {
        _completedIds = new NetworkList<FixedString128Bytes>();
        _puzzles = new ScenarioPuzzleProgress(_progress, new System.Random());
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Inst = null;
        AvailabilityChanged = null;
    }

    public override void OnNetworkSpawn()
    {
        if (Inst != null && Inst != this)
        {
            Debug.LogError("ScenarioManager는 네트워크 세션에 하나만 생성해야 합니다.", this);
            return;
        }

        Inst = this;
        if (IsServer) _bossPhase.Value = BossEncounterPhase.Ready;
        BossEncounterController.Active?.ResetEncounterSession(this);
        RestTent.Active?.ResetRestSession(this);
        // 이 매니저는 한 세션에 한 번만 스폰합니다. 같은 세션의 재스폰은 지원하지 않습니다.
        // 호스트 재시작 시 재사용된 씬 객체에도 이전 완료 기록이 남지 않게 합니다.
        // 클라이언트에는 서버에서 받은 스냅샷이 있으므로 목록을 비우면 안 됩니다.
        if (IsServer)
            _completedIds.Clear();
        _puzzles.Clear();
        _progress.Clear();
        for (int i = 0; i < _completedIds.Count; i++)
            _progress.ApplyCompleted(_completedIds[i].ToString());
        _completedIds.OnListChanged += OnCompletedIdsChanged;
        if (IsServer)
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;

        IsReady = LoadData();
        AvailabilityChanged?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        BossEncounterController.Active?.ResetEncounterSession(null);
        RestTent.Active?.ResetRestSession(null);
        _completedIds.OnListChanged -= OnCompletedIdsChanged;
        if (NetworkManager != null)
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        IsReady = false;
        _puzzles.Clear();
        _progress.Clear();
        if (Inst == this)
        {
            Inst = null;
            AvailabilityChanged?.Invoke();
        }
        base.OnNetworkDespawn();
    }

    private bool LoadData()
    {
        if (DataManager.Inst == null)
            return false;

        _scenarios = DataManager.Inst.ReadData<ScenarioData>();
        _dialogues.Clear();
        bool needsDialogue = false;
        foreach (ScenarioData scenario in _scenarios.Values)
            needsDialogue |= scenario.IsDialogue;
        var dialogueRows = needsDialogue ? DataManager.Inst.ReadData<DialogueLineData>() :
            new Dictionary<string, DialogueLineData>();
        foreach (DialogueLineData line in dialogueRows.Values)
        {
            if (string.IsNullOrWhiteSpace(line.DialogueId))
                return ReportDataError($"대화 ID가 비었습니다: {line.Id}");
            if (!_dialogues.TryGetValue(line.DialogueId, out List<DialogueLineData> lines))
            {
                lines = new List<DialogueLineData>();
                _dialogues.Add(line.DialogueId, lines);
            }
            lines.Add(line);
        }
        foreach (List<DialogueLineData> lines in _dialogues.Values)
        {
            lines.Sort(CompareLineOrder);
            for (int i = 1; i < lines.Count; i++)
            {
                if (lines[i - 1].Order == lines[i].Order)
                    return ReportDataError($"대화 순서가 중복되었습니다: {lines[i].DialogueId}/{lines[i].Order}");
            }
        }
        foreach (ScenarioData data in _scenarios.Values)
        {
            if (string.IsNullOrWhiteSpace(data.Id) || Encoding.UTF8.GetByteCount(data.Id) > 120 ||
                (!data.IsDialogue && !data.IsSlidingPuzzle && !data.IsWorldEvent) ||
                (data.IsDialogue && (string.IsNullOrWhiteSpace(data.DialogueId) || !_dialogues.ContainsKey(data.DialogueId))) ||
                ((data.IsSlidingPuzzle || data.IsWorldEvent) && (!data.CompleteWorldOnFinish || !string.IsNullOrEmpty(data.DialogueId))) ||
                !IsValidWorldReference(data.RequiredCompletedScenarioId) ||
                !IsValidWorldReference(data.BlockedByCompletedScenarioId))
                return ReportDataError($"시나리오 ID, 대화 또는 공유 사건 참조를 확인해 주세요: {data.Id}");
        }
        return _scenarios.Count > 0;
    }

    private bool IsValidWorldReference(string id)
    {
        return string.IsNullOrEmpty(id) ||
            (_scenarios.TryGetValue(id, out ScenarioData data) && data.CompleteWorldOnFinish);
    }

    private static int CompareLineOrder(DialogueLineData left, DialogueLineData right)
    {
        return left.Order.CompareTo(right.Order);
    }

    private bool ReportDataError(string message)
    {
        Debug.LogError(message, this);
        return false;
    }

    public bool TryGetScenario(string id, out ScenarioData data)
    {
        data = null;
        return IsReady && !string.IsNullOrEmpty(id) && _scenarios.TryGetValue(id, out data);
    }

    public IReadOnlyList<DialogueLineData> GetDialogue(string dialogueId)
    {
        return _dialogues.TryGetValue(dialogueId, out List<DialogueLineData> lines) ? lines : null;
    }

    public bool IsCompleted(string id) => _progress.IsCompleted(id);
    public bool CanStart(ScenarioData data) => IsReady && _progress.CanStart(data);

    public void RequestStart(string sourceId, ulong requestId)
    {
        if (IsSpawned && IsClient)
            RequestStartServerRpc(sourceId, requestId);
    }

    public void RequestComplete(ulong requestId)
    {
        if (IsSpawned && IsClient)
            RequestCompleteServerRpc(requestId);
    }

    public void RequestCancel(ulong requestId)
    {
        if (IsSpawned && IsClient)
            RequestCancelServerRpc(requestId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestStartServerRpc(string sourceId, ulong requestId, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        bool accepted = IsReady &&
            NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) &&
            ScenarioSource.TryGet(sourceId, out ScenarioSource source) &&
            source.ContainsPlayer(client.PlayerObject) &&
            TryGetScenario(source.ScenarioId, out ScenarioData data) &&
            _progress.TryBegin(clientId, requestId, data);
        SendReply(clientId, requestId, accepted ? ScenarioReply.Started : ScenarioReply.Rejected);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestCompleteServerRpc(ulong requestId, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        string completedWorldId = null;
        bool accepted = IsReady && _progress.TryComplete(clientId, requestId, out completedWorldId);
        // 완료 응답을 먼저 보내 호스트의 로컬 대화도 정상 완료로 정리합니다.
        SendReply(clientId, requestId, accepted ? ScenarioReply.Completed : ScenarioReply.Rejected);
        if (!accepted)
            return;

        // 완료 상태를 스냅샷으로 남겨 뒤늦게 접속한 클라이언트에도 전달합니다.
        if (completedWorldId != null)
            _completedIds.Add(new FixedString128Bytes(completedWorldId));
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestCancelServerRpc(ulong requestId, ServerRpcParams rpcParams = default)
    {
        _progress.Cancel(rpcParams.Receive.SenderClientId, requestId);
    }

    private void SendReply(ulong clientId, ulong requestId, ScenarioReply reply)
    {
        ClientRpcParams parameters = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        };
        ReceiveReplyClientRpc(requestId, reply, parameters);
    }

    [ClientRpc]
    private void ReceiveReplyClientRpc(ulong requestId, ScenarioReply reply, ClientRpcParams rpcParams = default)
    {
        ReplyReceived?.Invoke(requestId, reply);
    }

    private void OnCompletedIdsChanged(NetworkListEvent<FixedString128Bytes> change)
    {
        if (change.Type == NetworkListEvent<FixedString128Bytes>.EventType.Add)
            _progress.ApplyCompleted(change.Value.ToString());
        if (IsServer)
            _puzzles.PruneUnavailable();
        ProgressChanged?.Invoke();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        _puzzles.Disconnect(clientId);
        _progress.Disconnect(clientId);
    }
}
