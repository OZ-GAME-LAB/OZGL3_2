using UnityEngine;

[DisallowMultipleComponent]
public sealed class ScenarioNpcBinding : MonoBehaviour
{
    [Header("공유 사건 상태")]
    [SerializeField] private string _completedScenarioId;
    [Header("상태별 NPC 표시")]
    [SerializeField] private GameObject _beforeCompletion;
    [SerializeField] private GameObject _afterCompletion;
    private ScenarioManager _manager;

    private void OnEnable()
    {
        if (string.IsNullOrWhiteSpace(_completedScenarioId) || _beforeCompletion == null ||
            _afterCompletion == null || _beforeCompletion == _afterCompletion ||
            transform.IsChildOf(_beforeCompletion.transform) || transform.IsChildOf(_afterCompletion.transform))
        {
            Debug.LogError("사건 ID와 서로 다른 두 NPC 객체를 연결하고, Binding은 항상 켜진 별도 객체에 배치해 주세요.", this);
            enabled = false;
            return;
        }
        ScenarioManager.AvailabilityChanged += BindManager;
        BindManager();
    }

    private void BindManager()
    {
        if (_manager != null)
            _manager.ProgressChanged -= Refresh;
        _manager = ScenarioManager.Inst;
        if (_manager != null)
            _manager.ProgressChanged += Refresh;
        Refresh();
    }

    private void Refresh()
    {
        bool ready = _manager != null && _manager.IsReady;
        if (ready && (!_manager.TryGetScenario(_completedScenarioId, out ScenarioData data) || !data.CompleteWorldOnFinish))
        {
            Debug.LogError($"공유 완료 사건 ID를 확인해 주세요: {_completedScenarioId}", this);
            ready = false;
        }
        bool completed = ready && _manager.IsCompleted(_completedScenarioId);
        // 상태 수신 전에는 잘못된 지하 NPC가 잠깐 나타나거나 상호작용하지 않게 합니다.
        _beforeCompletion.SetActive(ready && !completed);
        _afterCompletion.SetActive(ready && completed);
    }

    private void OnDisable()
    {
        ScenarioManager.AvailabilityChanged -= BindManager;
        if (_manager != null)
            _manager.ProgressChanged -= Refresh;
        _manager = null;
    }
}
