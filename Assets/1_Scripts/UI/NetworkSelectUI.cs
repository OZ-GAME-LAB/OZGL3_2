using UnityEngine;
using UnityEngine.UI;

public class NetworkSelectUI : UIBase
{
    [SerializeField] private Button Button_StartAsHost;
    [SerializeField] private Button Button_StartAsServer;
    [SerializeField] private Button Button_StartAsClient;

    private void OnEnable()
    {
        Button_StartAsHost.onClick.AddListener(StartAsHost);
        Button_StartAsServer.onClick.AddListener(StartAsServer);
        Button_StartAsClient.onClick.AddListener(StartAsClient);
    }

    public void StartAsHost()
    {
        NetCodeNetworkManager.Inst.StartAsHost();
    }

    public void StartAsServer()
    {
        NetCodeNetworkManager.Inst.StartAsServer();
    }

    public void StartAsClient()
    {
        NetCodeNetworkManager.Inst.StartAsClient();
    }
}
