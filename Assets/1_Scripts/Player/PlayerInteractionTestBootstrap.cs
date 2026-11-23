using System.Collections;
using Unity.Cinemachine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>단일 호스트 테스트 씬의 시작 순서만 담당합니다.</summary>
public class PlayerInteractionTestBootstrap : MonoBehaviour
{
    [Header("씬 연결")]
    [SerializeField] private NetworkManager _networkManager;
    [SerializeField] private ObjectManager _objectManager;
    [SerializeField] private WorldManager _worldManager;
    [SerializeField] private ServiceManager _services;
    [SerializeField] private StageEntryPoint _entryPoint;
    [SerializeField] private ParallaxBackground _background;
    [SerializeField] private Camera _outputCamera;

    [Header("로컬 호스트")]
    [SerializeField] private ushort _port = 7779;

    public PlayerControllerView Player { get; private set; }
    public bool IsReady { get; private set; }
    private bool _startedHost;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private IEnumerator Start()
    {
        if (_networkManager == null || _objectManager == null || _worldManager == null ||
            _services == null || _entryPoint == null || DataManager.Inst == null)
        {
            Debug.LogError("테스트 씬의 필수 참조가 비어 있습니다.", this);
            yield break;
        }

        _worldManager.RegisterStageEntryPoint(_entryPoint);
        if (_networkManager.IsListening)
        {
            Debug.LogError("테스트 씬은 별도의 로컬 호스트로 실행해 주세요.", this);
            yield break;
        }

        UnityTransport transport = _networkManager.GetComponent<UnityTransport>();
        transport.SetConnectionData("127.0.0.1", _port, "127.0.0.1");
        _startedHost = _networkManager.StartHost();
        if (!_startedHost)
        {
            Debug.LogError("테스트 호스트를 시작하지 못했습니다.", this);
            yield break;
        }

        float deadline = Time.realtimeSinceStartup + 10f;
        while (!_networkManager.IsConnectedClient && Time.realtimeSinceStartup < deadline)
            yield return null;

        if (!_networkManager.IsConnectedClient)
        {
            Debug.LogError("테스트 호스트 연결 시간이 초과되었습니다.", this);
            yield break;
        }

        NetworkObject playerObject = _objectManager.SpawnPlayer(
            _networkManager.LocalClientId, _entryPoint.transform.position);
        if (playerObject == null || !playerObject.TryGetComponent(out PlayerControllerView player))
            yield break;

        Player = player;
        playerObject.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic;
        player.enabled = true;
        playerObject.GetComponent<PlayerInput>().enabled = true;
        _worldManager.PlacePlayerAtStart(player);

        CinemachineCamera followCamera = playerObject.GetComponentInChildren<CinemachineCamera>(true);
        if (followCamera != null)
        {
            followCamera.Follow = player.transform;
            followCamera.gameObject.SetActive(true);
        }

        if (_background != null && !_background.Initialize(_outputCamera))
            Debug.LogError("테스트 씬의 지상 배경 초기화에 실패했습니다.", this);

        _services.InitInventory();

        IsReady = true;
        Debug.Log("[InteractionTest] Ready: player and all runes.", this);
    }

    private void OnDestroy()
    {
        if (_startedHost && _networkManager != null && _networkManager.IsListening)
            _networkManager.Shutdown();
    }
#endif
}
