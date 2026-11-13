using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public class BossPoisonSpraySkill : MonsterSkillBase
{
    private Boss _boss;
    private BossNetwork _bossNetwork;
    private BossPoisonPath _path;
    private BossPoisonProjectile _projectile;
    private float _attackPower;
    private uint _attackId;

    public override bool InitSkill(Transform owner, float bodyHeight, float atk, GameObject attackPrefab)
    {
        if (_projectile != null)
            return true;

        if (owner == null || attackPrefab == null
            || !owner.TryGetComponent(out _boss)
            || !owner.TryGetComponent(out _bossNetwork)
            || !owner.TryGetComponent(out _path))
        {
            return FailInitialization("독액 스킬에는 Boss, BossNetwork, BossPoisonPath와 공격 프리팹이 필요합니다.");
        }

        if (!_path.TryGetTrajectory(-1, out _, out _, out _)
            || !_path.TryGetTrajectory(1, out _, out _, out _))
        {
            return FailInitialization("좌우 독액 경로의 시작·통과·도착 기준점을 확인해 주세요.");
        }

        if (!attackPrefab.TryGetComponent(out BossPoisonProjectile projectile)
            || !projectile.HasValidSettings() || float.IsNaN(atk) || float.IsInfinity(atk) || atk <= 0f)
        {
            return FailInitialization("독액 프리팹의 외형·시간·반경과 보스 공격력을 확인해 주세요.");
        }

        _attackPower = atk;
        _projectile = Instantiate(projectile, owner.position, Quaternion.identity);
        _projectile.transform.localScale = Vector3.one;
        _projectile.gameObject.SetActive(false);
        _projectile.FlightStarted += StartCooldown;
        _projectile.Finished += FinishAttack;
        return true;
    }

    public override void UseSkill(Transform target, Vector3 attackPosition)
    {
        if (!isActiveAndEnabled || IsAttacking || !IsCooldownComplete || _projectile == null)
            return;
        if (_bossNetwork == null || !_bossNetwork.IsServer || !_bossNetwork.IsSpawned || !_bossNetwork.IsReady)
            return;
        if (!_boss.TrySelectPoisonSprayDirection(out int direction))
            return;
        if (!_path.TryGetTrajectory(direction, out Vector3 start, out Vector3 control, out Vector3 end))
            return;

        double startTime = _bossNetwork.NetworkManager.ServerTime.Time;
        _projectile.transform.SetPositionAndRotation(start, Quaternion.identity);
        _projectile.gameObject.SetActive(true);
        IsAttacking = true;

        _attackId = _bossNetwork.BroadcastPoisonSpray(SkillId, start, control, end, startTime,
            _projectile.WarningSeconds, _projectile.FlightSeconds);
        _projectile.Play(_bossNetwork, true, CreateDamageInfo(_attackPower), start, control, end, startTime,
            _projectile.WarningSeconds, _projectile.FlightSeconds);
        if (!_projectile.IsPlaying)
            FinishAttack();
    }

    private void FinishAttack()
    {
        if (_attackId != 0 && _bossNetwork != null)
            _bossNetwork.BroadcastPoisonSprayFinished(_attackId);
        _attackId = 0;

        if (_projectile != null)
            _projectile.gameObject.SetActive(false);
        IsAttacking = false;
    }

    private void OnDisable()
    {
        FinishAttack();
    }

    private void OnDestroy()
    {
        if (_projectile == null)
            return;

        _projectile.FlightStarted -= StartCooldown;
        _projectile.Finished -= FinishAttack;
        Destroy(_projectile.gameObject);
        _projectile = null;
    }
}
