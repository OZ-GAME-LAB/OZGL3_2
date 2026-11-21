using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

public sealed partial class RestTent
{
    private uint _nextRequest, _localRequest, _localRun;
    private RestStep _localPhase;
    private bool _localActive, _localUIReady;
    private double _localDeadline;
    private CancellationTokenSource _localCancellation;
    private PlayerControllerView _localPlayer;
    private RestUI _restUI;
    private ScreenTransitionUI _fadeUI;
    private UIManager _localUIManager;

    public override void BeginInteraction(Transform[] anchors, Camera camera, string equipmentId)
    {
        base.BeginInteraction(anchors, camera, equipmentId);
        if (_localActive) return;
        NetworkObject local = Network != null && Network.LocalClient != null ? Network.LocalClient.PlayerObject : null;
        UIManager ui = UIManager.Inst;
        if (_manager == null || !_manager.IsReady || ui == null || local == null ||
            !local.TryGetComponent(out _localPlayer) || !local.TryGetComponent(out PlayerNetworkStats stats) ||
            !stats.IsAlive || stats.IsEncounterLocked || !BossAllowsRest() ||
            ui.HasDialogueConflict() || Busy(ui, UIType.DialogueUI) || Busy(ui, UIType.SettingUI) ||
            Busy(ui, UIType.BossEntryUI) || Busy(ui, UIType.CinematicEffectUI) ||
            Busy(ui, UIType.GameOverUI) || Busy(ui, UIType.RestUI) || Busy(ui, UIType.ScreenTransitionUI))
        {
            _localPlayer = null;
            RequestInteractionEnd();
            return;
        }
        _localActive = true;
        _localUIReady = false;
        _localRequest = ++_nextRequest;
        if (_localRequest == 0) _localRequest = ++_nextRequest;
        _localRun = 0;
        _localPhase = RestStep.Waiting;
        _localDeadline = Time.realtimeSinceStartupAsDouble + _stepTimeout;
        _localUIManager = ui;
        _localCancellation = new CancellationTokenSource();
        _manager.RequestRest(_localRequest, true);
    }

    private static bool Busy(UIManager manager, UIType type) => manager.IsOpen(type) || manager.IsLoading(type);

    public void ReceiveRestStep(uint request, uint run, RestStep phase, int accepted, int total, ulong[] resources, string message)
    {
        if (!_localActive || request != _localRequest || run < _localRun) return;
        if (phase == RestStep.Ended)
        {
            if (!string.IsNullOrEmpty(message)) Debug.LogWarning(message, this);
            EndLocal(false);
            return;
        }
        if (phase < _localPhase) return;
        _localRun = run;
        if (phase == RestStep.Waiting)
        {
            if (_restUI != null) _restUI.SetState(accepted, total, true);
            else if (!_openingRestUI) OpenRestUIAsync(request, run, accepted, total, _localCancellation.Token).Forget();
            _latestAccepted = accepted;
            _latestTotal = total;
            return;
        }
        if (phase == _localPhase) return;
        _localPhase = phase;
        _localDeadline = Time.realtimeSinceStartupAsDouble + _stepTimeout + 5d;
        _restUI?.SetState(accepted, total, false);
        RunRestStepAsync(request, run, phase, resources, _localCancellation.Token).Forget();
    }

    private bool _openingRestUI;
    private int _latestAccepted, _latestTotal;

    private bool IsCurrent(uint request, uint run, CancellationToken token) =>
        _localActive && request == _localRequest && run == _localRun && !token.IsCancellationRequested;

    private async UniTask OpenRestUIAsync(uint request, uint run, int accepted, int total, CancellationToken token)
    {
        _openingRestUI = true;
        _latestAccepted = accepted;
        _latestTotal = total;
        try
        {
            UIBase view = await _localUIManager.OpenVeryFrontRootAsync(UIType.RestUI, token);
            if (!IsCurrent(request, run, token)) return;
            _restUI = view as RestUI;
            if (_restUI == null) throw new InvalidOperationException("RestUI 프리팹 연결을 확인해 주세요.");
            _restUI.transform.SetAsLastSibling();
            _restUI.CancelRequested += CancelInteraction;
            _restUI.SetState(_latestAccepted, _latestTotal, true);
            _localPlayer.SetRestLocalState(true);
            _localUIReady = true;
            _localDeadline = double.PositiveInfinity;
            _manager.AcknowledgeRest(request, run, RestStep.Waiting, true);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            if (IsCurrent(request, run, token)) EndLocal(true);
        }
        finally
        {
            if (request == _localRequest) _openingRestUI = false;
        }
    }

