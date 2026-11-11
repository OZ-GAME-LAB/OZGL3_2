using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class Boss : MonoBehaviour
{
    public MonsterVM ViewModel { get; } = new MonsterVM();

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

    private readonly List<MonsterSkillBase> _skills = new();

    public IReadOnlyList<MonsterSkillBase> Skills => _skills;

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

    public async UniTask<bool> InitSkillsAsync(MonsterData data)
    {
        if (data == null)
            return false;

        string[] skillIds = data.HaveSkillIdList;
        float[] cooldowns = data.MonsterSkillCollTime;

        if (skillIds == null || skillIds.Length == 0)
            return true;

        if (cooldowns == null || cooldowns.Length != skillIds.Length)
        {
            Debug.LogError("보스의 스킬과 쿨타임 배열 개수가 다릅니다.", this);
            return false;
        }

        if (MonsterSkillManager.Inst == null)
        {
            Debug.LogError("MonsterSkillManager가 준비되지 않았습니다.", this);
            return false;
        }

        for (int i = 0; i < skillIds.Length; i++)
        {
            float cooldown = cooldowns[i];

            if (float.IsNaN(cooldown) || float.IsInfinity(cooldown) || cooldown < 0f)
            {
                Debug.LogError($"잘못된 보스 스킬 쿨타임: {skillIds[i]}", this);
                return false;
            }

            MonsterSkillBase skill = await MonsterSkillManager.Inst.CreateSkillAsync(skillIds[i], transform, 0f, data.Atk);

            if (this == null)
                return false;

            if (skill == null)
                return false;

            skill.InitCooldown(cooldown);
            _skills.Add(skill);
        }

        return true;
    }
}
