using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class HealAreaObject : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float _lifetimeSeconds = 2f;
    [SerializeField, Range(0f, 1f)] private float _maxHpHealRatio = 0.5f;
    [SerializeField, Min(0)] private int _bossPoisonStacksToRemove = 1;

    private readonly HashSet<ulong> _healedPlayers = new();
    private Collider2D _trigger;
    private Rigidbody2D _body;
    private NetworkManager _networkManager;
    private double _expiresAt;
    private bool _healOnServer;

    public float LifetimeSeconds => _lifetimeSeconds;

    private void Awake()
    {
        _trigger = GetComponent<Collider2D>();
        _body = GetComponent<Rigidbody2D>();

        if (_body != null)
        {
            _body.bodyType = RigidbodyType2D.Kinematic;
            _body.useFullKinematicContacts = true;
            _body.sleepMode = RigidbodySleepMode2D.NeverSleep;
        }

        DisableHealing();
    }

    public bool HasValidSettings()
    {
        return TryGetComponent(out Collider2D trigger) && trigger.isTrigger
            && TryGetComponent<Rigidbody2D>(out _)
            && !float.IsNaN(_lifetimeSeconds) && !float.IsInfinity(_lifetimeSeconds) && _lifetimeSeconds > 0f
            && !float.IsNaN(_maxHpHealRatio) && _maxHpHealRatio >= 0f && _maxHpHealRatio <= 1f
            && _bossPoisonStacksToRemove >= 0;
    }

    public bool Play(NetworkManager manager, double expiresAt, bool healOnServer)
    {
        if (manager == null || !manager.IsListening || !HasValidSettings()
            || double.IsNaN(expiresAt) || double.IsInfinity(expiresAt)
            || expiresAt <= manager.ServerTime.Time || (healOnServer && !manager.IsServer))
            return false;

        _networkManager = manager;
        _expiresAt = expiresAt;
        _healOnServer = healOnServer;
        _healedPlayers.Clear();
        _trigger.enabled = healOnServer;
        _body.simulated = healOnServer;
        return true;
    }

    private void Update()
    {
        if (_networkManager == null || !_networkManager.IsListening
            || _networkManager.ServerTime.Time >= _expiresAt)
        {
            DisableHealing();
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHeal(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryHeal(other);
    }

    private void TryHeal(Collider2D other)
    {
        if (!_healOnServer || _networkManager == null || !_networkManager.IsListening
            || !_networkManager.IsServer || _networkManager.ServerTime.Time >= _expiresAt
            || other == null || other.isTrigger)
            return;

        PlayerNetworkStats player = other.GetComponentInParent<PlayerNetworkStats>();
        if (player == null || !player.IsServer || !player.IsAlive
            || !player.gameObject.activeInHierarchy || !_healedPlayers.Add(player.NetworkObjectId))
            return;

        player.HealOnServer(player.MaxHp * _maxHpHealRatio);
        if (_bossPoisonStacksToRemove > 0)
            player.RemoveBossPoisonStacksOnServer(_bossPoisonStacksToRemove);
    }

    private void DisableHealing()
    {
        _healOnServer = false;
        if (_trigger != null)
            _trigger.enabled = false;
        if (_body != null)
            _body.simulated = false;
    }

    private void OnDisable()
    {
        DisableHealing();
        _healedPlayers.Clear();
        _networkManager = null;
    }
}
