using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// 클라이언트와 서버의 로컬 맵에 같은 SourceId로 배치합니다.
[DisallowMultipleComponent]
public sealed class ScenarioSource : MonoBehaviour
{
    private static readonly Dictionary<string, ScenarioSource> Sources = new Dictionary<string, ScenarioSource>();

    [Header("사건 연결")]
    [SerializeField] private string _sourceId;
    [SerializeField] private string _scenarioId;
    [SerializeField] private Collider2D _activationArea;

    [Header("서버 위치 검사")]
    [Tooltip("플레이어 중심과 진입 영역 사이에 허용할 거리입니다. 캐릭터 크기와 위치 동기화 오차를 고려합니다.")]
    [SerializeField, Min(0f)] private float _positionTolerance = 2f;

    public string SourceId => _sourceId;
    public string ScenarioId => _scenarioId;
    public event System.Action AvailabilityChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        Sources.Clear();
    }

    private void OnEnable()
    {
        if (string.IsNullOrWhiteSpace(_sourceId) || string.IsNullOrWhiteSpace(_scenarioId) || _activationArea == null)
        {
            Debug.LogError("ScenarioSource의 SourceId, ScenarioId, Activation Area를 연결해 주세요.", this);
            return;
        }
        if (Sources.ContainsKey(_sourceId))
        {
            Debug.LogError($"중복된 시나리오 SourceId입니다: {_sourceId}", this);
            return;
        }
        Sources.Add(_sourceId, this);
        AvailabilityChanged?.Invoke();
    }

    private void OnDisable()
    {
        if (!string.IsNullOrEmpty(_sourceId) && Sources.TryGetValue(_sourceId, out ScenarioSource source) && source == this)
            Sources.Remove(_sourceId);
        ServiceManager.Inst?.ScenarioService?.CancelFromSource(this);
        AvailabilityChanged?.Invoke();
    }

    public bool ContainsPlayer(NetworkObject player)
    {
        if (player == null || !isActiveAndEnabled || _activationArea == null ||
            !_activationArea.enabled || !_activationArea.gameObject.activeInHierarchy)
            return false;

        Vector2 position = player.transform.position;
        return (_activationArea.ClosestPoint(position) - position).sqrMagnitude <=
            _positionTolerance * _positionTolerance;
    }

    public static bool TryGet(string sourceId, out ScenarioSource source)
    {
        source = null;
        return !string.IsNullOrEmpty(sourceId) && Sources.TryGetValue(sourceId, out source) && source != null;
    }
}
