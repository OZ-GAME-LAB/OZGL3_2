using System;
using System.Collections.Generic;
using UnityEngine;

public class BossPoisonProjectile : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _visual;
    [SerializeField, Min(0f)] private float _warningSeconds = 1f;
    [SerializeField, Min(0.01f)] private float _flightSeconds = 2f;
    [SerializeField] private LayerMask _groundLayers = 1 << 8;
    [SerializeField] private LayerMask _playerLayers = 1 << 6;
    [SerializeField] private LayerMask _platformLayers = 1 << 11;

    public bool IsPlaying { get; private set; }
    public float WarningSeconds => _warningSeconds;
    public float FlightSeconds => _flightSeconds;
    public event Action FlightStarted;
    public event Action Finished;

    private BossNetwork _owner;
    private Vector3 _start, _control, _end;
    private Vector3 _visualScale;
    private Color _visualColor;
    private double _startTime;
    private RuneDamageInfo _damageInfo;
    private float _previousFlightTime;
    private bool _serverAttack;
    private bool _flightStarted;
    private ContactFilter2D _hitFilter;
    private readonly List<RaycastHit2D> _hits = new();

    private void Awake()
    {
        if (_visual == null)
            return;

        _visualScale = _visual.transform.localScale;
        _visualColor = _visual.color;
    }

    public bool HasValidSettings()
    {
        return _visual != null && _visual.sprite != null
            && GetVisualRadius() > 0f
            && IsFinite(_warningSeconds) && _warningSeconds >= 0f
            && IsFinite(_flightSeconds) && _flightSeconds > 0f;
    }

    public void Play(BossNetwork owner, bool serverAttack, RuneDamageInfo damageInfo,
        Vector3 start, Vector3 control, Vector3 end, double startTime,
        float warningSeconds, float flightSeconds)
    {
        _warningSeconds = warningSeconds;
        _flightSeconds = flightSeconds;
        if (owner == null || !owner.IsSpawned || !HasValidSettings()
            || (serverAttack && (!owner.IsServer || !IsFinite(damageInfo.RawDamage) || damageInfo.RawDamage <= 0f)))
            return;

        _owner = owner;
        _serverAttack = serverAttack;
        _damageInfo = damageInfo;
        _start = start;
        _control = control;
        _end = end;
        _startTime = startTime;
        _previousFlightTime = 0f;
        _flightStarted = false;
        transform.position = start;

        _hits.Clear();
        _visual.enabled = true;
        _hitFilter = new ContactFilter2D();
        _hitFilter.SetLayerMask((_groundLayers.value | _playerLayers.value) & ~_platformLayers.value);
        _hitFilter.useTriggers = false;
        IsPlaying = true;
        UpdateVisual(0f);
    }

    private void Update()
    {
        if (!IsPlaying)
            return;

        if (_owner == null || !_owner.IsSpawned || _owner.NetworkManager == null
            || !_owner.NetworkManager.IsListening)
        {
            Finish();
            return;
        }

        float elapsed = Mathf.Max(0f, (float)(_owner.NetworkManager.ServerTime.Time - _startTime));
        if (elapsed < _warningSeconds)
        {
            UpdateVisual(_warningSeconds > 0f ? elapsed / _warningSeconds : 1f);
            return;
        }

        if (!_flightStarted)
        {
            _flightStarted = true;
            UpdateVisual(1f);
            if (_serverAttack)
                FlightStarted?.Invoke();
        }

        float flightTime = Mathf.Min(elapsed - _warningSeconds, _flightSeconds);
        if (_serverAttack)
        {
            while (_previousFlightTime < flightTime)
            {
                float nextTime = Mathf.Min(_previousFlightTime + 0.02f, flightTime);
                Vector3 next = BossPoisonPath.Evaluate(_start, _control, _end, nextTime / _flightSeconds);
                if (TryHit(transform.position, next))
                {
                    Finish();
                    return;
                }
                transform.position = next;
                _previousFlightTime = nextTime;
            }
        }
        else
        {
            transform.position = BossPoisonPath.Evaluate(_start, _control, _end, flightTime / _flightSeconds);
        }

        if (flightTime >= _flightSeconds)
            Finish();
    }

    private void UpdateVisual(float progress)
    {
        _visual.transform.localScale = _visualScale * Mathf.Lerp(0.35f, 1f, progress);
        Color color = _visualColor;
        color.a *= Mathf.Lerp(0.3f, 1f, progress);
        _visual.color = color;
    }

    private bool TryHit(Vector3 from, Vector3 to)
    {
        Vector2 delta = to - from;
        float distance = delta.magnitude;
        if (distance <= 0f)
            return false;

        float radius = GetVisualRadius();
        if (radius <= 0f)
            return false;

        Vector2 visualOffset = GetVisualCenter() - transform.position;
        _hits.Clear();
        Physics2D.CircleCast((Vector2)from + visualOffset, radius, delta / distance, _hitFilter, _hits, distance);
        float nearestDistance = float.PositiveInfinity;
        PlayerRuneCombatActor nearestPlayer = null;
        bool foundHit = false;

        foreach (RaycastHit2D hit in _hits)
        {
            if (hit.collider == null || hit.distance >= nearestDistance)
                continue;

            PlayerRuneCombatActor player = hit.collider.GetComponentInParent<PlayerRuneCombatActor>();
            bool isGround = (_groundLayers.value & (1 << hit.collider.gameObject.layer)) != 0;
            if (player != null ? !player.IsAlive : !isGround)
                continue;

            nearestDistance = hit.distance;
            nearestPlayer = player;
            foundHit = true;
        }

        if (!foundHit)
            return false;

        transform.position = from + (Vector3)(delta / distance * nearestDistance);
        if (nearestPlayer != null)
        {
            RuneDamageResult result = nearestPlayer.ReceiveDirectDamageOnServer(_damageInfo);
            if (result.Accepted)
                Debug.Log($"독액 적중: {nearestPlayer.name}, 실제 피해: {result.ActualDamage}", nearestPlayer);
        }

        return true;
    }

    private void Finish()
    {
        if (!IsPlaying)
            return;
        IsPlaying = false;
        gameObject.SetActive(false);
        Finished?.Invoke();
    }

    private void OnDisable()
    {
        IsPlaying = false;
        _owner = null;
        _hits.Clear();

        if (_visual == null)
            return;

        _visual.transform.localScale = _visualScale;
        _visual.color = _visualColor;
        _visual.enabled = false;
    }

    private float GetVisualRadius()
    {
        if (_visual == null || _visual.sprite == null)
            return 0f;

        Vector3 size = _visual.sprite.bounds.size;
        Vector3 scale = _visual.transform.lossyScale;
        float width = Mathf.Abs(size.x * scale.x);
        float height = Mathf.Abs(size.y * scale.y);
        if (!IsFinite(width) || !IsFinite(height) || width <= 0f || height <= 0f)
            return 0f;

        return Mathf.Max(width, height) * 0.5f;
    }

    private Vector3 GetVisualCenter()
    {
        Vector3 center = _visual.sprite.bounds.center;
        if (_visual.flipX)
            center.x = -center.x;
        if (_visual.flipY)
            center.y = -center.y;
        return _visual.transform.TransformPoint(center);
    }

    private void OnDrawGizmosSelected()
    {
        float radius = GetVisualRadius();
        if (radius <= 0f)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(GetVisualCenter(), radius);
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
