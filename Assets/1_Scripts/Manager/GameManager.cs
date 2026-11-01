using System.Collections;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public static GameManager Inst { get; private set; }

    private void Awake()
    {
        Inst = this;
    }

    [Header("작업 공간 프리팹 설정")]
    [SerializeField] private GameObject _workspacePrefab_1;
    [SerializeField] private GameObject _workspacePrefab_2;
    [SerializeField] private GameObject _workspacePrefab_3;
    [SerializeField] private GameObject _workspacePrefab_4;

    private bool _worldInitialized;
    private GameObject _mapWorkspace;

    private void Start()
    {
        ServiceManager.Inst.InitInventory();
        //StartGame(); // 로비, 네트워크 선택 UI를 만들었기 때문에 바로 시작하지 않도록 주석처리했습니다.
    }

    public void StartGame()
    {
        if (_worldInitialized)
            return;

        if (_workspacePrefab_1 == null || _workspacePrefab_2 == null || _workspacePrefab_3 == null || _workspacePrefab_4 == null)
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

        _worldInitialized = true;

        if (NetworkManager.Singleton.IsClient)
        {
            InitializeBackground(_mapWorkspace);
            StartCoroutine(ConnectLocalPlayer());
        }
    }

    private void InitializeEnvironmentPresentation(GameObject mapWorkspace, PlayerControllerView player)
    {
        if (mapWorkspace == null || player == null)
            return;

        EnvironmentPresentationController controller =
            mapWorkspace.GetComponentInChildren<EnvironmentPresentationController>(true);
        if (controller == null)
            return; // 컨트롤러를 배치하지 않은 기존 맵은 그대로 실행합니다.

        controller.Initialize(player.transform, Camera.main, mapWorkspace.transform);
    }

    private void InitializeBackground(GameObject mapWorkspace)
    {
        if (mapWorkspace == null)
            return;

        ParallaxBackground background =
            mapWorkspace.GetComponentInChildren<ParallaxBackground>(true);

        if (background == null)
        {
            Debug.LogWarning("SungjunWork 안에 ParallaxBackground가 없습니다.", mapWorkspace);
            return;
        }

        // 기존 씬의 출력 카메라를 게임 시작 시 한 번만 가져온다.
        Camera outputCamera = Camera.main;
        if (outputCamera == null)
        {
            Debug.LogWarning("MainCamera 태그가 지정된 활성 Camera가 필요합니다.", this);
            return;
        }

        if (!background.Initialize(outputCamera))
            Debug.LogWarning("배경 초기화에 실패했습니다. Inspector 연결을 확인해 주세요.", background);
    }

    private IEnumerator ConnectLocalPlayer()
    {
        NetworkManager network = NetworkManager.Singleton;
        NetworkObject playerObject = null;

        while (network != null && network.IsListening)
        {
            if (network.LocalClient != null)
            {
                playerObject = network.LocalClient.PlayerObject;
            }

            if (playerObject != null && playerObject.IsSpawned)
                break;

            yield return null;
        }

        if (network == null || !network.IsListening ||
            playerObject == null)
        {
            yield break;
        }

        if (!playerObject.TryGetComponent(out PlayerControllerView player) ||
            !playerObject.TryGetComponent(out PlayerInput input) ||
            !playerObject.TryGetComponent(out Rigidbody2D body))
        {
            Debug.LogError("플레이어 프리팹의 필수 컴포넌트를 확인해 주세요.");
            yield break;
        }

        // LocalClient.PlayerObject이므로 본인 플레이어만 활성화
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

        InitializeEnvironmentPresentation(_mapWorkspace, player);
    }
}
