using System;
using Unity.Netcode;
using UnityEngine;

public class PlayerAttackProjectile : NetworkBehaviour
{
    private RuneCombatActor _attacker;
    private AttackProfile _profile;

    private float _attackPower;
    private Vector2 _direction;

    private float _speed;
    private float _remainingDistance;
    private float _radius;

    private LayerMask _targetLayer;
    private LayerMask _wallLayer;

    private bool _initialized;

    // 서버가 Instantiate한 뒤 Spawn하기 전에 호출.
    public void InitOnServer(
        RuneCombatActor attacker,
        AttackProfile profile,
        float attackPower,
        Vector2 direction,
        float speed,
        float distance,
        float radius,
        LayerMask targetLayer,
        LayerMask wallLayer)
    {
        if (attacker == null || attacker.IsServer == false|| profile == null)
        {
            return;
        }

        _attacker = attacker;
        _profile = profile;
        _attackPower = attackPower;
        _direction = direction.normalized;

        _speed = speed;
        _remainingDistance = distance;
        _radius = radius;

        _targetLayer = targetLayer;
        _wallLayer = wallLayer;

        transform.localScale *= profile._projectileSizeMultiple;

        float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;

        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        _initialized = true;
    }

    private void FixedUpdate()
    {
        if (IsSpawned == false || IsServer == false)
        {
            return;
        }

        if (_initialized == false || _attacker == null || _attacker.IsSpawned == false)
        {
            DespawnOnServer();
            return;
        }

        float moveDistance = Mathf.Min(_speed * Time.fixedDeltaTime, _remainingDistance);

        if (moveDistance <= 0f)
        {
            DespawnOnServer();
            return;
        }

        Vector2 origin = transform.position;
        int mask = _targetLayer.value | _wallLayer.value;

        RaycastHit2D[] hits = Physics2D.CircleCastAll(origin, _radius, _direction, moveDistance, mask);

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null)
            {
                continue;
            }

            RuneCombatActor target = hit.collider.GetComponentInParent<RuneCombatActor>();

            // 자기 자신과는 충돌하지 않음.
            if (target == _attacker)
            {
                continue;
            }

            if (IsInLayer(hit.collider.gameObject.layer, _wallLayer))
            {
                DespawnOnServer();
                return;
            }

            if (target == null)
            {
                continue;
            }

            bool accepted = RuneHitResolver.TryHitOnServer(_attacker, target, _profile, _attackPower);

            if (accepted)
            {
                DespawnOnServer();
                return;
            }
        }

        Vector2 nextPosition = origin + _direction * moveDistance;

        Vector3 position = transform.position;
        position.x = nextPosition.x;
        position.y = nextPosition.y;
        transform.position = position;

        _remainingDistance -= moveDistance;

        if (_remainingDistance <= 0f)
        {
            DespawnOnServer();
        }
    }

    private static bool IsInLayer(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private void DespawnOnServer()
    {
        if (IsServer && IsSpawned)
        {
            NetworkObject.Despawn(destroy: true);
        }
    }
}
