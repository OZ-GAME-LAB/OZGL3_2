using UnityEngine;
using Unity.Netcode;

public class NetCodeNetworkManager : MonoBehaviour
{
    [SerializeField] private NetworkManager _netCodeNetworkManager;

    public static NetCodeNetworkManager Inst { get; private set; }

    private NetCodeCliendtSideService _clientSideService = new NetCodeCliendtSideService();
    private NetCodeServerSideService _serverSideService = new NetCodeServerSideService();

    private void Awake()
    {
        Inst = this;    
    }

    private void Start()
    {
        UIManager.Inst.OpenNetworkUI();
    }

    private void OnDestroy()
    {
        _clientSideService.EndClientService();
        // _serverSideService.End
    }

    public void StartAsHost()
    {
        if (_netCodeNetworkManager == null)
        {
            UIManager.Inst.OpenSimplePopup("네트워크 매니저가 존재하지 않습니다!");
            return;
        }

        _clientSideService.InitClientService();
        _netCodeNetworkManager.StartHost();
    }

    public void StartAsServer()
    {
        if (_netCodeNetworkManager == null)
        {
            UIManager.Inst.OpenSimplePopup("네트워크 매니저가 존재하지 않습니다!");
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
            UIManager.Inst.OpenSimplePopup("네트워크 매니저가 존재하지 않습니다!");
            return;
        }

        _clientSideService.InitClientService();
        _netCodeNetworkManager.StartClient();
    }
}
