using UnityEngine;
using System;
using Unity.Netcode;
using System.Net;
using System.Net.Sockets;
using Unity.Netcode.Transports.UTP;
using Cysharp.Threading.Tasks;

public class NetCodeNetworkManager : MonoBehaviour
{
    public static NetCodeNetworkManager Inst { get; private set; }

    [SerializeField] private NetworkManager _netCodeNetworkManager;
    [SerializeField] private UnityTransport _transport;
    [SerializeField, Min(1f)] private float _connectionTimeout = 8f;

    private double _connectionDeadLine;

    private bool _isConnectingStopped;
    private bool _isConnecting;
    public bool IsConnecting => _isConnecting;

    private bool _isStarting;
    public bool CanStartConnection => 
        _isStarting == false && 
        _isConnecting == false &&
        _isConnectingStopped == false &&
        _netCodeNetworkManager.IsListening == false && 
        _netCodeNetworkManager.ShutdownInProgress == false;

    public event Action<string> ConnectionMessageChanged;

    private NetCodeCliendtSideService _clientSideService = new NetCodeCliendtSideService();
    private NetCodeServerSideService _serverSideService = new NetCodeServerSideService();

    private void Awake()
    {
        Inst = this;

        if (_transport == null && _netCodeNetworkManager != null)
        {
            _transport = _netCodeNetworkManager.GetComponent<UnityTransport>();
        }
    }

    private void Start()
    {
        UIManager.Inst.OpenNetworkUI().Forget();
    }

    private void OnDestroy()
    {
        _clientSideService.EndClientService();
        _serverSideService.EndServerService();
        // _serverSideService.End
    }

    private void Update()
    {
        if (_netCodeNetworkManager == null)
        {
            return;
        }

        if (_isConnectingStopped == true)
        {
            if (_netCodeNetworkManager.IsListening == false && _netCodeNetworkManager.ShutdownInProgress == false)
            {
                _isConnectingStopped = false;
            }

            return;
        }

        if (_isConnecting == false)
        {
            return;
        }

        if (Time.realtimeSinceStartupAsDouble >= _connectionDeadLine)
        {
            StopConnectionAttempt("접속 시간이 초과되었습니다. 호스트의 IP와 포트를 확인하세요.");
        }
    }

    public void CancelConnection()
    {
        if (_isConnecting == false)
        {
            return;
        }

        StopConnectionAttempt("접속을 취소했습니다.");
    }

    private void StopConnectionAttempt(string message)
    {
        _isConnecting = false;
        _isConnectingStopped = true;

        ConnectionMessageChanged?.Invoke(message);

        if (_netCodeNetworkManager != null && _netCodeNetworkManager.ShutdownInProgress == false)
        {
            _netCodeNetworkManager.Shutdown();
        }
    }

    public bool AcceptLocalConnection()
    {
        if (_isConnectingStopped)
        {
            return false;
        }

        _isConnecting = false;
        return true;
    }

    public bool HandleConnectionAttemptDisconnected(string reason)
    {
        if (_isConnectingStopped)
        {
            return true;
        }

        if (_isConnecting == false)
        {
            return false;
        }

        string message = string.IsNullOrEmpty(reason) ? "접속하지 못했습니다. 호스트의 실행 상태, IP, 포트를 확인하세요." : $"접속하지 못했습니다.: {reason}";
        
        StopConnectionAttempt(message);
        return true;
    }

    public bool TryStartHost(ushort port, out string error)
    {
        error = null;

        if (CheckConnectionReady(out error) == false)
        {
            return false;
        }

        if (port == 0)
        {
            error = "포트는 1~65535 범위로 입력하세요.";
            return false;
        }

        _isStarting = true;

        try
        {
            // 호스트는 모든 로컬 네트워크 인터페이스에서 수신.
            _transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");

            // 재시도 시 같은 이벤트가 중복 등록되지 않도록 정리.
            _serverSideService.EndServerService();
            _clientSideService.EndClientService();

            _serverSideService.InitServerService();
            _clientSideService.InitClientService();

            bool started = _netCodeNetworkManager.StartHost();

            if (started == false)
            {
                _serverSideService.EndServerService();
                _clientSideService.EndClientService();

                error = "호스트를 시작하지 못했습니다. Console을 확인하세요.";
            }

            return started;
        }
        finally
        {
            _isStarting = false;
        }
    }

