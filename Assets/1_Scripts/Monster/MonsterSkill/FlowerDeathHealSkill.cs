using Unity.Netcode;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public class FlowerDeathHealSkill : MonsterSkillBase
{
    private MonsterNetwork _ownerNetwork;
    private HealAreaObject _healPrefab;
    private bool _used;

    public override bool IsDeathSkill => true;

    public override bool InitSkill(Transform owner, float bodyHeight, float atk, GameObject attackPrefab)
    {
        _ownerNetwork = null;
        _healPrefab = null;
        _used = false;

        if (owner == null || !owner.TryGetComponent(out _ownerNetwork))
        {
            return FailInitialization("꽃 회복 스킬의 소유 오브젝트에 MonsterNetwork가 필요합니다.");
        }

        if (attackPrefab == null || !attackPrefab.TryGetComponent(out HealAreaObject healPrefab)
            || !healPrefab.HasValidSettings())
        {
            return FailInitialization("회복 프리팹의 HealAreaObject, Trigger, Rigidbody2D와 회복 설정을 확인하세요.");
        }

        _healPrefab = healPrefab;
        return true;
    }

    public override void UseSkill(Transform target, Vector3 attackPosition)
    {
        if (!isActiveAndEnabled || _used || _ownerNetwork == null || _healPrefab == null)
            return;

        if (!_ownerNetwork.IsServer || !_ownerNetwork.IsSpawned || _ownerNetwork.IsAlive)
            return;

        NetworkManager networkManager = _ownerNetwork.NetworkManager;

        if (networkManager == null || !networkManager.IsListening)
            return;

        _used = true;
        Vector3 position = _ownerNetwork.transform.position;
        double expiresAt = networkManager.ServerTime.Time + _healPrefab.LifetimeSeconds;
        HealAreaObject area = Instantiate(_healPrefab, position, Quaternion.identity);
        area.gameObject.SetActive(true);
        if (!area.Play(networkManager, expiresAt, true))
        {
            Destroy(area.gameObject);
            return;
        }

        _ownerNetwork.BroadcastHealArea(SkillId, position, expiresAt);
    }
}
