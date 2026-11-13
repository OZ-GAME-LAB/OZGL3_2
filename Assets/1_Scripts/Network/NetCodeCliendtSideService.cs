using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

public class NetCodeCliendtSideService
{
    public ulong CurrentClientId { get; private set; }

    public void InitClientService()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
        }
    }

    public void EndClientService()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        NetworkManager network = NetworkManager.Singleton;

        if (network == null)
        {
            return;
        }

        if (clientId == network.LocalClientId)
        {
            if (NetCodeNetworkManager.Inst != null && NetCodeNetworkManager.Inst.AcceptLocalConnection() == false)
            {
                return;
            }

            CurrentClientId = clientId;
            Debug.Log("서버 접속 성공! 내 클라이언트 ID: " + clientId);
            UIManager.Inst.CloseNetworkUI();
            GameManager.Inst.StartGame();

        }
        else
        {
            UIManager.Inst.OpenSimplePopup($"다른 클라이언트가 접속했습니다! {clientId}").Forget();
        }
    }

    private void OnClientDisconnect(ulong clientId)
    {
        NetworkManager network = NetworkManager.Singleton;

        if (network == null)
        {
            return;
        }

        if (clientId == network.LocalClientId)
        {
            string reason = network.DisconnectReason;

            if (NetCodeNetworkManager.Inst != null && NetCodeNetworkManager.Inst.HandleConnectionAttemptDisconnected(reason))
            {
                return;
            }

            string message = string.IsNullOrWhiteSpace(reason) ? "서버와의 연결이 종료되었습니다." : $"서버와의 연결이 종료되었습니다: {reason}";

            if (UIManager.Inst != null)
            {
                UIManager.Inst.OpenSimplePopup(message).Forget();
            }
        }
        else
        {
            if (UIManager.Inst != null)
            {
                UIManager.Inst.OpenSimplePopup($"다른 클라이언트가 접속 해제했습니다! {clientId}").Forget();
            }
        }
    }
}