    public bool TryStartClient(string address, ushort port, out string error)
    {
        error = null;

        if (CheckConnectionReady(out error) == false)
        {
            return false;
        }

        address = address?.Trim();

        // 이번 구현은 직접 접속용 IPv4 주소만 허용.
        if (IPAddress.TryParse(address, out IPAddress ip) == false ||
            ip.AddressFamily != AddressFamily.InterNetwork ||
            ip.Equals(IPAddress.Any) ||
            ip.Equals(IPAddress.Broadcast))
        {
            error = "올바른 호스트 IPv4 주소를 입력하세요.";
            return false;
        }

        if (port == 0)
        {
            error = "포트는 1~65535 범위로 입력하세요.";
            return false;
        }

        _isStarting = true;

        try
        {
            _transport.SetConnectionData(ip.ToString(), port);

            _transport.ConnectTimeoutMS = 1000;
            _transport.MaxConnectAttempts = 5;

            _serverSideService.EndServerService();
            _clientSideService.EndClientService();
            _clientSideService.InitClientService();

            _isConnecting = true;
            _connectionDeadLine = Time.realtimeSinceStartupAsDouble + _connectionTimeout;

            if ( _netCodeNetworkManager.StartClient() == false)
            {
                error = "접속 시도를 시작하지 못했습니다.";
                StopConnectionAttempt(error);
                return false;
            }

            return true;
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);

            error = "네트워크 시작 중 오류가 발생했습니다.";
            StopConnectionAttempt(error);
            return false;
        }
        finally
        {
            _isStarting = false;
        }
    }

    private bool CheckConnectionReady(out string error)
    {
        error = null;

        if (Application.isPlaying == false)
        {
            error = "플레이 모드에서 실행하세요.";
            return false;
        }

        if (_netCodeNetworkManager == null || _transport == null)
        {
            error = "NetworkManager와 UnityTransport를 연결하세요.";
            return false;
        }

        if (_netCodeNetworkManager.NetworkConfig.NetworkTransport != _transport)
        {
            error = "NetworkManager에 설정된 Transport와 연결된 Transport가 다릅니다.";
            return false;
        }

        if (CanStartConnection == false)
        {
            error = "이미 네트워크가 실행 중이거나 종료 처리 중입니다.";
            return false;
        }

        return true;
    }

    public void StartAsHost()
    {
        if (_netCodeNetworkManager == null)
        {
            UIManager.Inst.OpenSimplePopup("네트워크 매니저가 존재하지 않습니다!").Forget();
            return;
        }

        _serverSideService.InitServerService();
        _clientSideService.InitClientService();
        _netCodeNetworkManager.StartHost();
    }

    public void StartAsServer()
    {
        if (_netCodeNetworkManager == null)
        {
            UIManager.Inst.OpenSimplePopup("네트워크 매니저가 존재하지 않습니다!").Forget();
            return;
        }

        // 여러곳에서 이렇게 Singleton으로 받아와도 된다 : 다만 역할을 이 컴포넌트로 한정하기 위해
        // 멤버변수로 접근할 것임 (NetworkManager.Singleton.StartServer();)
        _serverSideService.InitServerService();
        _netCodeNetworkManager.StartServer();
    }

    public void StartAsClient()
    {
        if (_netCodeNetworkManager == null)
        {
            UIManager.Inst.OpenSimplePopup("네트워크 매니저가 존재하지 않습니다!").Forget();
            return;
        }

        _clientSideService.InitClientService();
        _netCodeNetworkManager.StartClient();
    }
}
