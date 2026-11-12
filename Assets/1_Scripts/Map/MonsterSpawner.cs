using UnityEngine;

public class MonsterSpawner : MonoBehaviour
{
    [SerializeField] private PatrolArea _patrolArea;

    public void SpawnMonster(string monsterId)
    {
        var networkManager = Unity.Netcode.NetworkManager.Singleton;

        if (networkManager == null ||
            !networkManager.IsListening ||
            !networkManager.IsServer)
            return;

        if (string.IsNullOrWhiteSpace(monsterId))
        {
            Debug.LogWarning("생성할 몬스터 ID가 없습니다.", this);
            return;
        }

        if (ObjectManager.Inst == null)
        {
            Debug.LogWarning("ObjectManager가 준비되지 않았습니다.", this);
            return;
        }

        ObjectManager.Inst.SpawnMonster(
            monsterId,
            _patrolArea,
            transform.position);
    }
}
