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
}