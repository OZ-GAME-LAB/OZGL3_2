using Unity.Netcode;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public class BossPoisonMistSkill : MonsterSkillBase
{
    private const float TriggerHpRatio = 0.7f;

    private BossNetwork _ownerNetwork;
    private BossPoisonMistObject _mist;

    public bool HasActivated { get; private set; }

    public override bool InitSkill(Transform owner, float bodyHeight, float atk, GameObject attackPrefab)
    {
        if (_mist != null)
            return true;

        if (owner == null || !owner.TryGetComponent(out _ownerNetwork)
            || attackPrefab == null || !attackPrefab.TryGetComponent(out BossPoisonMistObject mistPrefab)
            || !mistPrefab.HasValidSettings())
        {
            return FailInitialization("맹독 안개 스킬의 BossNetwork와 안개 프리팹의 BossPoisonMistObject, Trigger Collider2D 설정을 확인하세요.");
        }

        _mist = Instantiate(mistPrefab, owner.position, Quaternion.identity);
        _mist.gameObject.SetActive(false);
        _mist.Finished += OnMistFinished;
        return true;
    }

    public override void UseSkill(Transform target, Vector3 attackPosition)
    {
        if (!isActiveAndEnabled || HasActivated || IsAttacking || _mist == null
            || !IsCooldownComplete || _ownerNetwork == null || !_ownerNetwork.IsServer
            || !_ownerNetwork.IsSpawned || !_ownerNetwork.IsReady || !_ownerNetwork.IsAlive)
            return;

        if (_ownerNetwork.CurrentHp >= _ownerNetwork.MaxHp * TriggerHpRatio)
            return;

        NetworkManager manager = _ownerNetwork.NetworkManager;
        if (manager == null || !manager.IsListening)
            return;

        HasActivated = true;
        StartCooldown();

        Vector3 position = _ownerNetwork.transform.position;
        double startTime = manager.ServerTime.Time;
        float durationSeconds = _mist.DurationSeconds;
        _mist.transform.SetPositionAndRotation(position, Quaternion.identity);
        int appliedCount = _mist.Play(_ownerNetwork, startTime, durationSeconds);
        IsAttacking = _mist.IsPlaying;
        _ownerNetwork.BroadcastPoisonMist(SkillId, position, startTime, durationSeconds);
        Debug.Log($"맹독 안개 발동: 보스 HP {_ownerNetwork.CurrentHp}/{_ownerNetwork.MaxHp}, 독 부여 {appliedCount}명", this);
    }

    private void OnMistFinished()
    {
        IsAttacking = false;
    }

    private void OnDisable()
    {
        if (_mist != null)
            _mist.Stop();
        IsAttacking = false;
    }

    private void OnDestroy()
    {
        if (_mist == null)
            return;

        _mist.Finished -= OnMistFinished;
        Destroy(_mist.gameObject);
        _mist = null;
    }
}
