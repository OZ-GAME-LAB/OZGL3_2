using UnityEngine;
using Unity.Netcode.Transports.UTP;

using NetcodeManager = Unity.Netcode.NetworkManager;
public class NetworkStartTester : MonoBehaviour
{
    [SerializeField] private NetcodeManager _netcodeManager;
    [SerializeField] private UnityTransport _transport;

    [Header("같은 PC에서 테스트")]
    [SerializeField] private string _address = "127.0.0.1";
    [SerializeField] private ushort _port = 7777;

    private bool CanStart()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("플레이 모드에서 실행하세요.");
            return false;
        }

        if (_netcodeManager == null || _transport == null)
        {
            Debug.LogError("NetworkManager와 UnityTransport를 연결하세요.");
            return false;
        }

        if (_netcodeManager.IsListening)
        {
            Debug.LogWarning("이미 네트워크가 실행 중입니다.");
            return false;
        }

        return true;
    }

    [ContextMenu("테스트/Host 시작")]
    public void StartHost()
    {
        if (!CanStart())
            return;

        _transport.SetConnectionData(_address, _port);

        bool started = _netcodeManager.StartHost();

        Debug.Log(started ? "Host 시작 성공" : "Host 시작 실패");
    }

    [ContextMenu("테스트/Server 시작")]
    public void StartServer()
    {
        if (!CanStart())
            return;

        _transport.SetConnectionData(_address, _port);

        bool started = _netcodeManager.StartServer();

        Debug.Log(started ? "Server 시작 성공" : "Server 시작 실패");
    }

    [ContextMenu("테스트/Client 시작")]
    public void StartClient()
    {
        if (!CanStart())
            return;

        _transport.SetConnectionData(_address, _port);

        bool started = _netcodeManager.StartClient();

        Debug.Log(
            started ? "Client 접속 시도 시작" : "Client 시작 실패");
    }

    [ContextMenu("테스트/네트워크 종료")]
    public void Shutdown()
    {
        if (!Application.isPlaying || _netcodeManager == null)
            return;

        _netcodeManager.Shutdown();
    }
}
