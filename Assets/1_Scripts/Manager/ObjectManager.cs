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
}
