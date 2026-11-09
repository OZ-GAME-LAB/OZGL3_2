using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NetworkSelectUI : UIBase
{
    // 정식 UI 제작 전 쓰이던 코드
    /*
    [SerializeField] private Button Button_StartAsHost;
    [SerializeField] private Button Button_StartAsServer;
    [SerializeField] private Button Button_StartAsClient;
    */

    [Header("방 만들기")]
    [SerializeField] private TMP_InputField Input_HostPort;
    [SerializeField] private Button Button_CreateRoom;

    [Header("방 참가")]
    [SerializeField] private TMP_InputField Input_HostAddress;
    [SerializeField] private TMP_InputField Input_JoinPort;
    [SerializeField] private Button Button_JoinRoom;

    [Header("안내")]
    [SerializeField] private TMP_Text Text_Message;

    private void Awake()
    {
        Button_CreateRoom.onClick.AddListener(OnCreateRoom);
        Button_JoinRoom.onClick.AddListener(OnJoinRoom);

        if (string.IsNullOrWhiteSpace(Input_HostPort.text))
        {
            Input_HostPort.text = "7777";
        }

        if (string.IsNullOrWhiteSpace(Input_JoinPort.text))
        {
            Input_JoinPort.text = "7777";
        }

        if (string.IsNullOrWhiteSpace(Input_HostAddress.text))
        {
            Input_HostAddress.text = "127.0.0.1";
        }
    }

    private void OnEnable()
    {
        /*
        Button_StartAsHost.onClick.AddListener(StartAsHost);
        Button_StartAsServer.onClick.AddListener(StartAsServer);
        Button_StartAsClient.onClick.AddListener(StartAsClient);
        */

        SetMessage("방을 만들거나 호스트의 IP와 포트를 입력하세요.");
        RefreshInteractable();
    }

    private void Update()
    {
        // 접속 실패 후 네트워크 종료 처리가 끝나면 다시 입력 허용.
        RefreshInteractable();
    }

    private void OnDestroy()
    {
        if (Button_CreateRoom != null)
        {
            Button_CreateRoom.onClick.RemoveListener(OnCreateRoom);
        }

        if (Button_JoinRoom != null)
        {
            Button_JoinRoom.onClick.RemoveListener(OnJoinRoom);
        }
    }

    private void RefreshInteractable()
    {
        bool canStart = (NetCodeNetworkManager.Inst != null) && (NetCodeNetworkManager.Inst.CanStartConnection);

        Button_CreateRoom.interactable = canStart;
        Button_JoinRoom.interactable = canStart;

        Input_HostPort.interactable = canStart;
        Input_HostAddress.interactable = canStart;
        Input_JoinPort.interactable = canStart;
    }

    private void OnCreateRoom()
    {
        if (!TryReadPort(Input_HostPort.text, out ushort port))
        {
            SetMessage("호스트 포트는 1~65535 범위로 입력하세요.");
            return;
        }

        var manager = NetCodeNetworkManager.Inst;

        if (manager == null)
        {
            SetMessage("네트워크 매니저가 없습니다.");
            return;
        }

        SetMessage("방을 생성하고 있습니다.");

        if (manager.TryStartHost(port, out string error) == false)
        {
            SetMessage(error);
        }

        RefreshInteractable();
    }

    private void OnJoinRoom()
    {
        if (TryReadPort(Input_JoinPort.text, out ushort port) == false)
        {
            SetMessage("접속 포트는 1~65535 범위로 입력하세요.");
            return;
        }

        var manager = NetCodeNetworkManager.Inst;

        if (manager == null)
        {
            SetMessage("네트워크 매니저가 없습니다.");
            return;
        }

        SetMessage("호스트에 접속하고 있습니다.");

        if (manager.TryStartClient(Input_HostAddress.text, port, out string error) == false)
        {
            SetMessage(error);
        }

        RefreshInteractable();
    }

    private static bool TryReadPort(string text, out ushort port)
    {
        return ushort.TryParse(text?.Trim(), out port) && port > 0;
    }

    private void SetMessage(string message)
    {
        if (Text_Message != null)
        {
            Text_Message.text = message;
        }
    }

    /*
    public void StartAsHost()
    {
        NetCodeNetworkManager.Inst.StartAsHost();
    }
    */

    public void StartAsServer()
    {
        NetCodeNetworkManager.Inst.StartAsServer();
    }

    /*
    public void StartAsClient()
    {
        NetCodeNetworkManager.Inst.StartAsClient();
    }
    */
}
