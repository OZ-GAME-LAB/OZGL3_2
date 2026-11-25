using System;
using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class PlayerAttackProjectile : NetworkBehaviour
{
    private RuneCombatActor _attacker;
    private AttackProfile _profile;

    private float _attackPower;
    private Vector2 _direction;

    private float _speed;
    private float _remainingDistance;

    private Rigidbody2D _body;
    private CapsuleCollider2D _hitbox;
    private ContactFilter2D _hitFilter;

    private LayerMask _targetLayer;
    private LayerMask _wallLayer;

    private bool _initialized;

    private readonly List<RaycastHit2D> _castHits = new();

    private void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        _hitbox = GetComponent<CapsuleCollider2D>();

        _body.bodyType = RigidbodyType2D.Kinematic;
        _body.interpolation = RigidbodyInterpolation2D.None;
        _body.linearVelocity = Vector2.zero;
        _body.angularVelocity = 0f;

        _hitbox.isTrigger = true;
        _body.simulated = false;
    }

    // 서버가 Instantiate한 뒤 Spawn하기 전에 호출.
    public void InitOnServer(
        RuneCombatActor attacker,
        AttackProfile profile,
        float attackPower,
        Vector2 direction,
        float speed,
        float distance,
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

        _targetLayer = targetLayer;
        _wallLayer = wallLayer;

        transform.localScale *= profile._projectileSizeMultiple;

        float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;

        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        _hitFilter = new ContactFilter2D();
        _hitFilter.SetLayerMask(_targetLayer.value | _wallLayer.value);

        _hitFilter.useTriggers = true;

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

        _castHits.Clear();

        _hitbox.Cast(_direction, _hitFilter, _castHits, moveDistance);

        _castHits.Sort((a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit2D hit in _castHits)
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

            if (!hit.collider.isTrigger && IsInLayer(hit.collider.gameObject.layer, _wallLayer))
            {
                DespawnOnServer();
                return;
            }

            if (target is not MonsterRuneCombatActor monster || !monster.AcceptsWeaponContact(hit.collider, includeOtherTriggers: false))
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

        Vector2 nextPosition = _body.position + _direction * moveDistance;

        _body.MovePosition(nextPosition);

        _remainingDistance = Mathf.Max(0f, _remainingDistance - moveDistance);
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

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // 원격 클라이언트는 NetworkTransform으로 외형만 이동.
        _body.simulated = IsServer;
        _hitbox.enabled = IsServer;

        if (IsServer)
        {
            _body.position = transform.position;
            _body.rotation = transform.eulerAngles.z;

            // 초기 위치·회전·룬 크기 배율을 물리 검사에 반영.
            Physics2D.SyncTransforms();
        }
    }
}
