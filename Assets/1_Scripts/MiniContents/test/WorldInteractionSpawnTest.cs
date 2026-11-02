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
}