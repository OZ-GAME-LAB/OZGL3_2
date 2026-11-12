using Unity.Netcode;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public class FlowerDeathHealSkill : MonsterSkillBase
{
    [SerializeField, Min(0.01f)] private float _healRadius = 3f;

    private MonsterNetwork _ownerNetwork;
    private bool _used;

    public override bool IsDeathSkill => true;
    public override bool RequiresAttackPrefab => false;

    public override void InitSkill(Transform owner, float bodyHeight, float atk, GameObject attackPrefab)
    {
        _ownerNetwork = null;
        _used = false;

        if (owner == null || !owner.TryGetComponent(out _ownerNetwork))
        {
            Debug.LogError("꽃 회복 스킬의 소유 오브젝트에 MonsterNetwork가 필요합니다.", this);
        }
    }

    public override void UseSkill(Transform target, Vector3 attackPosition)
    {
        if (!isActiveAndEnabled || _used || _ownerNetwork == null)
            return;

        if (!_ownerNetwork.IsServer || !_ownerNetwork.IsSpawned || _ownerNetwork.IsAlive)
            return;

        if (float.IsNaN(_healRadius) || float.IsInfinity(_healRadius) || _healRadius <= 0f)
        {
            Debug.LogError("꽃 회복 반경은 0보다 큰 유한한 값이어야 합니다.", this);
            return;
        }

        NetworkManager networkManager = _ownerNetwork.NetworkManager;

        if (networkManager == null || !networkManager.IsListening)
            return;

        _used = true;
        Vector2 center = _ownerNetwork.transform.position;
        float radiusSquared = _healRadius * _healRadius;

        foreach (NetworkClient client in networkManager.ConnectedClientsList)
        {
            NetworkObject playerObject = client.PlayerObject;

            if (playerObject == null || !playerObject.IsSpawned || !playerObject.gameObject.activeInHierarchy)
                continue;

            if (!playerObject.TryGetComponent(out PlayerNetworkStats stats) || !stats.IsAlive)
                continue;

            Vector2 offset = (Vector2)playerObject.transform.position - center;

            if (offset.sqrMagnitude > radiusSquared)
                continue;

            stats.HealOnServer(stats.MaxHp * 0.5f);
        }
    }
}
