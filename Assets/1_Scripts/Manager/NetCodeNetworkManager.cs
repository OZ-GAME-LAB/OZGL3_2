using UnityEngine;
using System;
using Unity.Netcode;
using System.Net;
using System.Net.Sockets;
using Unity.Netcode.Transports.UTP;
using Cysharp.Threading.Tasks;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class NetCodeNetworkManager : MonoBehaviour
{
    public static NetCodeNetworkManager Inst { get; private set; }

    [SerializeField] private NetworkManager _netCodeNetworkManager;
    [SerializeField] private UnityTransport _transport;
    [SerializeField, Min(1f)] private float _connectionTimeout = 8f;
    [SerializeField] private HostDisconnectedUI _hostDisconnectedUIPrefab;

    private HostDisconnectedUI _hostDisconnectedUI;
    private string _lobbyScenePath;
    private bool _connectedAsClient;
    private bool _hostDisconnected;
    private bool _returningToLobby;
    private bool _isQuitting;
    private float _previousTimeScale;
    private bool _previousAudioPause;

    private double _connectionDeadLine;

    private bool _isConnectingStopped;
    private bool _isConnecting;
    public bool IsConnecting => _isConnecting;

    private bool _isStarting;
    public bool CanStartConnection =>
        _netCodeNetworkManager != null &&
        _isStarting == false && 
        _isConnecting == false &&
        _isConnectingStopped == false &&
        _hostDisconnected == false &&
        _returningToLobby == false &&
        _netCodeNetworkManager != null &&
        _netCodeNetworkManager.IsListening == false &&
        IsReturningToLobby == false &&
        _netCodeNetworkManager.ShutdownInProgress == false;

    public bool IsReturningToLobby { get; private set; }

    public event Action<string> ConnectionMessageChanged;

    private NetCodeCliendtSideService _clientSideService = new NetCodeCliendtSideService();
    private NetCodeServerSideService _serverSideService = new NetCodeServerSideService();
    private NetworkSessionSaveService _sessionSaveService;

    private void Awake()
    {
        Inst = this;
        _lobbyScenePath = SceneManager.GetActiveScene().path;
        Application.quitting += OnApplicationQuitting;

        if (_transport == null && _netCodeNetworkManager != null)
        {
            _transport = _netCodeNetworkManager.GetComponent<UnityTransport>();
        }

        _sessionSaveService = new NetworkSessionSaveService(_netCodeNetworkManager, this);
        _sessionSaveService.Initialize();
    }

    private void Start()
    {
        if (UIManager.Inst == null)
        {
            Debug.LogError("UIManager가 없습니다.", this);
            return;
        }

        UIManager.Inst.OpenLobbyUIAsync().Forget();
    }

    private void OnDestroy()
    {
        Application.quitting -= OnApplicationQuitting;
        _clientSideService.EndClientService();
        _serverSideService.EndServerService();
        _sessionSaveService?.Dispose();

        if (_hostDisconnected)
        {
            Time.timeScale = _returningToLobby ? 1f : _previousTimeScale;
            AudioListener.pause = _previousAudioPause;
        }

        if (Inst == this)
            Inst = null;
    }

    private void OnApplicationQuitting()
    {
        _isQuitting = true;
    }

    private void Update()
    {
        if (!_isQuitting)
            _sessionSaveService?.Tick(Time.unscaledDeltaTime);

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

    public bool RequestSessionSave()
    {
        return _sessionSaveService != null && _sessionSaveService.RequestSessionSave();
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
        _connectedAsClient = _netCodeNetworkManager != null && !_netCodeNetworkManager.IsServer;
        return true;
    }

    public void HandleLocalClientDisconnected(string reason)
    {
        if (_isQuitting || !Application.isPlaying || Application.exitCancellationToken.IsCancellationRequested)
            return;

        if (HandleConnectionAttemptDisconnected(reason) || !_connectedAsClient || _hostDisconnected)
            return;

        _hostDisconnected = true;
        _connectedAsClient = false;

        // TODO(저장 연결) : 캐릭터 저장 기능 완성 후 여기서 로컬 캐릭터 데이터를 확보하고 한 번 저장한다.
        // NetworkObject가 정리되기 전이므로 확인 버튼이 아닌 이 위치에 저장 처리를 연결한다.

        SaveManager saves = SaveManager.Inst;

        if (saves == null)
        {
            Debug.LogWarning("[연결 종료 저장 실패] SaveManager가 없습니다.", this);
        }
        else if (saves.TrySaveDisconnectedCharacter(out string error) == false)
        {
            Debug.LogWarning($"[연결 종료 저장 실패] {error}", this);
        }
        else
        {
            Debug.Log("[연결 종료 저장 완료] 로컬 캐릭터를 저장했습니다.", this);
        }

        _previousTimeScale = Time.timeScale;
        _previousAudioPause = AudioListener.pause;
        Time.timeScale = 0f;
        AudioListener.pause = true;

        NetworkObject player = _netCodeNetworkManager.LocalClient?.PlayerObject;
        if (player != null)
        {
            if (player.TryGetComponent(out PlayerControllerView controller))
                controller.enabled = false;
            if (player.TryGetComponent(out PlayerInput input))
            {
                input.DeactivateInput();
                input.enabled = false;
            }
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _hostDisconnectedUI = Instantiate(_hostDisconnectedUIPrefab, transform);
        _hostDisconnectedUI.Show(OnConfirmHostDisconnected);
    }

    private void OnConfirmHostDisconnected()
    {
        ReturnToLobbyAsync().Forget();
    }

    private async UniTask ReturnToLobbyAsync()
    {
        if (!_hostDisconnected || _returningToLobby)
            return;

        _returningToLobby = true;
        _hostDisconnectedUI.SetReturning(true);
        var cancellationToken = this.GetCancellationTokenOnDestroy();
        bool sceneReloadStarted = false;

        try
        {
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

            if (_netCodeNetworkManager.IsListening && !_netCodeNetworkManager.ShutdownInProgress)
                _netCodeNetworkManager.Shutdown(true);

            while (_netCodeNetworkManager.IsListening || _netCodeNetworkManager.ShutdownInProgress)
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

            if (!Application.CanStreamedLevelBeLoaded(_lobbyScenePath))
            {
                Debug.LogError($"로비로 돌아갈 씬이 빌드 목록에 없습니다: {_lobbyScenePath}", this);
                return;
            }

            _clientSideService.EndClientService();
            _serverSideService.EndServerService();
            SceneManager.MoveGameObjectToScene(gameObject, SceneManager.GetActiveScene());
            AsyncOperation load = SceneManager.LoadSceneAsync(_lobbyScenePath, LoadSceneMode.Single);

            if (load == null)
            {
                Debug.LogError($"로비 씬을 불러오지 못했습니다: {_lobbyScenePath}", this);
                DontDestroyOnLoad(gameObject);
                return;
            }

            sceneReloadStarted = true;
            await load.ToUniTask(cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            if (this != null)
                DontDestroyOnLoad(gameObject);
        }
        finally
        {
            if (this != null && !cancellationToken.IsCancellationRequested && !sceneReloadStarted && !_isQuitting)
            {
                _returningToLobby = false;
                _hostDisconnectedUI.ShowReturnError();
            }
        }
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

        if (_hostDisconnectedUIPrefab == null)
        {
            error = "호스트 연결 종료 팝업 프리팹을 연결하세요.";
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

    public async UniTask<bool> TryReturnToLobbyAsync()
    {
        if (IsReturningToLobby || _netCodeNetworkManager == null)
        {
            return false;
        }

        if (_netCodeNetworkManager.IsListening == false || _netCodeNetworkManager.ShutdownInProgress)
        {
            return false;
        }

        SaveManager saves = SaveManager.Inst;
        GameManager game = GameManager.Inst;
        UIManager ui = UIManager.Inst;

        if (saves == null || game == null || ui == null)
        {
            Debug.LogError("로비 복귀에 필요한 매니저를 확인해 주세요.", this);
            return false;
        }

        IsReturningToLobby = true;

        try
        {
            // 장착 룬을 읽을 수 있도록 플레이어가 스폰된 상태에서 저장한다.
            if (saves.TrySaveCurrentCharacter(out string error) == false)
            {
                Debug.LogWarning(error, this);
                return false;
            }

            if (_netCodeNetworkManager.IsHost)
            {
                WorldSaveData world = GameStartSelection.World;

                if (world == null)
                {
                    Debug.LogError("저장할 호스트 월드가 없습니다.", this);
                    return false;
                }

                if (saves.SaveWorld(world, out error) == false)
                {
                    Debug.LogError(error, this);
                    return false;
                }
            }

            // 로비 UI를 열 수 없는 경우에는 연결을 끊지 않는다.
            // 현재 설정창은 Popup Root에 있어 로비보다 앞에 남는다.
            LobbyUI lobby = await ui.OpenLobbyUIAsync();

            if (lobby == null)
            {
                Debug.LogError("로비 UI를 열지 못했습니다.", this);
                return false;
            }

            _connectedAsClient = false;
            _netCodeNetworkManager.Shutdown();

            while (_netCodeNetworkManager.IsListening || _netCodeNetworkManager.ShutdownInProgress)
            {
                await UniTask.Yield();
            }

            _clientSideService.EndClientService();
            _serverSideService.EndServerService();

            game.ClearSessionWorld();
            GameStartSelection.Clear();

            ui.CloseSessionWindows();

            return true;
        }
        finally
        {
            IsReturningToLobby = false;
        }
    }
}
