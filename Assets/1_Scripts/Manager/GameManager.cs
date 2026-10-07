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
        StartGame();
    }

    public void StartGame()
    {
        if (_workspacePrefab_1 == null || _workspacePrefab_2 == null || _workspacePrefab_3 == null || _workspacePrefab_4 == null)
        {
            return;
        }

        Instantiate(_workspacePrefab_1);
        Instantiate(_workspacePrefab_2);
        Instantiate(_workspacePrefab_3);
        Instantiate(_workspacePrefab_4);


    }
}
