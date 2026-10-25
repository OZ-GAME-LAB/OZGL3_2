using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

using NetcodeManager = Unity.Netcode.NetworkManager;

public class ObjectManager : MonoBehaviour
{
    public static ObjectManager Inst { get; private set; }

    [Header("NetCode")]
    [SerializeField] private NetcodeManager _netcodeManager;

    [Header("플레이어 프리팹")]
    [SerializeField] private NetworkObject Prefab_Player;

    [Header("상호작용 오브젝트 프리팹")]
    [SerializeField] private InteractableObject Prefab_InteractionObject;

    [Header("몬스터 프리팹")]
    [SerializeField] private Monster Prefab_Monster;


    private void Awake()
    {
        if (Inst != null && Inst != this)
        {
            Destroy(gameObject);
            return;
        }

        Inst = this;
    }

    private void OnDestroy()
    {
        if (Inst == this)
        {
            Inst = null;
        }
    }

    private bool CanSpawn()
    {
        if (_netcodeManager == null)
        {
            Debug.LogError("Netcode NetworkManager가 연결되지 않았습니다.");
            return false;
        }

        if (_netcodeManager.IsListening == false || _netcodeManager.IsServer == false)
        {
            Debug.LogWarning("실행 중인 서버에서만 스폰할 수 있습니다.");
            return false;
        }

        return true;
    }

    public NetworkObject SpawnObject(NetworkObject prefab, Vector3 position, Quaternion rotation)
    {
        if (CanSpawn() == false)
        {
            return null;
        }

        if (prefab == null)
        {
            Debug.LogError("스폰할 프리팹이 null입니다.");
            return null;
        }

        NetworkObject spawnedObject = Instantiate(prefab, position, rotation);

        spawnedObject.Spawn();

        return spawnedObject;
    }

    public NetworkObject SpawnPlayer(ulong clientId, Vector3 position)
    {
        if (CanSpawn() == false)
        {
            return null;
        }

        if (Prefab_Player == null)
        {
            Debug.LogError("스폰할 플레이어 프리팹이 null입니다.");
            return null;
        }

        var client = _netcodeManager.ConnectedClients[clientId];

        if (client.PlayerObject != null)
        {
            return client.PlayerObject;
        }

        NetworkObject spawnedPlayer = Instantiate(Prefab_Player, position, Quaternion.identity);

        spawnedPlayer.SpawnAsPlayerObject(clientId);

        return spawnedPlayer;
    }

    public void DespawnObject(NetworkObject target)
    {
        if (CanSpawn() == false)
        {
            return;
        }

        if (target == null || target.IsSpawned == false)
        {
            return;
        }

        target.Despawn(destroy: true);
    }

    public void SpawnInteractionObject(
    string id,
    Vector3 position,
    Quaternion rotation)
    {
        if (!CanSpawn())
            return;

        if (Prefab_InteractionObject == null || string.IsNullOrWhiteSpace(id))
        {
            Debug.LogError("상호작용 본체 프리팹 또는 ID가 없습니다.");
            return;
        }

        if (!Prefab_InteractionObject.TryGetComponent<NetworkObject>(out _) || !Prefab_InteractionObject.TryGetComponent<InteractionNetwork>(out _))
        {
            Debug.LogError( "상호작용 본체에 NetworkObject와 InteractionNetwork가 필요합니다.");
            return;
        }

        InteractableObject instance = Instantiate(Prefab_InteractionObject, position, rotation);

        instance.GetComponent<InteractionNetwork>().SetObjectId(id);
        instance.GetComponent<NetworkObject>().Spawn();

        return;
    }

    public void SpawnMonster(
    string id,
    PatrolArea patrolObject,
    Vector3 spawnPosition)
    {
        if (!CanSpawn())
            return;

        if(Prefab_Monster == null)
        {
            Debug.LogError("ObjectManager의 몬스터 프리팹이 할당되지 않음");
            return;
        }


        if (patrolObject == null || string.IsNullOrWhiteSpace(id) || ResourceManager.Inst == null)
        {
            Debug.LogError("몬스터 스폰에 필요한 값이 준비되지 않았습니다.");
            return;
        }

        GameObject[] patrolSpots = patrolObject.PatrolSpotList;

        if (patrolSpots == null)
            return;

        if (!Prefab_Monster.TryGetComponent<NetworkObject>(out _) ||
        !Prefab_Monster.TryGetComponent<MonsterNetwork>(out _) ||
        !Prefab_Monster.TryGetComponent<Unity.Behavior.BehaviorGraphAgent>(out _))
        {
            Debug.LogError("몬스터 본체에 NetworkObject, MonsterNetwork, BehaviorGraphAgent가 필요합니다.");
            return;
        }

        Monster instance = Instantiate(Prefab_Monster, spawnPosition, Quaternion.identity);

        instance.InitPatrolArea(patrolSpots);

        MonsterNetwork monsterNetwork = instance.GetComponent<MonsterNetwork>();

        if (!monsterNetwork.SetMonsterId(id))
        {
            Debug.LogError($"몬스터 ID 설정 실패: {id}");
            Destroy(instance.gameObject);
            return;
        }

        instance.GetComponent<NetworkObject>().Spawn();
    }
}
