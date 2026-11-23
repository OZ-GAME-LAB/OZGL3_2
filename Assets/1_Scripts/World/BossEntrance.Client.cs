using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

public sealed partial class BossEntrance
{
    private uint _nextRequest, _localRequest;
    private bool _localActive, _localUIReady, _openingUI;
    private double _localDeadline;
    private int _latestAccepted, _latestTotal;
    private CancellationTokenSource _localCancellation;
    private PlayerControllerView _localPlayer;
    private BossEntryUI _entryUI;
    private UIManager _localUIManager;

    public override void BeginInteraction(Transform[] anchors, Camera camera, string equipmentId)
    {
        base.BeginInteraction(anchors, camera, equipmentId);
        if (_localActive) return;
        NetworkObject local = Network != null && Network.LocalClient != null ? Network.LocalClient.PlayerObject : null;
        UIManager ui = UIManager.Inst;
        if (!CanPrepare || ui == null || local == null ||
            !local.TryGetComponent(out _localPlayer) || !local.TryGetComponent(out PlayerNetworkStats stats) ||
            !stats.IsAlive || stats.IsEncounterLocked || stats.IsResting || !Contains(stats) ||
            ui.HasDialogueConflict() || Busy(ui, UIType.SettingUI) || Busy(ui, UIType.DialogueUI) ||
            Busy(ui, UIType.BossEntryUI) || Busy(ui, UIType.CinematicEffectUI) ||
            Busy(ui, UIType.RestUI) || Busy(ui, UIType.ScreenTransitionUI) || Busy(ui, UIType.GameOverUI))
        {
            _localPlayer = null;
            RequestInteractionEnd();
            return;
        }
        _localActive = true;
        _localUIReady = false;
        _localRequest = ++_nextRequest;
        if (_localRequest == 0) _localRequest = ++_nextRequest;
        _localDeadline = Time.realtimeSinceStartupAsDouble + _uiTimeout;
        _localUIManager = ui;
        _localCancellation = new CancellationTokenSource();
        _manager.RequestBossPreparation(_localRequest, true);
    }

    private static bool Busy(UIManager ui, UIType type) => ui.IsOpen(type) || ui.IsLoading(type);

    public void ReceivePreparation(uint request, int accepted, int total, bool ended, string message)
    {
        if (!_localActive || request != _localRequest) return;
        if (ended)
        {
            if (!string.IsNullOrEmpty(message)) Debug.LogWarning(message, this);
            EndLocal(false);
            return;
        }
        _latestAccepted = accepted;
        _latestTotal = total;
        if (_entryUI != null) _entryUI.SetState(accepted, total);
        else if (!_openingUI) OpenWaitingUIAsync(request, _localCancellation.Token).Forget();
    }

    private bool IsCurrent(uint request, CancellationToken token) =>
        _localActive && request == _localRequest && !token.IsCancellationRequested;

    private async UniTask OpenWaitingUIAsync(uint request, CancellationToken token)
    {
        _openingUI = true;
        try
        {
            UIBase view = await _localUIManager.OpenVeryFrontRootAsync(UIType.BossEntryUI, token);
            if (!IsCurrent(request, token)) return;
            _entryUI = view as BossEntryUI;
            if (_entryUI == null) throw new InvalidOperationException("BossEntryUI 프리팹 연결을 확인해 주세요.");
            _entryUI.transform.SetAsLastSibling();
            _entryUI.CancelRequested += CancelInteraction;
            _entryUI.SetState(_latestAccepted, _latestTotal);
            _localPlayer.SetBossEntryWaiting(true);
            _localUIReady = true;
            _localDeadline = double.PositiveInfinity;
            _manager.AcknowledgeBossPreparation(request, true);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            if (IsCurrent(request, token)) EndLocal(true);
        }
        finally
        {
            if (request == _localRequest) _openingUI = false;
        }
    }

    // 암전 명령 수신 시 대기 잠금과 상호작용을 먼저 종료합니다. 이후 보스전이 잠금을 소유합니다.
    public bool CompleteLocalPreparation()
    {
        if (!_localActive || !_localUIReady || _localPlayer == null) return false;
        EndLocal(false);
        return true;
    }

    private void UpdateLocalLifetime()
    {
        if (!_localActive) return;
        if (_manager == null || !_manager.IsSpawned || Network == null || !Network.IsListening ||
            _localPlayer == null || !_localPlayer.TryGetComponent(out PlayerNetworkStats stats) || !stats.IsAlive ||
            !Contains(stats) || Time.realtimeSinceStartupAsDouble >= _localDeadline) EndLocal(true);
    }

    public override void CancelInteraction() { EndLocal(true); }

    private void EndLocal(bool notifyServer)
    {
        if (!_localActive) return;
        uint request = _localRequest;
        _localActive = false; // RequestInteractionEnd에 의한 CancelInteraction 재진입 방지.
        _localUIReady = false;
        _openingUI = false;
        CancellationTokenSource cancellation = _localCancellation;
        _localCancellation = null;
        PlayerControllerView player = _localPlayer;
        _localPlayer = null;
        try
        {
            cancellation?.Cancel();
            if (_entryUI != null)
            {
                _entryUI.CancelRequested -= CancelInteraction;
                if (_localUIManager != null && _localUIManager.IsOpen(UIType.BossEntryUI))
                    _localUIManager.Close(UIType.BossEntryUI);
            }
        }
        finally
        {
            cancellation?.Dispose();
            _entryUI = null;
            player?.SetBossEntryWaiting(false);
            try
            {
                if (notifyServer && _manager != null && _manager.IsSpawned)
                    _manager.RequestBossPreparation(request, false);
            }
            finally { RequestInteractionEnd(); }
        }
    }
}
