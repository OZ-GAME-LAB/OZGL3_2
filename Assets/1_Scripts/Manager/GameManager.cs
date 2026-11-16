using System.Collections;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public static GameManager Inst { get; private set; }

    [Header("작업 공간 프리팹 설정")]
    [SerializeField] private GameObject _workspacePrefab_1;
    [SerializeField] private GameObject _workspacePrefab_2;
    [SerializeField] private GameObject _workspacePrefab_3;
    [SerializeField] private GameObject _workspacePrefab_4;

    [Header("시나리오 네트워크 프리팹")]
    [SerializeField] private NetworkObject _scenarioManagerPrefab;

    private bool _worldInitialized;
    private GameObject _mapWorkspace;
    private FieldInteractionPoint[] _fieldPoints;
    private Coroutine _connectLocalPlayerRoutine;
    private NetworkObject _connectedLocalPlayer;

    private void Awake()
    {
        Inst = this;
    }

    private void Start()
    {
        ServiceManager.Inst.InitInventory();

        // 로비와 네트워크 연결 흐름에서 StartGame()을 호출합니다.
    }

    public void StartGame()
    {
        if (_worldInitialized)
        {
            InitializeScenarioManager();
            InitializeFieldPoints();
            BeginConnectLocalPlayer();
            return;
        }

        if (_workspacePrefab_1 == null ||
            _workspacePrefab_2 == null ||
            _workspacePrefab_3 == null ||
            _workspacePrefab_4 == null)
        {
            Debug.LogError("GameManager의 작업 공간 프리팹이 비어 있습니다.");
            return;
        }

        if (WorldManager.Inst == null || !WorldManager.Inst.Initialize())
        {
            Debug.LogError("활성 WorldManager를 확인해 주세요.");
            return;
        }

        Instantiate(_workspacePrefab_1);
        Instantiate(_workspacePrefab_2);
        Instantiate(_workspacePrefab_3);
        _mapWorkspace = Instantiate(_workspacePrefab_4);
        _fieldPoints = _mapWorkspace.GetComponentsInChildren<FieldInteractionPoint>(true);

        _worldInitialized = true;
        InitializeScenarioManager();
        InitializeFieldPoints();
        BeginConnectLocalPlayer();
    }

    private void InitializeFieldPoints()
    {
        if (_fieldPoints == null) return;
        foreach (FieldInteractionPoint point in _fieldPoints)
            if (point != null && point.isActiveAndEnabled) point.InitializeOnServer();
    }

    private void BeginConnectLocalPlayer()
    {
        NetworkManager network = NetworkManager.Singleton;
        if (network == null || !network.IsClient ||
            (_connectedLocalPlayer != null && _connectedLocalPlayer.IsSpawned &&
             network.LocalClient != null && network.LocalClient.PlayerObject == _connectedLocalPlayer))
            return;
        if (_connectLocalPlayerRoutine != null) StopCoroutine(_connectLocalPlayerRoutine);
        _connectLocalPlayerRoutine = StartCoroutine(ConnectLocalPlayer());
    }

    private void InitializeScenarioManager()
    {
        NetworkManager network = NetworkManager.Singleton;
        if (network == null || !network.IsServer || ScenarioManager.Inst != null || _scenarioManagerPrefab == null)
            return;
        if (ObjectManager.Inst == null || !_scenarioManagerPrefab.TryGetComponent<ScenarioManager>(out _))
        {
            Debug.LogError("ObjectManager 및 ScenarioManager 컴포넌트가 있는 네트워크 프리팹을 확인해 주세요.", this);
            return;
        }
        ObjectManager.Inst.SpawnObject(_scenarioManagerPrefab, Vector3.zero, Quaternion.identity);
    }

    private IEnumerator ConnectLocalPlayer()
    {
        NetworkManager network = NetworkManager.Singleton;
        NetworkObject playerObject = null;

        // 네트워크에서 본인 플레이어의 생성이 완료될 때까지 기다립니다.
        while (network != null && network.IsListening)
        {
            if (network.LocalClient != null)
                playerObject = network.LocalClient.PlayerObject;

            if (playerObject != null && playerObject.IsSpawned)
                break;

            yield return null;
        }

        if (network == null || !network.IsListening || playerObject == null)
            yield break;

        if (!playerObject.TryGetComponent(out PlayerControllerView player) ||
            !playerObject.TryGetComponent(out PlayerInput input) ||
            !playerObject.TryGetComponent(out Rigidbody2D body))
        {
            Debug.LogError("플레이어 프리팹의 필수 컴포넌트를 확인해 주세요.");
            yield break;
        }

        // LocalClient.PlayerObject이므로 본인 플레이어만 활성화합니다.
        _connectedLocalPlayer = playerObject;
        body.bodyType = RigidbodyType2D.Dynamic;
        player.enabled = true;
        input.enabled = true;

        CinemachineCamera followCamera =
            playerObject.GetComponentInChildren<CinemachineCamera>(true);

        if (followCamera != null)
        {
            followCamera.Follow = player.transform;
            followCamera.gameObject.SetActive(true);
        }

        // 실제 로컬 플레이어를 확보한 뒤 배경과 장식에 연결합니다.
        InitializeBackground(_mapWorkspace, player);
    }

    private void InitializeBackground(
        GameObject mapWorkspace,
        PlayerControllerView player)
    {
        if (mapWorkspace == null || player == null)
            return;

        Camera outputCamera = Camera.main;
        if (outputCamera == null)
        {
            Debug.LogWarning(
                "MainCamera 태그가 지정된 활성 Camera가 필요합니다.",
                this);
            return;
        }

        InitializeSceneryGroups(
            mapWorkspace,
            outputCamera,
            player.transform);

        ParallaxBackground background =
            mapWorkspace.GetComponentInChildren<ParallaxBackground>(true);

        if (background == null)
        {
            Debug.LogWarning(
                "SungjunWork 안에 ParallaxBackground가 없습니다.",
                mapWorkspace);
            return;
        }

        if (!background.Initialize(outputCamera))
        {
            Debug.LogWarning(
                "배경 초기화에 실패했습니다. Inspector 연결을 확인해 주세요.",
                background);
        }
    }

    private void InitializeSceneryGroups(
        GameObject mapWorkspace,
        Camera outputCamera,
        Transform player)
    {
        // 비활성 지역도 한 번 연결하고, 활성화될 때 추적을 시작합니다.
        ParallaxSceneryGroup[] groups =
            mapWorkspace.GetComponentsInChildren<ParallaxSceneryGroup>(true);

        for (int i = 0; i < groups.Length; i++)
        {
            if (!groups[i].Initialize(outputCamera, player))
            {
                Debug.LogWarning(
                    "장식 Parallax 초기화에 실패했습니다.",
                    groups[i]);
            }
        }
    }
}
