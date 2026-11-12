using UnityEngine;

public class WorldInteractionSpawnTest : MonoBehaviour
{
    [SerializeField] private string _testId;

    [SerializeField]
    private InteractableObjectSpawner[] _spawnPoints;

    [ContextMenu("테스트/상호작용 오브젝트 생성")]
    public void RequestSpawn()
    {
        if (!Application.isPlaying)
            return;

        var networkManager = Unity.Netcode.NetworkManager.Singleton;

        if (networkManager == null ||
            !networkManager.IsListening ||
            !networkManager.IsServer)
        {
            Debug.LogWarning("Host 또는 Server를 먼저 시작하세요.");
            return;
        }

        if (ObjectManager.Inst == null)
        {
            Debug.LogWarning("ObjectManager가 준비되지 않았습니다.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_testId))
        {
            Debug.LogWarning("테스트 ID를 입력하세요.");
            return;
        }

        if (_spawnPoints == null)
            return;

        foreach (InteractableObjectSpawner spawnPoint in _spawnPoints)
        {
            if (spawnPoint == null)
                continue;

            spawnPoint.SpawnInteractableObject(_testId);
        }
    }

    [Header("몬스터 스폰 테스트")]
    [SerializeField] private string _monsterTestId;
    [SerializeField] private MonsterSpawner[] _monsterSpawnPoints;

    [ContextMenu("테스트/몬스터 생성")]
    public void RequestMonsterSpawn()
    {
        if (!Application.isPlaying)
            return;

        var networkManager = Unity.Netcode.NetworkManager.Singleton;

        if (networkManager == null ||
            !networkManager.IsListening ||
            !networkManager.IsServer)
        {
            Debug.LogWarning("Host 또는 Server를 먼저 시작하세요.");
            return;
        }

        if (ObjectManager.Inst == null)
        {
            Debug.LogWarning("ObjectManager가 준비되지 않았습니다.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_monsterTestId))
        {
            Debug.LogWarning("몬스터 테스트 ID를 입력하세요.");
            return;
        }

        if (_monsterSpawnPoints == null)
            return;

        foreach (MonsterSpawner spawnPoint in _monsterSpawnPoints)
        {
            if (spawnPoint == null)
                continue;

            spawnPoint.SpawnMonster(_monsterTestId);
        }
    }

    [Header("보스 테스트")]
    [SerializeField] private string _bossTestId = "monster_test_boss";
    [SerializeField] private Transform _bossSpawnPoint;

    private Boss _spawnedBoss;

    private bool CanTestBoss()
    {
        var manager = Unity.Netcode.NetworkManager.Singleton;

        if (!Application.isPlaying ||
            manager == null ||
            !manager.IsListening ||
            !manager.IsServer)
        {
            Debug.LogWarning("Host 실행 중에 테스트해 주세요.", this);
            return false;
        }

        return true;
    }

    [ContextMenu("테스트/보스 생성")]
    public void RequestBossSpawn()
    {
        if (!CanTestBoss())
            return;

        if (_spawnedBoss != null)
        {
            Debug.LogWarning("이미 테스트 보스가 생성되어 있습니다.", _spawnedBoss);
            return;
        }

        if (ObjectManager.Inst == null || _bossSpawnPoint == null)
        {
            Debug.LogWarning("ObjectManager와 보스 생성 위치를 확인해 주세요.", this);
            return;
        }

        _spawnedBoss = ObjectManager.Inst.SpawnBoss(
            _bossTestId,
            _bossSpawnPoint.position);

        if (_spawnedBoss != null)
        {
            Debug.Log(
                "보스 생성 요청 완료. 스킬 준비 완료 로그를 기다려 주세요.",
                _spawnedBoss);
        }
    }

    [ContextMenu("테스트/보스 가시 공격")]
    public void RequestBossThornAttack()
    {
        if (!CanTestBoss())
            return;

        if (_spawnedBoss == null)
        {
            Debug.LogWarning("먼저 테스트 보스를 생성해 주세요.", this);
            return;
        }

        BossNetwork bossNetwork =
            _spawnedBoss.GetComponent<BossNetwork>();

        if (!bossNetwork.IsSpawned || !bossNetwork.IsReady)
        {
            Debug.LogWarning("보스의 스킬 준비가 아직 끝나지 않았습니다.", _spawnedBoss);
            return;
        }

        if (!_spawnedBoss.HasGroundPlayers())
        {
            Debug.LogWarning(
                "보스의 좌우 지면 감지 영역에 살아 있는 플레이어가 없습니다.",
                _spawnedBoss);

            return;
        }

        foreach (MonsterSkillBase skill in _spawnedBoss.Skills)
        {
            if (skill is not BossThornSkill thornSkill)
                continue;

            if (thornSkill.IsAttacking || !thornSkill.IsCooldownComplete)
            {
                Debug.Log("가시 공격 중이거나 쿨타임이 남아 있습니다.", thornSkill);
                return;
            }

            thornSkill.UseSkill(null, Vector3.zero);

            Debug.Log(
                thornSkill.IsAttacking
                    ? "가시 공격을 시작했습니다."
                    : "가시 공격이 시작되지 않았습니다. 앞선 오류 로그를 확인해 주세요.",
                thornSkill);

            return;
        }

        Debug.LogWarning("보스의 스킬 목록에 BossThornSkill이 없습니다.", _spawnedBoss);
    }

    [ContextMenu("테스트/보스 발판 침식")]
    public void RequestBossPlatformThornAttack()
    {
        if (!CanTestBoss())
            return;

        if (_spawnedBoss == null)
        {
            Debug.LogWarning("먼저 테스트 보스를 생성해 주세요.", this);
            return;
        }

        BossNetwork bossNetwork = _spawnedBoss.GetComponent<BossNetwork>();

        if (!bossNetwork.IsSpawned || !bossNetwork.IsReady)
        {
            Debug.LogWarning("보스의 스킬 준비가 아직 끝나지 않았습니다.", _spawnedBoss);
            return;
        }

        foreach (MonsterSkillBase skill in _spawnedBoss.Skills)
        {
            if (skill is not BossPlatformThornSkill platformSkill)
                continue;

            if (platformSkill.IsAttacking || !platformSkill.IsCooldownComplete)
            {
                Debug.Log("발판 침식이 진행 중이거나 쿨타임이 남아 있습니다.", platformSkill);
                return;
            }

            platformSkill.UseSkill(null, Vector3.zero);

            Debug.Log(
                platformSkill.IsAttacking
                    ? "발판 침식을 시작했습니다."
                    : "발판 침식이 시작되지 않았습니다. 사용 가능한 발판과 오류 로그를 확인해 주세요.",
                platformSkill);

            return;
        }

        Debug.LogWarning("보스의 스킬 목록에 BossPlatformThornSkill이 없습니다.", _spawnedBoss);
    }
}