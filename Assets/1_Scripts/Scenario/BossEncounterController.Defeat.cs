using System;
using Unity.Netcode;
using UnityEngine;

public sealed partial class BossEncounterController
{
    private bool _defeatStarted;
    private double _defeatEndsAt;

    private static bool IsDefeatPhase(BossEncounterPhase phase)
    {
        return phase == BossEncounterPhase.DefeatFraming || phase == BossEncounterPhase.DefeatSinking;
    }

    private void BeginDefeatOnServer()
    {
        if (_defeatStarted || _encounterBoss == null) return;
        _defeatStarted = true;
        // 처치 연출 대상과 전투 참가자는 다를 수 있으므로 전투 명단을 사용할 때 정화합니다.
        foreach (ulong id in _participants)
            if (TryGetPlayer(id, out PlayerNetworkStats fighter)) fighter.ClearBossPoisonOnServer();
        ++_run;
        _manager.SetBossPhaseOnServer(BossEncounterPhase.DefeatFraming);
        _encounterBoss.HoldDefeatOnServer(_stepTimeout * 2f + _encounterBoss.DefeatDuration + 10f);
        UnlockParticipants();
        _participants.Clear();
        _acknowledged.Clear();
        _destinations.Clear();
        _deadline = Time.realtimeSinceStartupAsDouble + _stepTimeout;
        try
        {
            if (!_manager.CompleteWorldEventOnServer(_defeatedEventId))
                Debug.LogError("보스 처치 이벤트 기록 실패: " + _defeatedEventId, this);
        }
        catch (Exception exception) { Debug.LogException(exception, this); }

        foreach (NetworkClient client in Network.ConnectedClientsList)
        {
            if (client.PlayerObject != null && client.PlayerObject.TryGetComponent(out PlayerNetworkStats player)
                && _encounterBoss.ContainsDefeatPosition(player.transform.position))
                _participants.Add(client.ClientId);
        }
        // Complete the snapshot and lock every target before the host can acknowledge synchronously.
        foreach (ulong id in _participants)
            if (TryGetPlayer(id, out PlayerNetworkStats player)) LockOnServer(player, true);
        foreach (ulong id in _participants)
            _manager.SendBossStepOnServer(id, _run, BossEncounterPhase.DefeatFraming, Vector3.zero);
    }

    private void UpdateDefeatOnServer()
    {
        if (_encounterBoss == null || !_encounterBoss.IsSpawned || _participants.Count == 0)
        {
            FinishDefeatOnServer();
            return;
        }
        bool answered = true;
        foreach (ulong id in _participants)
            if (Network.ConnectedClients.ContainsKey(id) && !_acknowledged.Contains(id)) answered = false;
        bool expired = Time.realtimeSinceStartupAsDouble >= _deadline;
        if (Phase == BossEncounterPhase.DefeatFraming && (answered || expired))
        {
            double start = Network.ServerTime.Time + 0.25d;
            _defeatEndsAt = start + _encounterBoss.DefeatDuration;
            if (!_encounterBoss.BeginDefeatSinkOnServer(start)) { FinishDefeatOnServer(); return; }
            _acknowledged.Clear();
            _manager.SetBossPhaseOnServer(BossEncounterPhase.DefeatSinking);
            _deadline = Time.realtimeSinceStartupAsDouble + _encounterBoss.DefeatDuration + _stepTimeout;
            foreach (ulong id in _participants)
                _manager.SendBossStepOnServer(id, _run, BossEncounterPhase.DefeatSinking, Vector3.zero);
        }
        else if (Phase == BossEncounterPhase.DefeatSinking &&
            ((answered && Network.ServerTime.Time >= _defeatEndsAt) || expired))
            FinishDefeatOnServer();
    }

    private void FinishDefeatOnServer()
    {
        _manager.SetBossPhaseOnServer(BossEncounterPhase.Completed);
        UnlockParticipants();
        _participants.Clear();
        _acknowledged.Clear();
        StopEncounterBoss();
    }
}