    private async UniTask RunRestStepAsync(uint request, uint run, RestStep phase, ulong[] resources, CancellationToken token)
    {
        try
        {
            if (!_localUIReady) throw new InvalidOperationException("휴식 UI 준비 전에 연출을 시작할 수 없습니다.");
            if (phase == RestStep.Fading)
            {
                if (Busy(_localUIManager, UIType.ScreenTransitionUI)) throw new InvalidOperationException("다른 화면 전환이 진행 중입니다.");
                UIBase view = await _localUIManager.OpenVeryFrontRootAsync(UIType.ScreenTransitionUI, token);
                if (!IsCurrent(request, run, token)) return;
                _fadeUI = view as ScreenTransitionUI;
                if (_fadeUI == null) throw new InvalidOperationException("화면 전환 UI를 준비하지 못했습니다.");
                _fadeUI.transform.SetAsLastSibling();
                await _fadeUI.FadeAsync(true, token);
            }
            else if (phase == RestStep.Restoring)
            {
                while (!LocalResourcesReady(resources)) await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            else if (phase == RestStep.Releasing)
            {
                if (_fadeUI == null) throw new InvalidOperationException("휴식 암전 화면이 없습니다.");
                await _fadeUI.FadeAsync(false, token);
            }
            if (IsCurrent(request, run, token) && phase == _localPhase)
                _manager.AcknowledgeRest(request, run, phase, true);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            if (IsCurrent(request, run, token)) EndLocal(true);
        }
    }

    private bool LocalResourcesReady(ulong[] ids)
    {
        if (Network == null || !Network.IsListening) return false;
        foreach (ulong id in ids)
            if (!Network.SpawnManager.SpawnedObjects.TryGetValue(id, out NetworkObject obj) ||
                !obj.TryGetComponent(out InteractionNetwork resource) || !resource.IsReady || resource.IsDormant) return false;
        return true;
    }

    private void UpdateLocalLifetime()
    {
        if (!_localActive) return;
        if (_manager == null || !_manager.IsSpawned || Network == null || !Network.IsListening ||
            _localPlayer == null || !_localPlayer.TryGetComponent(out PlayerNetworkStats stats) || !stats.IsAlive ||
            Time.realtimeSinceStartupAsDouble >= _localDeadline) EndLocal(true);
    }

    public override void CancelInteraction() { EndLocal(true); }

    public void CancelLocalForPlayer(PlayerControllerView player)
    {
        if (_localActive && _localPlayer == player) EndLocal(true);
    }

    private void EndLocal(bool notifyServer)
    {
        if (!_localActive) return;
        uint request = _localRequest;
        _localActive = false; // 기존 상호작용 종료 이벤트의 CancelInteraction 재진입을 먼저 차단합니다.
        _localUIReady = false;
        _openingRestUI = false;
        CancellationTokenSource cancellation = _localCancellation;
        _localCancellation = null;
        PlayerControllerView player = _localPlayer;
        _localPlayer = null;
        try
        {
            try { cancellation?.Cancel(); }
            catch (Exception exception) { Debug.LogException(exception, this); }
            if (_restUI != null) _restUI.CancelRequested -= CancelInteraction;
            CloseOwnedUI(_restUI, UIType.RestUI);
            CloseOwnedUI(_fadeUI, UIType.ScreenTransitionUI);
        }
        finally
        {
            cancellation?.Dispose();
            _restUI = null;
            _fadeUI = null;
            player?.SetRestLocalState(false);
            try { if (notifyServer && _manager != null && _manager.IsSpawned) _manager.RequestRest(request, false); }
            catch (Exception exception) { Debug.LogException(exception, this); }
            finally
            {
                try { RequestInteractionEnd(); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
        }
    }

    private void CloseOwnedUI(UIBase view, UIType type)
    {
        if (view == null || _localUIManager == null || !_localUIManager.IsOpen(type)) return;
        try { _localUIManager.Close(type); }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            // 닫기 콜백이 실패해도 이 흐름이 획득한 암전 화면은 남기지 않습니다.
            if (view != null) view.gameObject.SetActive(false);
        }
    }
}
