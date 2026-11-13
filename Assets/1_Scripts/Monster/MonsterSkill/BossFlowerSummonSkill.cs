using Unity.Netcode;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public class BossFlowerSummonSkill : MonsterSkillBase
{
    [SerializeField] private string _flowerMonsterId = "monster_test_boss_flower";

    private Boss _boss;
    private BossNetwork _bossNetwork;
    private MonsterNetwork _flower;
    private Transform _reservedPlatform;
    private bool _initialized;

    public override bool RequiresAttackPrefab => false;

    public override bool InitSkill(Transform owner, float bodyHeight, float atk, GameObject attackPrefab)
    {
        _initialized = false;

        if (owner == null || !owner.TryGetComponent(out _boss)
            || !owner.TryGetComponent(out _bossNetwork))
        {
            return FailInitialization("꽃 소환 스킬의 소유 오브젝트에 Boss와 BossNetwork가 필요합니다.");
        }

        if (DataManager.Inst == null || string.IsNullOrWhiteSpace(_flowerMonsterId))
        {
            return FailInitialization("꽃 소환에 필요한 DataManager와 몬스터 ID를 확인하세요.");
        }

        MonsterData data = DataManager.Inst.GetData<MonsterData>(_flowerMonsterId);
        if (data == null || data.MonsterType != MoveType.Fix)
        {
            return FailInitialization($"고정형 꽃 몬스터 데이터가 필요합니다: {_flowerMonsterId}");
        }

        _initialized = true;
        return true;
    }

    public override void UseSkill(Transform target, Vector3 attackPosition)
    {
        if (!_initialized || !isActiveAndEnabled || IsAttacking || !IsCooldownComplete || _flower != null)
            return;

        if (_boss == null || _bossNetwork == null || !_bossNetwork.IsServer
            || !_bossNetwork.IsSpawned || !_bossNetwork.IsReady || !_bossNetwork.IsAlive)
            return;

        if (!HasPlayerWithBossPoison())
            return;

        if (ObjectManager.Inst == null)
        {
            Debug.LogError("꽃을 소환할 ObjectManager가 준비되지 않았습니다.", this);
            _initialized = false;
            return;
        }

        if (!_boss.TryReserveFlowerPlatform(out _reservedPlatform, out Vector3 spawnPosition))
            return;

        IsAttacking = true;
        try
        {
            Monster instance = ObjectManager.Inst.SpawnMonster(_flowerMonsterId, null, spawnPosition);
            if (instance == null || !instance.TryGetComponent(out _flower) || !_flower.IsSpawned)
            {
                Debug.LogError($"꽃 소환에 실패했습니다. 몬스터 데이터와 프리팹 연결을 확인하세요: {_flowerMonsterId}", this);
                return;
            }

            _flower.Died += OnFlowerDied;
            _flower.Despawned += OnFlowerDespawned;
            Debug.Log($"하얀 꽃 소환: {_reservedPlatform.name}", _flower);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
        }
        finally
        {
            if (_flower == null || !_flower.IsSpawned)
            {
                ReleaseFlower();
                _initialized = false;
            }
        }
    }

    private bool HasPlayerWithBossPoison()
    {
        NetworkManager manager = _bossNetwork.NetworkManager;
        if (manager == null || !manager.IsListening)
            return false;

        foreach (NetworkClient client in manager.ConnectedClientsList)
        {
            NetworkObject player = client.PlayerObject;
            if (player == null || !player.IsSpawned || !player.gameObject.activeInHierarchy)
                continue;

            if (player.TryGetComponent(out PlayerNetworkStats stats)
                && stats.IsAlive && stats.BossPoisonStackCount >= 2)
                return true;
        }

        return false;
    }

    private void OnFlowerDied(MonsterNetwork flower)
    {
        if (flower != _flower)
            return;

        ReleaseFlower();
        StartCooldown();
    }

    private void OnFlowerDespawned(MonsterNetwork flower)
    {
        if (flower != _flower)
            return;

        ReleaseFlower();
        _initialized = false;
        if (_bossNetwork != null && _bossNetwork.IsSpawned && _bossNetwork.IsAlive)
            Debug.LogWarning("꽃이 사망 처리 없이 사라졌습니다. 꽃 초기화 로그를 확인하세요. 이번 보스의 꽃 재소환을 중지합니다.", this);
    }

    private void ReleaseFlower()
    {
        if (_flower != null)
        {
            _flower.Died -= OnFlowerDied;
            _flower.Despawned -= OnFlowerDespawned;
        }

        if (_boss != null && _reservedPlatform != null)
            _boss.ReleasePlatform(_reservedPlatform);

        _flower = null;
        _reservedPlatform = null;
        IsAttacking = false;
    }

    private void OnDestroy()
    {
        ReleaseFlower();
    }
}
