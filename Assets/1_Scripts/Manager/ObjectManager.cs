using Cysharp.Threading.Tasks;
using NUnit.Framework.Internal.Execution;
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
    [SerializeField] private InteractableObject Prefab_LoggingMiningObject;
    // [SerializeField] private InteractableObject Prefab_FishingObject;

    [Header("몬스터 프리팹")]
    [SerializeField] private Monster Prefab_Monster;

    [Header("필드 드롭 아이템 프리팹")]
    [SerializeField] private FieldItemObject Prefab_FieldDropItem;

    [Header("보스 프리팹")]
    [SerializeField] private Boss Prefab_Boss;

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

    public InteractableObject SpawnInteractionObject(string id, Vector3 position, Quaternion rotation)
    {
        if (!CanSpawn())
            return null;

        if (string.IsNullOrWhiteSpace(id) || DataManager.Inst == null)
        {
            Debug.LogError("상호작용 ID 또는 DataManager를 확인해 주세요.");
            return null;
        }

        FieldObjectData data = DataManager.Inst.GetData<FieldObjectData>(id);

        if (data == null)
            return null;

        InteractableObject prefab;

        switch (data.InteractionType)
        {
            case "Gathering":
                prefab = Prefab_InteractionObject;
                break;

            case "Logging":
            case "Mining":
                prefab = Prefab_LoggingMiningObject;
                break;
                /*
            case "Fishing":
                prefab = Prefab_FishingObject;
                break;
                */
            default:
                Debug.LogError($"지원하지 않는 상호작용 종류입니다: {data.InteractionType}");
                return null;
        }

        if (prefab == null)
        {
            Debug.LogError($"상호작용 본체 프리팹이 연결되지 않았습니다: {data.InteractionType}");
            return null;
        }

        if (!prefab.TryGetComponent<NetworkObject>(out _) || !prefab.TryGetComponent<InteractionNetwork>(out _))
        {
            Debug.LogError($"상호작용 본체에 NetworkObject와 InteractionNetwork가 필요합니다: {prefab.name}");
            return null;
        }

        InteractableObject instance = Instantiate(prefab, position, rotation);

        instance.GetComponent<InteractionNetwork>().SetObjectId(id);
        instance.GetComponent<NetworkObject>().Spawn();

        return instance;
    }

    public Monster SpawnMonster(string id, PatrolArea patrolObject, Vector3 spawnPosition)
    {
        if (!CanSpawn())
            return null;

        if (Prefab_Monster == null)
        {
            Debug.LogError("ObjectManager의 몬스터 프리팹이 할당되지 않음");
            return null;
        }

        if (string.IsNullOrWhiteSpace(id) || DataManager.Inst == null || ResourceManager.Inst == null)
        {
            Debug.LogError("몬스터 스폰에 필요한 값이 준비되지 않았습니다.");
            return null;
        }

        MonsterData data = DataManager.Inst.GetData<MonsterData>(id);

        if (data == null || (data.MonsterType != MoveType.Fix && data.MonsterType != MoveType.Move && data.MonsterType != MoveType.Fly))
        {
            Debug.LogError($"몬스터 데이터와 이동 유형을 확인하세요: {id}");
            return null;
        }

        GameObject[] patrolSpots = null;

        if (data.MonsterType == MoveType.Move || data.MonsterType == MoveType.Fly)
        {
            if (patrolObject == null)
            {
                Debug.LogError($"이동형 몬스터의 순찰 구역을 연결하세요: {id}");
                return null;
            }

            patrolSpots = patrolObject.PatrolSpotList;

            if (patrolSpots == null)
                return null;
        }

        if (!Prefab_Monster.TryGetComponent<NetworkObject>(out _) ||
            !Prefab_Monster.TryGetComponent<MonsterNetwork>(out _) ||
            !Prefab_Monster.TryGetComponent<Unity.Behavior.BehaviorGraphAgent>(out _))
        {
            Debug.LogError("몬스터 본체에 NetworkObject, MonsterNetwork, BehaviorGraphAgent가 필요합니다.");
            return null;
        }

        Monster instance = Instantiate(Prefab_Monster, spawnPosition, Quaternion.identity);
        instance.InitPatrolArea(patrolSpots);

        MonsterNetwork monsterNetwork = instance.GetComponent<MonsterNetwork>();

        if (!monsterNetwork.SetMonsterId(id))
        {
            Debug.LogError($"몬스터 ID 설정 실패: {id}");
            Destroy(instance.gameObject);
            return null;
        }

        instance.GetComponent<NetworkObject>().Spawn();
        return instance;
    }


    public FieldItemObject SpawnItem(string itemDataId, int count, Vector3 position)
    {
        if (CanSpawn() == false)
        {
            return null;
        }

        if (Prefab_FieldDropItem == null)
        {
            Debug.LogError("필드 아이템 프리팹이 연결되지 않았습니다.");
            return null;
        }

        if (string.IsNullOrEmpty(itemDataId) || count <= 0)
        {
            Debug.LogWarning("아이템 ID 또는 수량이 올바르지 않습니다.");
            return null;
        }

        if (DataManager.Inst == null)
        {
            Debug.LogError("DataManager가 null입니다.");
            return null;
        }

        ItemData itemData = DataManager.Inst.GetData<ItemData>(itemDataId);

        if (itemData == null)
        {
            return null;
        }

        if (itemData.MaxStack <= 0 || count > itemData.MaxStack)
        {
            Debug.LogWarning(
                $"드롭 수량이 올바르지 않습니다. " + $"아이템: {itemDataId}, 수량: {count}, " + $"최대 스택: {itemData.MaxStack}");

            return null;
        }

        FieldItemObject item = Instantiate(Prefab_FieldDropItem, position, Quaternion.identity);

        item.Init(itemDataId, count);
        item.NetworkObject.Spawn(destroyWithScene: true);
        return item;
    }

    public Boss SpawnBoss(string id, Vector3 position)
    {
        if (!CanSpawn())
            return null;

        if (Prefab_Boss == null)
        {
            Debug.LogError("보스 프리팹이 연결되지 않았습니다.", this);
            return null;
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            Debug.LogError("보스 데이터 ID가 비어 있습니다.", this);
            return null;
        }

        if (!Prefab_Boss.TryGetComponent<NetworkObject>(out _) || !Prefab_Boss.TryGetComponent<BossNetwork>(out _))
        {
            Debug.LogError("보스 프리팹의 루트에 NetworkObject와 BossNetwork가 필요합니다.", this);

            return null;
        }

        Boss instance = Instantiate(Prefab_Boss, position, Quaternion.identity);

        NetworkObject networkObject = instance.GetComponent<NetworkObject>();

        BossNetwork bossNetwork = instance.GetComponent<BossNetwork>();

        networkObject.Spawn();

        if (!bossNetwork.InitializeStatsOnServer(id))
        {
            Debug.LogError($"보스 초기화에 실패했습니다: {id}", instance);

            networkObject.Despawn();
            return null;
        }

        return instance;
    }
}
