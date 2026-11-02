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

        if (_patrolArea == null)
        {
            Debug.LogWarning("몬스터의 순찰 구역을 연결하세요.", this);
            return;
        }

        ObjectManager.Inst.SpawnMonster(
            monsterId,
            _patrolArea,
            transform.position);
    }
}
