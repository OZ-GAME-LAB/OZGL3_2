using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

public sealed partial class BossEncounterController
{
    private uint _localRun;
    private BossEncounterPhase _localPhase;
    private CancellationTokenSource _localStep;
    private PlayerControllerView _localPlayer;
    private BossEntryUI _entryUI;
    private ScreenTransitionUI _transitionUI;
    private CinematicEffectUI _cinematicUI;
    private float _localDeadline;
    private bool _localActive;

    public void ReceiveStep(uint run, BossEncounterPhase phase, Vector3 destination)
    {
        if (run < _localRun || (run == _localRun && _localActive && phase == _localPhase)) return;
        if (run == _localRun && (_localPhase == BossEncounterPhase.Cancelled ||
            (phase == BossEncounterPhase.Prompt && _localPhase != BossEncounterPhase.Prompt))) return;
        CancelLocalStep();
        if (run != _localRun) CleanupLocal();
        _localRun = run;
        _localPhase = phase;
        if (phase == BossEncounterPhase.Cancelled)
        {
            _nextLocalPrompt = Time.unscaledTime + 2f;
            CleanupLocal();
            return;
        }
        _localActive = true;
        _localDeadline = Time.unscaledTime + (phase == BossEncounterPhase.Prompt ? _choiceTimeout : _stepTimeout) + 5f;
        _localStep = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        RunLocalStepAsync(run, phase, destination, _localStep.Token).Forget();
    }

    private async UniTask RunLocalStepAsync(uint run, BossEncounterPhase phase, Vector3 destination, CancellationToken token)
    {
        try
        {
            NetworkObject local = Network != null && Network.LocalClient != null ? Network.LocalClient.PlayerObject : null;
            if (local == null || !local.TryGetComponent(out _localPlayer)) throw new InvalidOperationException("보스전 로컬 플레이어가 없습니다.");
            if (phase != BossEncounterPhase.Recovering && UIManager.Inst == null) throw new InvalidOperationException("UIManager가 준비되지 않았습니다.");
            if (phase == BossEncounterPhase.Prompt)
            {
                _localPlayer.SetEncounterControlLocked(true);
                _entryUI = await OpenUIAsync<BossEntryUI>(UIType.BossEntryUI, token);
                _entryUI.transform.SetAsLastSibling();
                _entryUI.Chosen -= OnEntryChosen;
                _entryUI.Chosen += OnEntryChosen;
                return;
            }
            CloseEntryUI();
            if (phase == BossEncounterPhase.Fading)
            {
                _localPlayer.SetEncounterControlLocked(true);
                _transitionUI = await OpenUIAsync<ScreenTransitionUI>(UIType.ScreenTransitionUI, token);
                await _transitionUI.FadeAsync(true, token);
            }
            else if (phase == BossEncounterPhase.Moving)
            {
                if (!_localPlayer.PlaceForEncounter(destination, true)) throw new InvalidOperationException("보스전 이동에 실패했습니다.");
            }
            else if (phase == BossEncounterPhase.Framing)
            {
                await PrepareIntroductionAsync(token);
            }
            else if (phase == BossEncounterPhase.Intro)
            {
                await PlayIntroductionAsync(token);
            }
            else if (phase == BossEncounterPhase.Recovering)
            {
                if (local.TryGetComponent(out PlayerNetworkStats stats) && stats.IsAlive)
                {
                    // UI가 없거나 로딩에 실패해도 귀환 배치 자체는 반드시 시도합니다.
                    try
                    {
                        if (_transitionUI != null && _transitionUI.isActiveAndEnabled)
                        {
                            await _transitionUI.FadeAsync(true, token);
                        }
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                    catch (Exception error) { Debug.LogWarning($"귀환 암전 생략: {error.Message}", this); }
                    if (!_localPlayer.PlaceForEncounter(destination, true)) throw new InvalidOperationException("보스전 귀환 배치에 실패했습니다.");
                    RestoreEncounterCamera();
                    try
                    {
                        if (_transitionUI != null) await _transitionUI.FadeAsync(false, token);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                    catch (Exception error) { Debug.LogWarning($"귀환 밝아짐 생략: {error.Message}", this); }
                }
                CompleteLocalStep(run, phase);
                CleanupLocal();
                return;
            }
            else if (phase == BossEncounterPhase.Releasing)
            {
                RestoreEncounterCamera();
                if (_cinematicUI != null) await _cinematicUI.HideAsync(token);
                CompleteLocalStep(run, phase);
                CleanupLocal();
                return;
            }
            else return;
            CompleteLocalStep(run, phase);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            if (token.IsCancellationRequested) return;
            Debug.LogException(exception, this);
            if (run != _localRun || phase != _localPhase) return;
            ScenarioManager.Inst?.AcknowledgeBossStep(run, phase, false);
            CleanupLocal();
        }
    }

    private static async UniTask<T> OpenUIAsync<T>(UIType type, CancellationToken token) where T : UIBase
    {
        UIManager manager = UIManager.Inst;
        while (manager != null && manager.IsLoading(type)) await UniTask.Yield(PlayerLoopTiming.Update, token);
        token.ThrowIfCancellationRequested();
        UIBase view = await manager.OpenVeryFrontRootAsync(type, token);
        token.ThrowIfCancellationRequested();
        if (view is not T typed) throw new InvalidOperationException($"보스전 UI 연결을 확인하세요: {type}");
        return typed;
    }

    private void OnEntryChosen(bool enter)
    {
        ScenarioManager.Inst?.ChooseBossEntry(_localRun, enter);
    }

    private void CompleteLocalStep(uint run, BossEncounterPhase phase)
    {
        if (run == _localRun && phase == _localPhase) ScenarioManager.Inst?.AcknowledgeBossStep(run, phase, true);
    }

    private void UpdateLocalLifetime()
    {
        if (!_localActive) return;
        if (_manager == null || !_manager.IsSpawned || Network == null || !Network.IsListening ||
            Time.unscaledTime > _localDeadline ||
            (_localPlayer != null && _localPlayer.TryGetComponent(out PlayerNetworkStats stats) && !stats.IsAlive))
        {
            _manager?.AcknowledgeBossStep(_localRun, _localPhase, false);
            CleanupLocal();
        }
    }

    private void CancelLocalStep()
    {
        if (_localStep == null) return;
        _localStep.Cancel();
        _localStep.Dispose();
        _localStep = null;
    }

    private void CloseEntryUI()
    {
        if (_entryUI == null) return;
        _entryUI.Chosen -= OnEntryChosen;
        CloseIfOpen(UIType.BossEntryUI);
        _entryUI = null;
    }

    private static void CloseIfOpen(UIType type)
    {
        if (UIManager.Inst != null && UIManager.Inst.IsOpen(type)) UIManager.Inst.Close(type);
    }

    private void CleanupLocal()
    {
        CancelLocalStep();
        _localActive = false;
        CloseEntryUI();
        // 공유 UI 종류라도 이번 보스 흐름에서 얻은 화면만 정리합니다.
        if (_transitionUI != null) CloseIfOpen(UIType.ScreenTransitionUI);
        if (_cinematicUI != null) CloseIfOpen(UIType.CinematicEffectUI);
        _transitionUI = null;
        _cinematicUI = null;
        RestoreEncounterCamera();
        if (_localPlayer != null) _localPlayer.SetEncounterControlLocked(false);
        _localPlayer = null;
    }

    partial void RestoreEncounterCamera();
}
