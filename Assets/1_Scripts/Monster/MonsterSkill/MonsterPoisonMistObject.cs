using System.Collections.Generic;
using UnityEngine;

public class MonsterPoisonMistObject : MonsterTimedAttackObject
{
    [SerializeField, Min(0.01f)] private float _durationSeconds = 2f;
    private Collider2D _area;
    private readonly List<Collider2D> _overlaps = new();
    private readonly Dictionary<PlayerRuneCombatActor, float> _nextHitTimes = new();
    public override float DurationSeconds => _durationSeconds;

    public override bool HasValidSettings()
    {
        return TryGetComponent(out Collider2D area) && area.isTrigger
            && IsFinite(_durationSeconds) && _durationSeconds > 0f;
    }

    protected override void OnPlay()
    {
        _area = GetComponent<Collider2D>();
        _area.enabled = Owner.IsServer;
        _nextHitTimes.Clear();
        if (Owner.IsServer)
            Physics2D.SyncTransforms();
    }

    protected override void Tick(float elapsed, float previousElapsed)
    {
        if (!Owner.IsServer || elapsed >= Lifetime)
            return;

        _overlaps.Clear();
        _area.Overlap(new ContactFilter2D { useTriggers = false }, _overlaps);
        foreach (Collider2D other in _overlaps)
        {
            PlayerRuneCombatActor player = other.GetComponentInParent<PlayerRuneCombatActor>();
            if (player == null || !player.IsAlive || !player.IsSpawned
                || (_nextHitTimes.TryGetValue(player, out float nextHit) && elapsed < nextHit))
                continue;

            _nextHitTimes[player] = elapsed + 1f;
            RuneDamageInfo hitInfo = DamageInfo;
            hitInfo.HitDirection = (Vector2)(other.bounds.center - transform.position);
            player.ReceiveDirectDamageOnServer(hitInfo);
        }
    }

    protected override void OnStopped()
    {
        if (_area != null)
            _area.enabled = false;
        _overlaps.Clear();
        _nextHitTimes.Clear();
    }
}
