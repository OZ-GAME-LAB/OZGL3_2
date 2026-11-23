using System;
using Unity.Netcode;
using UnityEngine;

public partial class BossNetwork
{
    [Header("처치 연출")]
    [SerializeField] private BoxCollider2D _defeatRoomArea;
    [SerializeField, Min(0.1f)] private float _defeatSinkSeconds = 4f;
    private readonly NetworkVariable<double> _defeatStartTime = new(-1d);
    private readonly NetworkVariable<float> _defeatDuration = new(4f);
    private readonly NetworkVariable<bool> _deathConfirmed = new(false);
    private bool _deathStarted;
    private bool _deathProcessing;
    private bool _defeatHeld;
    private bool _deathRemovalRequested;
    private bool _deadAttacksStopped;
    private double _defeatHoldDeadline;

    public double DefeatStartTime => _defeatStartTime.Value;
    public float DefeatDuration => _defeatDuration.Value;
    public bool DeathConfirmed => _deathConfirmed.Value;

    private void ResetDefeatState()
    {
        _deathStarted = false;
        _deathProcessing = false;
        _defeatHeld = false;
        _deathRemovalRequested = false;
        _deadAttacksStopped = false;
        if (!IsServer) return;
        _deathConfirmed.Value = false;
        _defeatStartTime.Value = -1d;
    }

    public bool ContainsDefeatPosition(Vector3 worldPosition)
    {
        if (_defeatRoomArea == null) return false;
        Vector2 point = _defeatRoomArea.transform.InverseTransformPoint(worldPosition);
        point -= _defeatRoomArea.offset;
        Vector2 half = _defeatRoomArea.size * 0.5f;
        return Mathf.Abs(point.x) <= half.x && Mathf.Abs(point.y) <= half.y;
    }

    public bool HoldDefeatOnServer(float maximumSeconds)
    {
        if (!IsServer || !IsSpawned || !_deathProcessing || _defeatHeld) return false;
        _defeatHeld = true;
        _defeatHoldDeadline = Time.realtimeSinceStartupAsDouble + Mathf.Max(1f, maximumSeconds);
        return true;
    }

    public bool BeginDefeatSinkOnServer(double startTime)
    {
        if (!IsServer || !IsSpawned || !_defeatHeld || _defeatStartTime.Value >= 0d) return false;
        _defeatStartTime.Value = startTime;
        return true;
    }

    public void CompleteDefeatOnServer()
    {
        if (!IsServer || !IsSpawned) return;
        // A synchronous scenario callback must never remove the reward source mid-death.
        _deathRemovalRequested = true;
        if (!_deathProcessing) NetworkObject.Despawn();
    }

    private void HandleDeathOnServer()
    {
        if (_deathStarted) return;
        _deathStarted = true;
        _deathProcessing = true;
        _deathConfirmed.Value = true;
        _combatActive.Value = false;
        IsReady = false;
        if (_bt != null) _bt.enabled = false;
        _defeatDuration.Value = float.IsNaN(_defeatSinkSeconds) || float.IsInfinity(_defeatSinkSeconds)
            ? 4f : Mathf.Max(0.1f, _defeatSinkSeconds);
        try
        {
            try { StopDeadAttacks(); }
            catch (Exception exception) { Debug.LogException(exception, this); }
            if (DiedOnServer != null)
            {
                foreach (Action subscriber in DiedOnServer.GetInvocationList())
                {
                    try { subscriber(); }
                    catch (Exception exception) { Debug.LogException(exception, this); }
                }
            }
        }
        finally
        {
            try
            {
                int playerCount = NetworkManager.ConnectedClientsList.Count;
                DropRewardUtil.SpawnMonsterRewardsOnServer(_monsterData, transform.position, playerCount, this);
            }
            finally
            {
                _deathProcessing = false;
                if (IsSpawned && (!_defeatHeld || _deathRemovalRequested)) NetworkObject.Despawn();
            }
        }
    }

    private void StopDeadAttacks()
    {
        if (_deadAttacksStopped) return;
        _deadAttacksStopped = true;
        if (_boss != null)
            foreach (MonsterSkillBase skill in _boss.Skills)
                if (skill != null)
                {
                    try { skill.enabled = false; }
                    catch (Exception exception) { Debug.LogException(exception, this); }
                }
        DestroyThornVisuals();
        DestroyPoisonVisual();
        DestroyPoisonMistVisual();
    }

    protected override void Update()
    {
        base.Update();
        // Initial HP is also zero until stats arrive. Only server-confirmed death owns cleanup.
        if (!IsSpawned || !DeathConfirmed) return;
        StopDeadAttacks();
        if (IsServer && _defeatHeld && !_deathProcessing && Time.realtimeSinceStartupAsDouble >= _defeatHoldDeadline)
            CompleteDefeatOnServer();
    }
}
