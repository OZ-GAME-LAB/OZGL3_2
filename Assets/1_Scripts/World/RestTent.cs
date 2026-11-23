using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

// 한 텐트의 휴식 참가와 진행 단계는 서버가 소유합니다. 통신은 ScenarioManager에 맡깁니다.
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed partial class RestTent : InteractableObject
{
    private sealed class RestViewModel : InteractionVMBase
    {
        public override string InteractionInfoText => "휴식하기 (F)";
    }

    public static RestTent Active { get; private set; }
    public static bool BlocksBossEntry => Active != null && (Active._localActive || Active._requests.Count > 0);
    [Header("휴식 영역")]
    [SerializeField] private BoxCollider2D _area;
    [Header("휴식 연출과 응답 제한")]
    [SerializeField, Min(0f)] private float _blackHoldSeconds = 0.6f;
    [SerializeField, Min(1f)] private float _stepTimeout = 20f;
    private readonly RestViewModel _viewModel = new();
    private readonly Dictionary<ulong, uint> _requests = new();
    private readonly Dictionary<ulong, uint> _lastRequests = new();
    private readonly Dictionary<ulong, double> _uiDeadlines = new();
    private readonly HashSet<ulong> _uiReady = new();
    private readonly HashSet<ulong> _acknowledged = new();
    private readonly List<InteractionNetwork> _restored = new();
    private readonly List<MonsterSpawner.RestRecovery> _monsterRecoveries = new();
    private readonly List<ulong> _removeIds = new();
    private ScenarioManager _manager;
    private RestStep _phase;
    private uint _run;
    private int _lastTotal;
    private double _deadline;
    private double _releaseAfter;
    private bool _effectsCommitted;
    private NetworkManager Network => NetworkManager.Singleton;
    public override InteractionVMBase ViewModel => _viewModel;
    public override bool RequiresServerReservation => false;
    public override bool KeepInteractionOnRelease => true;

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
        ResetRestSession(ScenarioManager.Inst);
    }

    public override UniTask InitObject(string id) => UniTask.CompletedTask;

    public void ResetRestSession(ScenarioManager manager)
    {
        if (_manager != null && _manager.IsServer) EndOnServer("휴식 세션이 종료되었습니다.");
        EndLocal(false);
        _requests.Clear();
        _lastRequests.Clear();
        _uiReady.Clear();
        _uiDeadlines.Clear();
        _acknowledged.Clear();
        _restored.Clear();
        _monsterRecoveries.Clear();
        _run = 0;
        _nextRequest = 0;
        _phase = RestStep.Waiting;
        _manager = manager;
    }

    protected override void OnDisable()
    {
        if (Active == this)
        {
            ResetRestSession(null);
            Active = null;
        }
        base.OnDisable();
    }

    private void Update()
    {
        if (_manager != ScenarioManager.Inst) ResetRestSession(ScenarioManager.Inst);
        _viewModel.IsAvailable = _manager != null && _manager.IsReady && BossAllowsRest();
        UpdateLocalLifetime();
        if (_manager == null || !_manager.IsServer || !_manager.IsSpawned || _requests.Count == 0) return;
        if (!BossAllowsRest()) { EndOnServer("다른 연출이 시작되어 휴식을 취소했습니다."); return; }
        if (_phase == RestStep.Waiting)
        {
            _removeIds.Clear();
            foreach (var pair in _requests)
                if (!TryGetParticipant(pair.Key, out PlayerNetworkStats player) || !Contains(player) ||
                    (!_uiReady.Contains(pair.Key) && Time.realtimeSinceStartupAsDouble >= _uiDeadlines[pair.Key]))
                    _removeIds.Add(pair.Key);
            foreach (ulong id in _removeIds) RemoveParticipant(id, "휴식 준비가 취소되었습니다.");
            int total = Network.ConnectedClientsList.Count;
            if (total != _lastTotal) BroadcastWaiting();
            if (total > 0 && _requests.Count == total && _uiReady.Count == total)
                BeginStep(RestStep.Fading);
            return;
        }
        if (!AllParticipantsValid() || Time.realtimeSinceStartupAsDouble >= _deadline)
        {
            EndOnServer(_effectsCommitted ? "휴식 효과 적용 후 응답을 확인하지 못했습니다." : "휴식 응답이 지연되어 취소했습니다.");
            return;
        }
        if (_phase == RestStep.Restoring)
        {
            bool pending = false;
            foreach (var recovery in _monsterRecoveries)
            {
                if (!recovery.Finished) pending = true;
                else if (!recovery.Succeeded)
                {
                    EndOnServer("일부 몬스터를 복원하지 못했습니다. 다음 휴식에서 다시 시도할 수 있습니다.");
                    return;
                }
            }
            if (pending) return;
        }
        if (_acknowledged.Count != _requests.Count) return;
        if (_phase == RestStep.Fading) ApplyRestEffects();
        else if (_phase == RestStep.Restoring && Time.realtimeSinceStartupAsDouble >= _releaseAfter && ResourcesReady())
            BeginStep(RestStep.Releasing);
        else if (_phase == RestStep.Releasing) EndOnServer(null);
    }

    private bool BossAllowsRest() => _manager != null &&
        (_manager.BossPhase == BossEncounterPhase.Ready || _manager.BossPhase == BossEncounterPhase.Completed);

    private bool TryGetPlayer(ulong id, out PlayerNetworkStats player)
    {
        player = null;
        return Network != null && Network.IsListening && Network.ConnectedClients.TryGetValue(id, out NetworkClient client) &&
            client.PlayerObject != null && client.PlayerObject.TryGetComponent(out player);
    }

    private bool TryGetParticipant(ulong id, out PlayerNetworkStats player) =>
        TryGetPlayer(id, out player) && player.IsAlive && !player.IsEncounterLocked;

    private bool Contains(PlayerNetworkStats player)
    {
        if (_area == null || !_area.isActiveAndEnabled || !player.TryGetComponent(out Collider2D body) ||
            !body.isActiveAndEnabled || body.isTrigger) return false;
        ColliderDistance2D distance = _area.Distance(body);
        return distance.isValid && distance.isOverlapped;
    }

    public void RequestOnServer(ulong sender, uint request, bool enter)
    {
        if (_manager == null || !_manager.IsServer || !_manager.IsSpawned || request == 0) return;
        _lastRequests.TryGetValue(sender, out uint last);
        if (!enter)
        {
            if (_requests.TryGetValue(sender, out uint active) && active == request)
            {
                if (_phase == RestStep.Waiting) RemoveParticipant(sender, null);
                else EndOnServer("참가자가 휴식을 중단했습니다.");
            }
            if (request > last) _lastRequests[sender] = request;
            return;
        }
        if (request <= last) return;
        _lastRequests[sender] = request;
        if (_phase != RestStep.Waiting || !BossAllowsRest() || !TryGetParticipant(sender, out PlayerNetworkStats player) || !Contains(player))
        {
            Send(sender, request, RestStep.Ended, "지금은 텐트에서 휴식할 수 없습니다.");
            return;
        }
        if (_requests.Count == 0) { ++_run; _effectsCommitted = false; _restored.Clear(); }
        _requests[sender] = request;
        _uiReady.Remove(sender);
        _uiDeadlines[sender] = Time.realtimeSinceStartupAsDouble + _stepTimeout;
        player.SetRestingOnServer(true);
        BroadcastWaiting();
    }

    public void AcknowledgeOnServer(ulong sender, uint request, uint run, RestStep phase, bool success)
    {
        if (_manager == null || !_manager.IsServer || run != _run || phase != _phase ||
            !_requests.TryGetValue(sender, out uint active) || request != active) return;
        if (!success)
        {
            if (_phase == RestStep.Waiting) RemoveParticipant(sender, "휴식 UI를 준비하지 못했습니다.");
            else EndOnServer("휴식 화면을 준비하지 못했습니다.");
            return;
        }
        if (phase == RestStep.Waiting)
        {
            if (_uiReady.Contains(sender)) return;
            if (Time.realtimeSinceStartupAsDouble >= _uiDeadlines[sender])
            {
                RemoveParticipant(sender, "휴식 UI 준비 시간이 초과되었습니다.");
                return;
            }
            _uiReady.Add(sender);
        }
        else _acknowledged.Add(sender);
    }

    private bool AllParticipantsValid()
    {
        if (Network == null || Network.ConnectedClientsList.Count != _requests.Count) return false;
        foreach (ulong id in _requests.Keys)
            if (!TryGetParticipant(id, out PlayerNetworkStats player) || !Contains(player)) return false;
        return _requests.Count > 0;
    }

    private void BroadcastWaiting()
    {
        _lastTotal = Network != null ? Network.ConnectedClientsList.Count : 0;
        Broadcast(RestStep.Waiting);
    }

    private void BeginStep(RestStep phase)
    {
        if (!AllParticipantsValid()) { EndOnServer("휴식 참가자가 변경되었습니다."); return; }
        _phase = phase;
        _acknowledged.Clear();
        _deadline = Time.realtimeSinceStartupAsDouble + _stepTimeout;
        Broadcast(phase);
    }

    private void Broadcast(RestStep phase)
    {
        var recipients = new List<ulong>(_requests.Keys);
        uint run = _run;
        foreach (ulong id in recipients)
        {
            if (run != _run || phase != _phase) break;
            if (_requests.TryGetValue(id, out uint request)) Send(id, request, phase, null);
        }
    }

    private void Send(ulong id, uint request, RestStep phase, string message)
    {
        ulong[] resources = Array.Empty<ulong>();
        if (phase == RestStep.Restoring)
        {
            resources = new ulong[_restored.Count];
            for (int i = 0; i < resources.Length; i++) resources[i] = _restored[i].NetworkObjectId;
        }
        try
        {
            _manager.SendRestStepOnServer(id, request, _run, phase, _requests.Count,
                Network != null ? Network.ConnectedClientsList.Count : 0, resources, message ?? "");
        }
        catch (Exception exception) { Debug.LogException(exception, this); }
    }

    private void RemoveParticipant(ulong id, string message)
    {
        if (!_requests.TryGetValue(id, out uint request)) return;
        _requests.Remove(id);
        _uiReady.Remove(id);
        _uiDeadlines.Remove(id);
        if (TryGetPlayer(id, out PlayerNetworkStats player)) player.SetRestingOnServer(false);
        Send(id, request, RestStep.Ended, message);
        BroadcastWaiting();
    }

    private void ApplyRestEffects()
    {
        if (_effectsCommitted) return;
        _effectsCommitted = true; // 아래 작업은 되돌릴 수 없으므로 예외 뒤에도 같은 run에서 다시 실행하지 않습니다.
        try
        {
            foreach (ulong id in _requests.Keys)
            {
                if (!TryGetParticipant(id, out PlayerNetworkStats player)) throw new InvalidOperationException("휴식 참가자가 사망하거나 연결이 끊겼습니다.");
                player.ClearStatusEffectsOnServer();
                player.HealOnServer(player.MaxHp);
                if (player.CurrentHp != player.MaxHp || player.IsPoisoned || player.IsBossPoisoned || player.IsBleeding)
                    throw new InvalidOperationException("휴식 회복 결과를 확인하지 못했습니다.");
            }
            if (!InteractableObjectSpawner.RespawnDepletedResourcesOnServer(_restored))
                throw new InvalidOperationException("일부 자원을 복원하지 못했습니다.");
            GameManager.Inst.RestoreDefeatedMonstersOnServer(_monsterRecoveries, _stepTimeout);
            _releaseAfter = Time.realtimeSinceStartupAsDouble + _blackHoldSeconds;
            BeginStep(RestStep.Restoring);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            EndOnServer("휴식 효과 일부가 적용된 뒤 오류가 발생했습니다. 자동으로 다시 실행하지 않습니다.");
        }
    }

    private bool ResourcesReady()
    {
        foreach (InteractionNetwork resource in _restored)
            if (resource == null || !resource.IsReady || resource.IsDormant) return false;
        return true;
    }

    private void EndOnServer(string message)
    {
        _phase = RestStep.Ended;
        var participants = new List<KeyValuePair<ulong, uint>>(_requests);
        _requests.Clear();
        _uiReady.Clear();
        _uiDeadlines.Clear();
        _acknowledged.Clear();
        foreach (var participant in participants)
        {
            if (TryGetPlayer(participant.Key, out PlayerNetworkStats player)) player.SetRestingOnServer(false);
            Send(participant.Key, participant.Value, RestStep.Ended, message);
        }
        _restored.Clear();
        _monsterRecoveries.Clear();
        _phase = RestStep.Waiting;
    }
}
