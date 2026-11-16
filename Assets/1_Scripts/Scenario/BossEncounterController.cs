using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// 맵 배치와 이번 보스전의 참가자/단계를 소유합니다. 통신과 완료 기록은 ScenarioManager가 담당합니다.
[DisallowMultipleComponent]
public sealed partial class BossEncounterController : MonoBehaviour
{
    public static BossEncounterController Active { get; private set; }
    [Header("입장 영역과 귀환/전투 지점")]
    [SerializeField] private BoxCollider2D _entryArea;
    [SerializeField] private StageEntryPoint _preparationPoint;
    [SerializeField] private StageEntryPoint _battlePoint;
    [SerializeField] private GameObject _entranceBarrier;
    [Header("참가자 배치와 응답 제한")]
    [SerializeField, Min(0.5f)] private float _playerSpacing = 1.8f;
    [SerializeField, Min(1f)] private float _stepTimeout = 20f;
    [SerializeField, Min(5f)] private float _choiceTimeout = 60f;
    [SerializeField, Min(0.1f)] private float _arrivalTolerance = 0.45f;

    private readonly List<ulong> _participants = new();
    private readonly HashSet<ulong> _acknowledged = new();
    private readonly Dictionary<ulong, Vector3> _destinations = new();
    private uint _run;
    private ulong _chooser;
    private double _deadline;
    private float _nextLocalPrompt;
    private bool _failed;
    private ScenarioManager _manager;
    private NetworkManager Network => NetworkManager.Singleton;
    private BossEncounterPhase Phase => _manager != null ? _manager.BossPhase : BossEncounterPhase.Ready;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Active = null; }

    private void Awake()
    {
        InitializeBossPlacement();
    }

    private void OnEnable()
    {
        if (Active != null && Active != this) { enabled = false; return; }
        Active = this;
        ResetEncounterSession(ScenarioManager.Inst, false);
    }

    public void ResetEncounterSession(ScenarioManager manager, bool newSession = true)
    {
        CleanupLocal();
        if (_manager != null && _manager.IsServer)
        {
            UnlockParticipants();
            StopEncounterBoss();
        }
        _participants.Clear();
        _acknowledged.Clear();
        _destinations.Clear();
        if (newSession)
        {
            _run = 0;
            _localRun = 0;
        }
        _failed = false;
        _manager = manager;
        if (_manager != null && _manager.IsServer)
            _manager.SetBossPhaseOnServer(!newSession && _manager.IsCompleted(_defeatedEventId)
                ? BossEncounterPhase.Completed : BossEncounterPhase.Ready);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!isActiveAndEnabled || _manager == null || !_manager.IsReady || Phase != BossEncounterPhase.Ready ||
            Time.unscaledTime < _nextLocalPrompt || other == null || other.isTrigger) return;
        PlayerNetworkStats player = other.GetComponentInParent<PlayerNetworkStats>();
        if (player == null || !player.IsOwner || !player.IsAlive || !Contains(player)) return;
        if (ServiceManager.Inst?.ScenarioService?.IsBusy == true ||
            (UIManager.Inst != null && UIManager.Inst.HasDialogueConflict())) return;
        _nextLocalPrompt = Time.unscaledTime + 2f;
        _manager.RequestBossPrompt();
    }

    private void Update()
    {
        _manager = ScenarioManager.Inst;
        if (_entranceBarrier != null) _entranceBarrier.SetActive(Phase != BossEncounterPhase.Completed);
        UpdateLocalLifetime();
        if (_manager == null || !_manager.IsSpawned || !_manager.IsServer) return;
        if (Phase == BossEncounterPhase.Ready && _manager.IsCompleted(_defeatedEventId))
            _manager.SetBossPhaseOnServer(BossEncounterPhase.Completed);
        UpdateBattleOnServer();
        if (Phase == BossEncounterPhase.Ready || Phase == BossEncounterPhase.Fighting || Phase == BossEncounterPhase.Completed) return;
        bool expired = Time.realtimeSinceStartupAsDouble > _deadline;
        if (Phase == BossEncounterPhase.Recovering)
        {
            if (RecoveredOnServer()) FinishRecovery();
            else if (expired)
            {
                // 응답이 없어도 잠금은 풀되 실제 귀환 전에는 재입장을 열지 않습니다.
                UnlockParticipants();
                _deadline = Time.realtimeSinceStartupAsDouble + _stepTimeout;
                foreach (ulong id in _participants)
                    if (TryGetAlivePlayer(id, out _))
                        _manager.SendBossStepOnServer(id, _run, BossEncounterPhase.Recovering, _destinations[id]);
            }
            return;
        }
        if (Phase == BossEncounterPhase.Prompt)
        {
            if (_failed || expired || !TryGetAlivePlayer(_chooser, out PlayerNetworkStats chooser) || !Contains(chooser)) RecoverOnServer();
            return;
        }
        if (_failed || expired || !AllParticipantsAlive()) { RecoverOnServer(); return; }
        if (_acknowledged.Count != _participants.Count) return;
        if (Phase == BossEncounterPhase.Fading)
        {
            BeginStep(BossEncounterPhase.Moving);
        }
        else if (Phase == BossEncounterPhase.Moving && ArrivedOnServer())
        {
            BeginStep(BossEncounterPhase.Framing);
        }
        else if (Phase == BossEncounterPhase.Framing)
        {
            _manager.SetBossPhaseOnServer(BossEncounterPhase.Intro);
            _acknowledged.Clear();
            _deadline = Time.realtimeSinceStartupAsDouble + _stepTimeout;
            BeginIntroductionOnServer();
        }
        else if (Phase == BossEncounterPhase.Intro && _introSent && Network.ServerTime.Time >= _introEndsAt)
        {
            BeginStep(BossEncounterPhase.Releasing);
        }
        else if (Phase == BossEncounterPhase.Releasing)
        {
            BeginCombatOnServer();
        }
    }

    private bool Contains(PlayerNetworkStats player)
    {
        return _entryArea != null && _entryArea.enabled && player != null &&
            _entryArea.OverlapPoint(player.transform.position);
    }

    private bool TryGetAlivePlayer(ulong id, out PlayerNetworkStats player)
    {
        return TryGetPlayer(id, out player) && player.IsAlive;
    }

    private bool TryGetPlayer(ulong id, out PlayerNetworkStats player)
    {
        player = null;
        return Network != null && Network.IsListening && Network.ConnectedClients.TryGetValue(id, out NetworkClient client) &&
            client.PlayerObject != null && client.PlayerObject.TryGetComponent(out player);
    }

    public void RequestPromptOnServer(ulong clientId)
    {
        _manager = ScenarioManager.Inst;
        if (_manager == null || !_manager.IsServer || Phase != BossEncounterPhase.Ready ||
            _manager.IsCompleted(_defeatedEventId) ||
            _preparationPoint == null || _battlePoint == null ||
            !TryGetAlivePlayer(clientId, out PlayerNetworkStats player) || !Contains(player)) return;
        ++_run;
        _chooser = clientId;
        _participants.Clear();
        _participants.Add(clientId);
        _acknowledged.Clear();
        _destinations.Clear();
        _failed = false;
        LockOnServer(player, true);
        _manager.SetBossPhaseOnServer(BossEncounterPhase.Prompt);
        _deadline = Time.realtimeSinceStartupAsDouble + _choiceTimeout;
        _manager.SendBossStepOnServer(clientId, _run, BossEncounterPhase.Prompt, Vector3.zero);
    }

    public void ChooseOnServer(ulong sender, uint run, bool enter)
    {
        if (_manager == null || !_manager.IsServer || run != _run || Phase != BossEncounterPhase.Prompt || sender != _chooser) return;
        if (!TryGetAlivePlayer(sender, out PlayerNetworkStats chooser) || !Contains(chooser)) { RecoverOnServer(); return; }
        if (!enter) { RecoverOnServer(); return; }
        _participants.Clear();
        foreach (NetworkClient client in Network.ConnectedClientsList)
        {
            if (TryGetAlivePlayer(client.ClientId, out PlayerNetworkStats player) && Contains(player)) _participants.Add(client.ClientId);
        }
        _participants.Sort();
        _destinations.Clear();
        for (int i = 0; i < _participants.Count; i++)
        {
            ulong id = _participants[i];
            _destinations.Add(id, _battlePoint.transform.position + Vector3.right * (_playerSpacing * i));
            if (TryGetAlivePlayer(id, out PlayerNetworkStats player)) LockOnServer(player, true);
        }
        PrepareBossOnServer();
        BeginStep(BossEncounterPhase.Fading);
    }

    private static void LockOnServer(PlayerNetworkStats player, bool locked)
    {
        if (locked && player.TryGetComponent(out PlayerInteractionController interaction)) interaction.CancelInteractionOnServer();
        player.SetEncounterLockedOnServer(locked);
    }

    private void BeginStep(BossEncounterPhase phase)
    {
        _acknowledged.Clear();
        _failed = false;
        _manager.SetBossPhaseOnServer(phase);
        _deadline = Time.realtimeSinceStartupAsDouble + _stepTimeout;
        foreach (ulong id in _participants)
            _manager.SendBossStepOnServer(id, _run, phase, _destinations.TryGetValue(id, out Vector3 point) ? point : Vector3.zero);
    }

    public void AcknowledgeOnServer(ulong sender, uint run, BossEncounterPhase phase, bool success)
    {
        if (_manager == null || !_manager.IsServer || run != _run || phase != Phase || !_participants.Contains(sender)) return;
        if (!success) _failed = true;
        else _acknowledged.Add(sender);
    }

    private bool AllParticipantsAlive()
    {
        foreach (ulong id in _participants) if (!TryGetAlivePlayer(id, out _)) return false;
        return _participants.Count > 0;
    }

    private bool ArrivedOnServer()
    {
        foreach (ulong id in _participants)
        {
            if (!TryGetAlivePlayer(id, out PlayerNetworkStats player) ||
                !AtDestination(player, _destinations[id])) return false;
        }
        return true;
    }

    private bool AtDestination(PlayerNetworkStats player, Vector3 destination)
    {
        return player.TryGetComponent(out PlayerControllerView controller) &&
            Vector2.Distance(controller.EncounterFootPosition, destination) <= _arrivalTolerance;
    }

    private bool RecoveredOnServer()
    {
        foreach (ulong id in _participants)
            if (TryGetAlivePlayer(id, out PlayerNetworkStats player) &&
                (!_acknowledged.Contains(id) || !AtDestination(player, _destinations[id]))) return false;
        return true;
    }

    private void RecoverOnServer()
    {
        if (_manager == null || !_manager.IsServer || Phase == BossEncounterPhase.Recovering) return;
        StopEncounterBoss();
        _acknowledged.Clear();
        _manager.SetBossPhaseOnServer(BossEncounterPhase.Recovering);
        _deadline = Time.realtimeSinceStartupAsDouble + _stepTimeout;
        for (int i = 0; i < _participants.Count; i++)
        {
            ulong id = _participants[i];
            Vector3 point = _preparationPoint.transform.position + Vector3.left * (i * _playerSpacing);
            _destinations[id] = point;
            if (!Network.ConnectedClients.ContainsKey(id)) { _acknowledged.Add(id); continue; }
            _manager.SendBossStepOnServer(id, _run, BossEncounterPhase.Recovering, point);
        }
    }

    private void FinishRecovery()
    {
        UnlockParticipants();
        _participants.Clear();
        _acknowledged.Clear();
        _destinations.Clear();
        _manager.SetBossPhaseOnServer(BossEncounterPhase.Ready);
        _nextLocalPrompt = Time.unscaledTime + 2f;
    }

    private void OnDisable()
    {
        if (Active != this) return;
        CleanupLocal();
        if (_manager != null && _manager.IsServer)
        {
            UnlockParticipants();
            StopEncounterBoss();
            _manager.SetBossPhaseOnServer(_manager.IsCompleted(_defeatedEventId)
                ? BossEncounterPhase.Completed : BossEncounterPhase.Ready);
        }
        if (Active == this) Active = null;
    }

    private void UnlockParticipants()
    {
        foreach (ulong id in _participants)
            if (TryGetPlayer(id, out PlayerNetworkStats player)) LockOnServer(player, false);
    }

    partial void InitializeBossPlacement();
    partial void PrepareBossOnServer();
    partial void BeginIntroductionOnServer();
    partial void BeginCombatOnServer();
    partial void UpdateBattleOnServer();
    partial void StopEncounterBoss();
}
