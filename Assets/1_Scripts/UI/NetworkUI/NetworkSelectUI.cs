using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

public partial class NetworkSelectUI : UIBase
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

    [SerializeField] private Button Button_BackToLobby;

    private bool _returningToLobby;
    private bool _openingSaveSelection;

    private NetCodeNetworkManager _boundManager;

    private void Awake()
    {
        Button_CreateRoom.onClick.AddListener(OnCreateRoom);
        Button_JoinRoom.onClick.AddListener(OnJoinRoom);
        Button_CancelConnection.onClick.AddListener(OnCancelConnection);
        Button_BackToLobby.onClick.AddListener(OnBackToLobby);

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

        if (Button_BackToLobby != null)
        {
            Button_BackToLobby.onClick.RemoveListener(OnBackToLobby);
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
        bool selectionOpen = UIManager.Inst != null && UIManager.Inst.IsOpen(UIType.SaveSelectionUI);

        bool canStart =
            _boundManager != null &&
            _boundManager.CanStartConnection &&
            !_returningToLobby &&
            !_enteringField &&
            !_openingSaveSelection &&
            !selectionOpen;

        Button_CreateRoom.interactable = canStart;
        Button_JoinRoom.interactable = canStart;

        Input_HostPort.interactable = canStart;
        Input_HostAddress.interactable = canStart;
        Input_JoinPort.interactable = canStart;

        if (Button_CancelConnection != null)
        {
            Button_CancelConnection.interactable = _returningToLobby == false && _boundManager != null && _boundManager.IsConnecting;
        }

        if (Button_BackToLobby != null)
        {
            Button_BackToLobby.interactable = canStart;
        }
    }

    private void OnCreateRoom()
    {
        if (_openingSaveSelection || _returningToLobby)
        {
            return;
        }

        if (TryReadPort(Input_HostPort.text, out ushort port) == false)
        {
            SetMessage("호스트 포트는 1~65535 범위로 입력하세요.");
            return;
        }

        TryBindManager();

        if (_boundManager == null || _boundManager.CanStartConnection == false)
        {
            SetMessage("네트워크가 실행 중이거나 준비되지 않았습니다.");
            return;
        }

        OpenSaveSelectionAsync(true, null, port).Forget();
    }

    private void OnJoinRoom()
    {
        if (_openingSaveSelection || _returningToLobby)
        {
            return;
        }

        if (TryReadPort(Input_JoinPort.text, out ushort port) == false)
        {
            SetMessage("접속 포트는 1~65535 범위로 입력하세요.");
            return;
        }

        string address = Input_HostAddress.text?.Trim();

        if (!IPAddress.TryParse(address, out IPAddress ip) ||
        ip.AddressFamily != AddressFamily.InterNetwork ||
        ip.Equals(IPAddress.Any) ||
        ip.Equals(IPAddress.Broadcast))
        {
            SetMessage("올바른 호스트 IPv4 주소를 입력하세요.");
            return;
        }

        TryBindManager();

        if (_boundManager == null || !_boundManager.CanStartConnection)
        {
            SetMessage("네트워크가 실행 중이거나 준비되지 않았습니다.");
            return;
        }

        OpenSaveSelectionAsync(false, ip.ToString(), port).Forget();
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

    private void OnBackToLobby()
    {
        ReturnToLobbyAsync().Forget();
    }

    private async UniTask ReturnToLobbyAsync()
    {
        if (_returningToLobby || UIManager.Inst == null)
        {
            return;
        }

        TryBindManager();

        if (_boundManager == null || _boundManager.CanStartConnection == false)
        {
            SetMessage("네트워크가 실행 중이거나 종료 처리 중입니다.");
            return;
        }

        _returningToLobby = true;
        RefreshInteractable();

        UIManager uiManager = UIManager.Inst;

        try
        {
            LobbyUI lobby = await uiManager.OpenLobbyUIAsync();

            if (this == null || uiManager == null || lobby == null)
            {
                return;
            }

            uiManager.CloseNetworkUI();
        }
        finally
        {
            if (this != null)
            {
                _returningToLobby = false;
                RefreshInteractable();
            }
        }
    }

    private async UniTask OpenSaveSelectionAsync(bool isHost, string address, ushort port)
    {
        UIManager uiManager = UIManager.Inst;

        if (uiManager == null || _openingSaveSelection)
        {
            return;
        }

        _openingSaveSelection = true;
        RefreshInteractable();

        try
        {
            UIBase ui = await uiManager.OpenVeryFrontRootAsync(UIType.SaveSelectionUI);

            if (this == null || uiManager == null)
            {
                return;
            }

            if (isActiveAndEnabled == false)
            {
                uiManager.Close(UIType.SaveSelectionUI);
                return;
            }

            if (ui is not SaveSelectionUI selectionUI)
            {
                if (ui != null)
                {
                    uiManager.Close(UIType.SaveSelectionUI);
                }

                SetMessage("선택 UI 프리팹과 SaveSelectionUI 컴포넌트를 확인하세요.");
                return;
            }

            // 같은 VeryFront Root 내에서 네트워크 UI보다 앞에 표시.
            selectionUI.transform.SetAsLastSibling();

            if (isHost)
            {
                selectionUI.BeginHost(port, OnSaveSelectionConfirmed);
            }
            else
            {
                selectionUI.BeginClient(address, port, OnSaveSelectionConfirmed);
            }
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);

            if (this != null)
            {
                SetMessage("선택창을 열지 못했습니다. Console을 확인하세요.");
            }
        }
        finally
        {
            if (this != null)
            {
                _openingSaveSelection = false;
                RefreshInteractable();
            }
        }
    }

    private void OnSaveSelectionConfirmed()
    {
        if (this == null || !isActiveAndEnabled)
            return;

        if (!GameStartSelection.HasSelection || GameStartSelection.Character == null)
        {
            SetMessage("먼저 캐릭터를 선택해 주세요.");
            return;
        }

        TryBindManager();

        // 접속 성공 처리 중 NetworkSelectUI가 닫히면
        // OnDisable에서 _boundManager가 해제될 수 있으므로
        // 사용할 매니저를 지역 변수에 보관.
        NetCodeNetworkManager manager = _boundManager;

        if (manager == null || manager.CanStartConnection == false)
        {
            SetMessage("네트워크가 실행 중이거나 준비되지 않았습니다.");
            return;
        }

        if (!_enteringField)
            EnterFieldAsync(manager).Forget();
    }
}
