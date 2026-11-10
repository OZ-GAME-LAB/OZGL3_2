using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NetworkSelectUI : UIBase
{
    [Header("방 만들기")]
    [SerializeField] private TMP_InputField Input_HostPort;
    [SerializeField] private Button Button_CreateRoom;

    [Header("방 참가")]
    [SerializeField] private TMP_InputField Input_HostAddress;
    [SerializeField] private TMP_InputField Input_JoinPort;
    [SerializeField] private Button Button_JoinRoom;
    [SerializeField] private Button Button_CancelConnection;

    [Header("안내")]
    [SerializeField] private TMP_Text Text_Message;

    private NetCodeNetworkManager _boundManager;

    private void Awake()
    {
        Button_CreateRoom.onClick.AddListener(OnCreateRoom);
        Button_JoinRoom.onClick.AddListener(OnJoinRoom);
        Button_CancelConnection.onClick.AddListener(OnCancelConnection);

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
        TryBindManager();
        SetMessage("방을 만들거나 호스트의 IP와 포트를 입력하세요.");
        RefreshInteractable();
    }

    private void OnDisable()
    {
        UnbindManager();
    }

    private void Update()
    {
        // 접속 실패 후 네트워크 종료 처리가 끝나면 다시 입력 허용.
        TryBindManager();
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

        if (Button_CancelConnection != null)
        {
            Button_CancelConnection.onClick.RemoveListener(OnCancelConnection);
        }
    }

    private void TryBindManager()
    {
        NetCodeNetworkManager manager = NetCodeNetworkManager.Inst;

        if (manager == _boundManager)
        {
            return;
        }

        UnbindManager();

        _boundManager = manager;

        if (_boundManager != null)
        {
            _boundManager.ConnectionMessageChanged += SetMessage;
        }

    }

    private void UnbindManager()
    {
        if (ReferenceEquals(_boundManager, null) == false)
        {
            _boundManager.ConnectionMessageChanged -= SetMessage;
        }

        _boundManager = null;
    }

    private void OnCancelConnection()
    {
        if (_boundManager != null)
        {
            _boundManager.CancelConnection();
        }
    }

    private void RefreshInteractable()
    {
        bool canStart = (_boundManager != null) && (_boundManager.CanStartConnection);

        Button_CreateRoom.interactable = canStart;
        Button_JoinRoom.interactable = canStart;
        Button_CancelConnection.interactable = _boundManager != null && _boundManager.IsConnecting; //canCancel/isCancelable로 빼면 가독성이 괜찮을지 고민중

        Input_HostPort.interactable = canStart;
        Input_HostAddress.interactable = canStart;
        Input_JoinPort.interactable = canStart;
    }

    private void OnCreateRoom()
    {
        if (TryReadPort(Input_HostPort.text, out ushort port) == false)
        {
            SetMessage("호스트 포트는 1~65535 범위로 입력하세요.");
            return;
        }

        TryBindManager();
        var manager = _boundManager;

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

        TryBindManager();
        var manager = _boundManager;

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

    public void StartAsServer()
    {
        TryBindManager();

        if (_boundManager == null)
        {
            SetMessage("네트워크 매니저가 없습니다.");
            return;
        }

        _boundManager.StartAsServer();
    }
}
