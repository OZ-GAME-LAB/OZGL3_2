using System;
using System.Collections.Generic;
using UnityEngine;

public class BossPoisonMistObject : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float _durationSeconds = 2f;

    private readonly List<Collider2D> _overlaps = new();
    private readonly HashSet<PlayerNetworkStats> _affectedPlayers = new();
    private Collider2D _areaCollider;
    private BossNetwork _owner;
    private double _expiresAt;

    public float DurationSeconds => _durationSeconds;
    public bool IsPlaying { get; private set; }
    public event Action Finished;

    private void Awake()
    {
        _areaCollider = GetComponent<Collider2D>();
        if (_areaCollider != null)
            _areaCollider.enabled = false;
    }

    public bool HasValidSettings()
    {
        return TryGetComponent(out Collider2D area) && area.isTrigger
            && !float.IsNaN(_durationSeconds) && !float.IsInfinity(_durationSeconds) && _durationSeconds > 0f;
    }

    public int Play(BossNetwork owner, double startTime, float durationSeconds)
    {
        if (IsPlaying)
            return 0;

        if (!HasValidSettings() || owner == null || !owner.IsSpawned || !owner.IsAlive
            || owner.NetworkManager == null || !owner.NetworkManager.IsListening
            || double.IsNaN(startTime) || double.IsInfinity(startTime)
            || float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds) || durationSeconds <= 0f)
        {
            Stop();
            return 0;
        }

        _expiresAt = startTime + durationSeconds;
        if (owner.NetworkManager.ServerTime.Time >= _expiresAt)
        {
            Stop();
            return 0;
        }

        _owner = owner;
        IsPlaying = true;
        gameObject.SetActive(true);
        _areaCollider = GetComponent<Collider2D>();
        _areaCollider.enabled = owner.IsServer;

        if (!owner.IsServer)
            return 0;

        return ApplyPoisonOnServer();
    }

    private int ApplyPoisonOnServer()
    {
        _overlaps.Clear();
        _affectedPlayers.Clear();

        try
        {
            Physics2D.SyncTransforms();
            _areaCollider.Overlap(new ContactFilter2D { useTriggers = false }, _overlaps);

            int appliedCount = 0;
            foreach (Collider2D other in _overlaps)
            {
                if (other == null || !other.isActiveAndEnabled || other.isTrigger)
                    continue;

                PlayerNetworkStats player = other.GetComponentInParent<PlayerNetworkStats>();
                if (player == null || !player.IsServer || !player.IsSpawned
                    || !player.isActiveAndEnabled || !player.IsAlive || !_affectedPlayers.Add(player))
                    continue;

                if (player.ApplyBossPoisonOnServer())
                    appliedCount++;
            }

            return appliedCount;
        }
        finally
        {
            _areaCollider.enabled = false;
            _overlaps.Clear();
            _affectedPlayers.Clear();
        }
    }

    private void Update()
    {
        if (!IsPlaying)
            return;

        if (_owner == null || !_owner.IsSpawned || !_owner.IsAlive
            || _owner.NetworkManager == null || !_owner.NetworkManager.IsListening
            || _owner.NetworkManager.ServerTime.Time >= _expiresAt)
            Stop();
    }

    public void Stop()
    {
        bool wasPlaying = IsPlaying;
        IsPlaying = false;
        _owner = null;
        if (_areaCollider != null)
            _areaCollider.enabled = false;
        gameObject.SetActive(false);
        if (wasPlaying)
            Finished?.Invoke();
    }

    private void OnDisable()
    {
        Stop();
    }
}
