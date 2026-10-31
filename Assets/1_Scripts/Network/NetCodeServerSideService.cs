using Unity.Netcode;
using UnityEngine;

public class NetCodeServerSideService
{
    public void InitServerService()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnServerClientConnected;
            NetworkManager.Singleton.OnServerStarted += OnServerStarted;
            NetworkManager.Singleton.OnServerStopped += OnServerStopped;
        }
    }

    public void EndServerService()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnServerClientConnected;
            NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
            NetworkManager.Singleton.OnServerStopped -= OnServerStopped;
        }
    }

    //나중에는 UI에 로그를 띄우는 식으로 구현할 것.
    private void OnServerStarted()
    {
        Debug.Log("서버 시작!");

        GameManager.Inst.StartGame();
    }

    private void OnServerStopped(bool isHost)
    {
        if (isHost)
        {
            Debug.Log("호스트 서버가 중지되었습니다.");
        }
        else
        {
            Debug.Log("전용(Dedicated) 서버가 중지되었습니다.");
        }
    }

    private void OnServerClientConnected(ulong clientId)
    {
        NetworkManager network = NetworkManager.Singleton;

        if (network == null || !network.IsServer)
            return;

        if (ObjectManager.Inst == null || WorldManager.Inst == null)
        {
            Debug.LogError("ObjectManager와 WorldManager를 확인해 주세요.");
            return;
        }

        if (!WorldManager.Inst.TryGetStartSpawnPoint(out var entryPoint))
            return;

        NetworkObject playerObject = ObjectManager.Inst.SpawnPlayer(clientId, entryPoint.transform.position);

        if (playerObject == null)
            return;

        if (playerObject.TryGetComponent(out PlayerControllerView player))
        {
            WorldManager.Inst.PlacePlayerAtStart(player);
        }
    }
}
