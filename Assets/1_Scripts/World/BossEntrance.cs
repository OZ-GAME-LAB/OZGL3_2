using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

// 입구의 준비 명단은 서버가 소유합니다. 확정된 명단만 보스전 컨트롤러에 넘깁니다.
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed partial class BossEntrance : InteractableObject
{
    private sealed class EntryViewModel : InteractionVMBase
    {
        public override string InteractionInfoText => "진입하기 (F)";
    }

    public static BossEntrance Active { get; private set; }
    [Header("보스 입구")]
    [SerializeField] private BoxCollider2D _area;
    [SerializeField] private BossEncounterController _encounter;
    [Header("준비 확인")]
    [SerializeField, Min(1f)] private float _uiTimeout = 20f;
    [SerializeField, Min(0.05f)] private float _checkInterval = 0.1f;

    private readonly EntryViewModel _viewModel = new();
    private readonly Dictionary<ulong, uint> _requests = new();
    private readonly Dictionary<ulong, uint> _lastRequests = new();
    private readonly Dictionary<ulong, double> _uiDeadlines = new();
    private readonly HashSet<ulong> _uiReady = new();
    private readonly List<ulong> _removeIds = new();
    private ScenarioManager _manager;
    private double _nextCheck;
    private int _lastTotal;
    private NetworkManager Network => NetworkManager.Singleton;
    private bool CanPrepare => _manager != null && _manager.IsReady && _manager.IsSpawned &&
        _encounter != null && _encounter.isActiveAndEnabled &&
        (_manager.BossPhase == BossEncounterPhase.Ready || _manager.BossPhase == BossEncounterPhase.Waiting) &&
        !RestTent.BlocksBossEntry;

    public override InteractionVMBase ViewModel => _viewModel;
    public override bool RequiresServerReservation => false;
    public override bool KeepInteractionOnRelease => true;
    public override UniTask InitObject(string id) => UniTask.CompletedTask;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Active = null; }

    protected override void Awake()
    {
        base.Awake();
        if (_area == null) _area = GetComponent<BoxCollider2D>();
    }

    protected override void OnEnable()
    {
        if (Active != null && Active != this) { enabled = false; return; }
        Active = this;
        base.OnEnable();
        ResetEntrySession(ScenarioManager.Inst);
    }

    protected override void OnDisable()
    {
        if (Active == this)
        {
            ResetEntrySession(null);
            Active = null;
        }
        base.OnDisable();
    }

    public void ResetEntrySession(ScenarioManager manager)
    {
        if (_manager != null && _manager.IsServer) EndWaitingOnServer("입장 준비가 종료되었습니다.");
        EndLocal(false);
        _requests.Clear();
        _lastRequests.Clear();
        _uiReady.Clear();
        _uiDeadlines.Clear();
        _nextCheck = 0d;
        _manager = manager;
    }

    private void Update()
    {
        if (_manager != ScenarioManager.Inst) ResetEntrySession(ScenarioManager.Inst);
        _viewModel.IsAvailable = CanPrepare;
        UpdateLocalLifetime();
        if (_manager == null || !_manager.IsServer || !_manager.IsSpawned || _requests.Count == 0) return;
        if (!CanPrepare) { EndWaitingOnServer("지금은 보스룸에 진입할 수 없습니다."); return; }
        double now = Time.realtimeSinceStartupAsDouble;
        if (now < _nextCheck) return;
        _nextCheck = now + _checkInterval;
        _removeIds.Clear();
        foreach (var pair in _requests)
            if (!TryGetEligiblePlayer(pair.Key, out _) ||
                (!_uiReady.Contains(pair.Key) && now >= _uiDeadlines[pair.Key])) _removeIds.Add(pair.Key);
        foreach (ulong id in _removeIds) RemoveOnServer(id, "입장 준비가 취소되었습니다.");
        int total = Network.ConnectedClientsList.Count;
        if (total != _lastTotal) BroadcastWaiting();
        if (!AllPlayersReady()) return;

        var participants = new List<ulong>(_requests.Keys);
        participants.Sort();
        // 컨트롤러가 먼저 단계를 바꿉니다. 호스트의 동기 콜백에서 발생한 취소는 준비 취소로 취급하지 않습니다.
        if (!_encounter.TryBeginEntryOnServer(participants))
        {
            EndWaitingOnServer("보스룸 이동을 준비하지 못했습니다.");
            return;
        }
        ClearWaitingOnServer();
    }

    public bool Contains(PlayerNetworkStats player)
    {
        if (_area == null || !_area.isActiveAndEnabled || player == null ||
            !player.TryGetComponent(out Collider2D body) || !body.isActiveAndEnabled || body.isTrigger) return false;
        ColliderDistance2D distance = _area.Distance(body);
        return distance.isValid && distance.isOverlapped;
    }

    private bool TryGetPlayer(ulong id, out PlayerNetworkStats player)
    {
        player = null;
        return Network != null && Network.IsListening && Network.ConnectedClients.TryGetValue(id, out NetworkClient client) &&
            client.PlayerObject != null && client.PlayerObject.TryGetComponent(out player);
    }

    private bool TryGetEligiblePlayer(ulong id, out PlayerNetworkStats player) =>
        TryGetPlayer(id, out player) && player.IsAlive && !player.IsEncounterLocked && !player.IsResting && Contains(player);

    public void RequestOnServer(ulong sender, uint request, bool prepare)
    {
        if (_manager == null || !_manager.IsServer || !_manager.IsSpawned || request == 0) return;
        _lastRequests.TryGetValue(sender, out uint last);
        if (!prepare)
        {
            if (_manager.BossPhase == BossEncounterPhase.Waiting &&
                _requests.TryGetValue(sender, out uint active) && active == request) RemoveOnServer(sender, null);
            if (request > last) _lastRequests[sender] = request;
            return;
        }
        if (request <= last) return;
        _lastRequests[sender] = request;
        if (!CanPrepare || !TryGetEligiblePlayer(sender, out PlayerNetworkStats player))
        {
            Send(sender, request, true, "영역 안에서 보스룸 입장을 준비해 주세요.");
            return;
        }
        _requests[sender] = request;
        _uiReady.Remove(sender);
        _uiDeadlines[sender] = Time.realtimeSinceStartupAsDouble + _uiTimeout;
        player.SetBossEntryWaitingOnServer(true);
        _manager.SetBossPhaseOnServer(BossEncounterPhase.Waiting);
        BroadcastWaiting();
    }

    public void AcknowledgeOnServer(ulong sender, uint request, bool success)
    {
        if (_manager == null || !_manager.IsServer || _manager.BossPhase != BossEncounterPhase.Waiting ||
            !_requests.TryGetValue(sender, out uint active) || active != request || _uiReady.Contains(sender)) return;
        if (!success || Time.realtimeSinceStartupAsDouble >= _uiDeadlines[sender])
        { RemoveOnServer(sender, "입장 대기 화면을 준비하지 못했습니다."); return; }
        _uiReady.Add(sender);
    }

    private bool AllPlayersReady()
    {
        if (Network == null || _requests.Count == 0 || Network.ConnectedClientsList.Count != _requests.Count ||
            _uiReady.Count != _requests.Count) return false;
        foreach (ulong id in _requests.Keys) if (!TryGetEligiblePlayer(id, out _)) return false;
        return true;
    }

    private void BroadcastWaiting()
    {
        _lastTotal = Network != null ? Network.ConnectedClientsList.Count : 0;
        // 호스트의 즉시 UI 실패 응답이 준비 명단을 변경할 수 있어 명단을 복사합니다.
        var recipients = new List<ulong>(_requests.Keys);
        foreach (ulong id in recipients)
            if (_requests.TryGetValue(id, out uint request)) Send(id, request, false, null);
    }

    private void Send(ulong id, uint request, bool ended, string message)
    {
        _manager.SendBossPreparationOnServer(id, request, _requests.Count,
            Network != null ? Network.ConnectedClientsList.Count : 0, ended, message ?? "");
    }

    private void RemoveOnServer(ulong id, string message)
    {
        if (!_requests.TryGetValue(id, out uint request)) return;
        _requests.Remove(id);
        _uiReady.Remove(id);
        _uiDeadlines.Remove(id);
        if (TryGetPlayer(id, out PlayerNetworkStats player)) player.SetBossEntryWaitingOnServer(false);
        if (_requests.Count == 0 && _manager.BossPhase == BossEncounterPhase.Waiting)
            _manager.SetBossPhaseOnServer(BossEncounterPhase.Ready);
        Send(id, request, true, message);
        BroadcastWaiting();
    }

    private void ClearWaitingOnServer()
    {
        foreach (ulong id in _requests.Keys)
            if (TryGetPlayer(id, out PlayerNetworkStats player)) player.SetBossEntryWaitingOnServer(false);
        _requests.Clear();
        _uiReady.Clear();
        _uiDeadlines.Clear();
    }

    private void EndWaitingOnServer(string message)
    {
        var requests = new List<KeyValuePair<ulong, uint>>(_requests);
        ClearWaitingOnServer();
        if (_manager.BossPhase == BossEncounterPhase.Waiting) _manager.SetBossPhaseOnServer(BossEncounterPhase.Ready);
        foreach (var pair in requests) Send(pair.Key, pair.Value, true, message);
    }
}
