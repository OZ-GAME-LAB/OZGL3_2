using UnityEngine;

public sealed partial class BossEncounterController
{
    [Header("보스 배치와 시나리오 이벤트")]
    [SerializeField] private GameObject _placedBossPreview;
    [SerializeField] private Transform _bossSpawnPoint;
    [SerializeField] private string _bossId = "monster_test_boss";
    [SerializeField] private string _startedEventId = "BossEncounterStarted";
    [SerializeField] private string _defeatedEventId = "BossEncounterDefeated";
    [Header("연출 카메라")]
    [SerializeField] private Vector3 _cinematicCameraOffset = new(0f, 7f, -10f);
    [SerializeField, Min(1f)] private float _cinematicCameraSize = 12f;
    [SerializeField, Min(1f)] private float _introLeadSeconds = 1.5f;
    private BossNetwork _encounterBoss;
    private bool _introSent;
    private double _introEndsAt;

    partial void InitializeBossPlacement()
    {
        // 편집 화면의 직접 배치는 유지하고, 실행 시에는 서버에서 네트워크 보스 한 개만 생성합니다.
        if (_placedBossPreview != null) _placedBossPreview.SetActive(false);
    }

    partial void PrepareBossOnServer()
    {
        _introSent = false;
        if (_bossSpawnPoint == null || ObjectManager.Inst == null ||
            !_manager.TryGetScenario(_startedEventId, out ScenarioData started) || !started.IsWorldEvent ||
            !_manager.TryGetScenario(_defeatedEventId, out ScenarioData defeated) || !defeated.IsWorldEvent)
        {
            Debug.LogError("보스전 배치 또는 WorldEvent 데이터 연결을 확인하세요.", this);
            return;
        }
        Boss instance = ObjectManager.Inst.SpawnBoss(_bossId, _bossSpawnPoint.position, false);
        if (instance == null || !instance.TryGetComponent(out _encounterBoss)) return;
        _encounterBoss.DiedOnServer += OnEncounterBossDied;
        _manager.SetEncounterBossOnServer(_encounterBoss.NetworkObjectId);
    }

    partial void BeginIntroductionOnServer()
    {
        TryBeginIntroduction();
    }

    private void TryBeginIntroduction()
    {
        if (_introSent || _encounterBoss == null || !_encounterBoss.IsReady) return;
        BossIntroVisual visual = _encounterBoss.GetComponent<BossIntroVisual>();
        if (visual == null) { _failed = true; return; }
        double start = Network.ServerTime.Time + _introLeadSeconds;
        if (!_encounterBoss.BeginIntroductionOnServer(start)) { _failed = true; return; }
        _introSent = true;
        _introEndsAt = start + visual.Duration;
        foreach (ulong id in _participants)
            _manager.SendBossStepOnServer(id, _run, BossEncounterPhase.Intro, Vector3.zero);
    }

    partial void BeginCombatOnServer()
    {
        if (_encounterBoss == null || !_encounterBoss.StartCombatOnServer()) { RecoverOnServer(); return; }
        if (!_manager.CompleteWorldEventOnServer(_startedEventId))
        {
            Debug.LogError("보스 전투 시작 이벤트 기록 실패: " + _startedEventId, this);
            RecoverOnServer();
            return;
        }
        _manager.SetBossPhaseOnServer(BossEncounterPhase.Fighting);
        UnlockParticipants();
    }

    partial void UpdateBattleOnServer()
    {
        if (Phase == BossEncounterPhase.Ready || Phase == BossEncounterPhase.Prompt ||
            Phase == BossEncounterPhase.Completed || Phase == BossEncounterPhase.Recovering) return;
        if (_encounterBoss == null || !_encounterBoss.IsSpawned)
        {
            RecoverOnServer();
            return;
        }
        if (Phase == BossEncounterPhase.Intro)
        {
            TryBeginIntroduction();
        }
        if (Phase == BossEncounterPhase.Fighting)
        {
            foreach (ulong id in _participants) if (TryGetAlivePlayer(id, out _)) return;
            RecoverOnServer();
        }
    }

    private void OnEncounterBossDied()
    {
        if (_manager == null || !_manager.IsServer || Phase != BossEncounterPhase.Fighting) return;
        if (!_manager.CompleteWorldEventOnServer(_defeatedEventId))
        {
            Debug.LogError("보스 처치 이벤트 기록 실패: " + _defeatedEventId, this);
            return;
        }
        _manager.SetBossPhaseOnServer(BossEncounterPhase.Completed);
        UnlockParticipants();
        _participants.Clear();
        _acknowledged.Clear();
        if (_encounterBoss != null) _encounterBoss.DiedOnServer -= OnEncounterBossDied;
        _encounterBoss = null;
    }

    partial void StopEncounterBoss()
    {
        if (_encounterBoss != null)
        {
            _encounterBoss.DiedOnServer -= OnEncounterBossDied;
            if (_encounterBoss.IsServer && _encounterBoss.IsSpawned) _encounterBoss.NetworkObject.Despawn();
        }
        _encounterBoss = null;
        _introSent = false;
    }
}
