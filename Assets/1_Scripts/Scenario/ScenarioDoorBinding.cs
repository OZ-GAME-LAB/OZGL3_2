using UnityEngine;

/// <summary>공유 사건의 완료 상태를 읽어 문 열림에 반영합니다.</summary>
[DisallowMultipleComponent]
public sealed class ScenarioDoorBinding : MonoBehaviour
{
    [Header("공유 사건 상태")]
    [SerializeField] private string _completedScenarioId;
    [Header("연결할 문")]
    [SerializeField] private EventDoorOpen _door;

    [SerializeField] private PuzzleDoorShot _shot;
    private ScenarioManager _manager;
    private bool _hasSnapshot;
    private bool _wasCompleted;

    private void OnEnable()
    {
        if (string.IsNullOrWhiteSpace(_completedScenarioId) || _door == null)
        {
            Debug.LogError("문에 연결할 공유 사건 ID와 EventDoorOpen을 지정해 주세요.", this);
            RestoreClosed();
            enabled = false;
            return;
        }
        ScenarioManager.AvailabilityChanged += BindManager;
        BindManager();
    }

    private void BindManager()
    {
        ScenarioManager manager = ScenarioManager.Inst;
        if (_manager != manager)
        {
            if (_manager != null)
            {
                _manager.ProgressChanged -= Refresh;
                _manager.PuzzleDoorPresentation -= OnPuzzlePresentation;
            }
            _shot?.StopShot();
            _manager = manager;
            _hasSnapshot = false;
            if (_manager != null)
            {
                _manager.ProgressChanged += Refresh;
                _manager.PuzzleDoorPresentation += OnPuzzlePresentation;
            }
        }
        Refresh();
    }

    private void Refresh()
    {
        if (_manager == null || !_manager.IsReady)
        {
            RestoreClosed();
            return;
        }
        if (!_manager.TryGetScenario(_completedScenarioId, out ScenarioData data) || !data.CompleteWorldOnFinish)
        {
            Debug.LogError($"문에 연결된 공유 완료 사건 ID를 확인해 주세요: {_completedScenarioId}", this);
            RestoreClosed();
            return;
        }

        bool completed = _manager.IsCompleted(_completedScenarioId);
        // 늦은 접속이나 재활성화에서는 현재 상태를 바로 표시합니다.
        if (!_hasSnapshot)
            _door.SetOpenImmediate(completed);
        else if (completed && !_wasCompleted)
            _door.Open();
        else if (!completed && _wasCompleted)
            _door.SetOpenImmediate(false);

        _hasSnapshot = true;
        _wasCompleted = completed;
    }

    private void OnPuzzlePresentation(string scenarioId, bool solver)
    {
        if (scenarioId == _completedScenarioId && _shot != null)
            _shot.Present(_door, solver);
    }

    private void RestoreClosed()
    {
        _hasSnapshot = false;
        _wasCompleted = false;
        if (_door != null)
            _door.SetOpenImmediate(false);
    }

    private void OnDisable()
    {
        ScenarioManager.AvailabilityChanged -= BindManager;
        if (_manager != null)
        {
            _manager.ProgressChanged -= Refresh;
            _manager.PuzzleDoorPresentation -= OnPuzzlePresentation;
        }
        _shot?.StopShot();
        _manager = null;
        _hasSnapshot = false;
        _wasCompleted = false;
    }
}
