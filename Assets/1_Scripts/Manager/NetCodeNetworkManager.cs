using UnityEngine;
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

    private bool _starting;
    public bool CanStartConnection => _starting == false && _netCodeNetworkManager != null && _netCodeNetworkManager.IsListening == false && _netCodeNetworkManager.ShutdownInProgress == false;

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

        _starting = true;

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
            _starting = false;
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

        _starting = true;

        try
        {
            _transport.SetConnectionData(ip.ToString(), port);

            _serverSideService.EndServerService();
            _clientSideService.EndClientService();

            _clientSideService.InitClientService();

            bool started = _netCodeNetworkManager.StartClient();

            if (!started)
            {
                _clientSideService.EndClientService();

                error = "접속 시도를 시작하지 못했습니다. Console을 확인하세요.";
            }

            // true는 접속 완료가 아니라 접속 시도 시작을 의미.
            return started;
        }
        finally
        {
            _starting = false;
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
