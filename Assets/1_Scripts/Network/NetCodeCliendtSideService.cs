using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

public class NetCodeCliendtSideService
{
    public ulong CurrentClientId { get; private set; }
    private NetworkManager _network;

    public void InitClientService()
    {
        EndClientService();
        _network = NetworkManager.Singleton;
        if (_network != null)
        {
            _network.OnClientConnectedCallback += OnClientConnected;
            _network.OnClientDisconnectCallback += OnClientDisconnect;
            _network.OnPreShutdown += OnPreShutdown;
            _network.OnClientStopped += OnClientStopped;
        }
    }

    public void EndClientService()
    {
        if (_network != null)
        {
            _network.OnClientDisconnectCallback -= OnClientDisconnect;
            _network.OnClientConnectedCallback -= OnClientConnected;
            _network.OnPreShutdown -= OnPreShutdown;
            _network.OnClientStopped -= OnClientStopped;
        }
        _network = null;
        CurrentClientId = 0;
    }

    private void OnClientConnected(ulong clientId)
    {
        NetworkManager network = _network;

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
        NetworkManager network = _network;

        if (network == null)
        {
            return;
        }

        if (clientId == network.LocalClientId)
        {
            NetCodeNetworkManager.Inst?.HandleLocalClientDisconnected(network.DisconnectReason);
        }
        else
        {
            if (!network.ShutdownInProgress && UIManager.Inst != null)
            {
                UIManager.Inst.OpenSimplePopup($"다른 클라이언트가 접속 해제했습니다! {clientId}").Forget();
            }
        }
    }

    private void OnPreShutdown()
    {
        if (_network != null && !_network.IsServer)
            NetCodeNetworkManager.Inst?.HandleLocalClientDisconnected(_network.DisconnectReason);
    }

    private void OnClientStopped(bool wasHost)
    {
        if (!wasHost && _network != null)
            NetCodeNetworkManager.Inst?.HandleLocalClientDisconnected(_network.DisconnectReason);
    }
}
