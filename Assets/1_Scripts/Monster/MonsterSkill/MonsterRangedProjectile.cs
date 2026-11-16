using System.Collections.Generic;
using UnityEngine;

public class MonsterRangedProjectile : MonsterTimedAttackObject
{
    [SerializeField] private SpriteRenderer _visual;
    [SerializeField, Min(0.01f)] private float _speed = 10f;
    [SerializeField, Min(0.01f)] private float _flightSeconds = 3f;
    [SerializeField, Min(0f)] private float _windupSeconds;
    [SerializeField] private LayerMask _obstacleLayers = 1 << 8;
    [SerializeField] private LayerMask _playerLayers = 1 << 6;
    [SerializeField] private LayerMask _platformLayers = 1 << 11;

    private readonly List<RaycastHit2D> _hits = new();
    private ContactFilter2D _hitFilter;
    public override float DurationSeconds => _flightSeconds;
    public float WindupSeconds => _windupSeconds;

    public override bool HasValidSettings()
    {
        return _visual != null && _visual.sprite != null
            && IsFinite(_speed) && _speed > 0f
            && IsFinite(_flightSeconds) && _flightSeconds > 0f
            && IsFinite(_windupSeconds) && _windupSeconds >= 0f;
    }

    protected override void OnPlay()
    {
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg);
        _hitFilter = new ContactFilter2D { useTriggers = false };
        _hitFilter.SetLayerMask((_obstacleLayers.value | _playerLayers.value) & ~_platformLayers.value);
    }

    protected override void Tick(float elapsed, float previousElapsed)
    {
        Vector3 from = Origin + (Vector3)(Direction * (_speed * previousElapsed));
        Vector3 to = Origin + (Vector3)(Direction * (_speed * elapsed));
        if (Owner.IsServer && TryHit(from, to))
        {
            Stop();
            return;
        }

        transform.position = to;
    }

    private bool TryHit(Vector3 from, Vector3 to)
    {
        float distance = Vector3.Distance(from, to);
        Vector2 visualOffset = _visual.bounds.center - transform.position;
        float radius = Mathf.Max(_visual.bounds.extents.x, _visual.bounds.extents.y);
        _hits.Clear();
        Physics2D.CircleCast((Vector2)from + visualOffset, radius, Direction, _hitFilter, _hits, distance);

        RaycastHit2D nearest = default;
        PlayerRuneCombatActor victim = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (RaycastHit2D hit in _hits)
        {
            if (hit.collider == null || hit.distance >= nearestDistance)
                continue;

            PlayerRuneCombatActor player = hit.collider.GetComponentInParent<PlayerRuneCombatActor>();
            bool obstacle = (_obstacleLayers.value & (1 << hit.collider.gameObject.layer)) != 0;
            if (player != null ? !player.IsAlive : !obstacle)
                continue;

            nearest = hit;
            nearestDistance = hit.distance;
            victim = player;
        }

        if (nearest.collider == null)
            return false;

        transform.position = from + (Vector3)(Direction * nearestDistance);
        if (victim != null)
        {
            RuneDamageInfo hitInfo = DamageInfo;
            hitInfo.HitDirection = Direction;
            victim.ReceiveDirectDamageOnServer(hitInfo);
        }

        return true;
    }

    private void OnDrawGizmosSelected()
    {
        if (_visual == null)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_visual.bounds.center, Mathf.Max(_visual.bounds.extents.x, _visual.bounds.extents.y));
    }
}
