using UnityEngine;

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

    private void Start()
    {
        NetworkManager.Inst.InitInventory();
        StartGame();
    }

    public void StartGame()
    {
        if (_workspacePrefab_1 == null || _workspacePrefab_2 == null || _workspacePrefab_3 == null || _workspacePrefab_4 == null)
        {
            return;
        }

        if (WorldManager.Inst == null)
        {
            Debug.LogWarning("게임 시작 전에 활성 WorldManager가 씬에 있어야 합니다.", this);
            return;
        }

        if (!WorldManager.Inst.Initialize())
            return;

        GameObject playerWorkspace = Instantiate(_workspacePrefab_1);
        PlayerControllerView player = playerWorkspace.GetComponentInChildren<PlayerControllerView>();
        Instantiate(_workspacePrefab_2);
        Instantiate(_workspacePrefab_3);
        GameObject mapWorkspace = Instantiate(_workspacePrefab_4);

        // 맵의 EntryPoint 등록이 끝난 뒤 기존 플레이어를 배치합니다.
        WorldManager.Inst.PlacePlayerAtStart(player);

        InitializeBackground(mapWorkspace);
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
}
