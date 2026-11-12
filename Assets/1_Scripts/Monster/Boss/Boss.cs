using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class Boss : MonoBehaviour
{
    private readonly HashSet<Transform> _reservedPlatforms = new();
    private readonly List<Transform> _platformCandidates = new();
    private readonly List<Transform> _occupiedPlatformCandidates = new();

    public MonsterVM ViewModel { get; } = new MonsterVM();

    [Header("지면 감지 — 가시덩굴")]
    [SerializeField] private BossDetector[] _groundDetectors;

    [Header("근접 감지 — 일방향 덩굴")]
    [SerializeField] private BossDetector[] _nearDetectors;

    [Header("좌우 1단 발판 감지 — 독액 분사")]
    [SerializeField] private BossDetector[] _firstPlatformDetectors;

    [Header("전체 발판 — 침식 및 하얀 꽃")]
    [SerializeField] private Transform[] _platformPoints;

    [SerializeField] private SpriteRenderer _bodyRenderer;

    public IReadOnlyList<Transform> PlatformPoints => _platformPoints;

    private readonly List<PlayerNetworkStats> _detectedPlayers = new();

    private readonly List<PlayerNetworkStats> _groundPlayers = new();

    private readonly List<PlayerNetworkStats> _nearPlayers = new();

    private readonly List<PlayerNetworkStats> _firstPlatformPlayers = new();

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

        if (_bodyRenderer == null)
        {
            Debug.LogError("보스의 몸체 SpriteRenderer가 연결되지 않았습니다.", this);
            return false;
        }

        float bodyHeight = _bodyRenderer.bounds.size.y;

        for (int i = 0; i < skillIds.Length; i++)
        {
            float cooldown = cooldowns[i];

            if (float.IsNaN(cooldown) || float.IsInfinity(cooldown) || cooldown < 0f)
            {
                Debug.LogError($"잘못된 보스 스킬 쿨타임: {skillIds[i]}", this);
                return false;
            }

            MonsterSkillBase skill = await MonsterSkillManager.Inst.CreateSkillAsync(skillIds[i], transform, bodyHeight, data.Atk);

            if (this == null)
                return false;

            if (skill == null)
                return false;

            skill.InitCooldown(cooldown);
            _skills.Add(skill);
        }

        return true;
    }

    public bool TryReservePlatforms(List<Transform> results)
    {
        results.Clear();
        _platformCandidates.Clear();
        _occupiedPlatformCandidates.Clear();

        var manager = Unity.Netcode.NetworkManager.Singleton;
        if (manager == null || !manager.IsServer)
            return false;

        if (_platformPoints == null)
            return false;

        foreach (Transform platform in _platformPoints)
        {
            if (platform == null || !platform.gameObject.activeInHierarchy)
                continue;

            if (_reservedPlatforms.Contains(platform) || _platformCandidates.Contains(platform))
                continue;

            if (!platform.TryGetComponent(out BoxCollider2D area) || !area.enabled)
                continue;

            _platformCandidates.Add(platform);

            if (!platform.TryGetComponent(out BossDetector detector))
                continue;

            detector.GetAlivePlayers(_detectedPlayers);

            if (_detectedPlayers.Count > 0)
                _occupiedPlatformCandidates.Add(platform);
        }

        if (_occupiedPlatformCandidates.Count == 0)
            return false;

        int occupiedIndex = Random.Range(0, _occupiedPlatformCandidates.Count);
        Transform firstPlatform = _occupiedPlatformCandidates[occupiedIndex];

        results.Add(firstPlatform);
        _platformCandidates.Remove(firstPlatform);

        if (_platformCandidates.Count > 0)
        {
            int randomIndex = Random.Range(0, _platformCandidates.Count);
            Transform secondPlatform = _platformCandidates[randomIndex];

            results.Add(secondPlatform);
        }

        foreach (Transform platform in results)
        {
            _reservedPlatforms.Add(platform);
        }

        return true;
    }

    public bool TryReserveFlowerPlatform(out Transform selectedPlatform, out Vector3 spawnPosition)
    {
        selectedPlatform = null;
        spawnPosition = default;
        _platformCandidates.Clear();

        var manager = Unity.Netcode.NetworkManager.Singleton;
        if (manager == null || !manager.IsServer || _platformPoints == null)
            return false;

        foreach (Transform platform in _platformPoints)
        {
            if (platform == null || !platform.gameObject.activeInHierarchy)
                continue;

            if (_reservedPlatforms.Contains(platform) || _platformCandidates.Contains(platform))
                continue;

            if (!platform.TryGetComponent(out BoxCollider2D area) || !area.enabled)
                continue;

            _platformCandidates.Add(platform);
        }

        if (_platformCandidates.Count == 0)
            return false;

        selectedPlatform = _platformCandidates[Random.Range(0, _platformCandidates.Count)];
        Bounds bounds = selectedPlatform.GetComponent<BoxCollider2D>().bounds;
        spawnPosition = new Vector3(bounds.center.x, bounds.max.y, selectedPlatform.position.z);
        _reservedPlatforms.Add(selectedPlatform);
        return true;
    }

    public void ReleasePlatform(Transform platform)
    {
        var manager = Unity.Netcode.NetworkManager.Singleton;
        if (manager == null || !manager.IsServer)
            return;

        _reservedPlatforms.Remove(platform);
    }

    public bool TrySelectNearAttackDirection(out int direction)
    {
        direction = 0;

        GetNearPlayers(_nearPlayers);

        if (_nearPlayers.Count == 0)
            return false;

        bool hasLeftPlayer = false;
        bool hasRightPlayer = false;

        float bossX = transform.position.x;

        foreach (PlayerNetworkStats player in _nearPlayers)
        {
            if (player.transform.position.x < bossX)
            {
                hasLeftPlayer = true;
            }
            else
            {
                hasRightPlayer = true;
            }
        }

        if (hasLeftPlayer && hasRightPlayer)
        {
            direction = Random.Range(0, 2) == 0 ? -1 : 1;
        }
        else if (hasLeftPlayer)
        {
            direction = -1;
        }
        else
        {
            direction = 1;
        }

        return true;
    }

    public bool TryGetDirectionalThornBounds(int direction, out Bounds attackBounds)
    {
        attackBounds = default;

        if (direction != -1 && direction != 1)
            return false;

        if (_groundDetectors == null || _bodyRenderer == null)
            return false;

        Bounds bodyBounds = _bodyRenderer.bounds;

        foreach (BossDetector detector in _groundDetectors)
        {
            if (detector == null || !detector.isActiveAndEnabled)
                continue;

            if (!detector.TryGetComponent(out BoxCollider2D area) || !area.enabled)
                continue;

            Bounds groundBounds = area.bounds;

            Vector3 min = groundBounds.min;
            Vector3 max = groundBounds.max;

            if (direction > 0)
            {
                min.x = Mathf.Max(min.x, bodyBounds.max.x);
            }
            else
            {
                max.x = Mathf.Min(max.x, bodyBounds.min.x);
            }

            if (max.x <= min.x)
                continue;

            attackBounds.SetMinMax(min, max);
            return true;
        }

        return false;
    }

    public bool TrySelectPoisonSprayDirection(out int direction)
    {
        direction = 0;

        GetFirstPlatformPlayers(_firstPlatformPlayers);

        if (_firstPlatformPlayers.Count == 0)
            return false;

        bool hasLeftPlayer = false;
        bool hasRightPlayer = false;

        float bossX = transform.position.x;

        foreach (PlayerNetworkStats player in _firstPlatformPlayers)
        {
            if (player.transform.position.x < bossX)
            {
                hasLeftPlayer = true;
            }
            else
            {
                hasRightPlayer = true;
            }
        }

        if (hasLeftPlayer && hasRightPlayer)
        {
            direction = Random.Range(0, 2) == 0 ? -1 : 1;
        }
        else if (hasLeftPlayer)
        {
            direction = -1;
        }
        else
        {
            direction = 1;
        }

        return true;
    }
}
