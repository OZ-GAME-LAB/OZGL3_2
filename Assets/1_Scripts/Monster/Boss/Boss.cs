using System.Collections.Generic;
using UnityEngine;

public class Boss : MonoBehaviour
{
    [Header("지면 감지 — 가시덩굴")]
    [SerializeField] private BossDetector[] _groundDetectors;

    [Header("근접 감지 — 일방향 덩굴")]
    [SerializeField] private BossDetector[] _nearDetectors;

    [Header("좌우 1단 발판 감지 — 독액 분사")]
    [SerializeField] private BossDetector[] _firstPlatformDetectors;

    [Header("전체 발판 — 침식 및 하얀 꽃")]
    [SerializeField] private Transform[] _platformPoints;

    public IReadOnlyList<Transform> PlatformPoints => _platformPoints;

    private readonly List<PlayerNetworkStats> _detectedPlayers = new();

    private readonly List<PlayerNetworkStats> _groundPlayers = new();

    private readonly List<BossDetector> _groundAttackCandidates = new();


    public void GetGroundPlayers(List<PlayerNetworkStats> results)
    {
        CollectPlayers(_groundDetectors, results);
    }

    public void GetNearPlayers(List<PlayerNetworkStats> results)
    {
        CollectPlayers(_nearDetectors, results);
    }

    public void GetFirstPlatformPlayers(List<PlayerNetworkStats> results)
    {
        CollectPlayers(_firstPlatformDetectors, results);
    }

    private void CollectPlayers(BossDetector[] detectors, List<PlayerNetworkStats> results)
    {
        results.Clear();

        if (detectors == null)
            return;

        foreach (BossDetector detector in detectors)
        {
            AppendPlayers(detector, results);
        }
    }

    private void AppendPlayers(BossDetector detector, List<PlayerNetworkStats> results)
    {
        if (detector == null)
            return;

        detector.GetAlivePlayers(_detectedPlayers);

        foreach (PlayerNetworkStats player in _detectedPlayers)
        {
            if (!results.Contains(player))
            {
                results.Add(player);
            }
        }
    }

    public bool HasGroundPlayers()
    {
        GetGroundPlayers(_groundPlayers);

        return _groundPlayers.Count > 0;
    }

    public bool TrySelectGroundAttackArea(out BossDetector selectedArea)
    {
        selectedArea = null;
        _groundAttackCandidates.Clear();

        if (_groundDetectors == null)
            return false;

        foreach (BossDetector detector in _groundDetectors)
        {
            if (detector == null)
                continue;

            detector.GetAlivePlayers(_detectedPlayers);

            if (_detectedPlayers.Count == 0)
                continue;

            if (!_groundAttackCandidates.Contains(detector))
            {
                _groundAttackCandidates.Add(detector);
            }
        }

        if (_groundAttackCandidates.Count == 0)
            return false;

        int index = Random.Range(0, _groundAttackCandidates.Count);
        selectedArea = _groundAttackCandidates[index];

        return true;
    }



    [ContextMenu("테스트/가시덩굴 지면 선택")]
    private void TestSelectGroundAttackArea()
    {
        var manager = Unity.Netcode.NetworkManager.Singleton;

        if (!Application.isPlaying ||
            manager == null ||
            !manager.IsServer)
        {
            Debug.LogWarning("Host 또는 Server 실행 중에 확인해 주세요.", this);
            return;
        }

        if (!TrySelectGroundAttackArea(out BossDetector selectedArea))
        {
            Debug.Log("공격 가능한 지면이 없습니다.", this);
            return;
        }

        Debug.Log($"가시덩굴 공격 지면: {selectedArea.name}", selectedArea);
    }
}
