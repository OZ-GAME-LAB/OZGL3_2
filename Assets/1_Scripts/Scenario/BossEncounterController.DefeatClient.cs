using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed partial class BossEncounterController
{
    private PlayerNetworkStats _defeatLocalStats;
    private DemoCompleteUI _demoCompleteUI;
    private bool _defeatLocalOwned;
    private bool _readingDemoComplete;

    private async UniTask PrepareDefeatAsync(CancellationToken token)
    {
        _defeatLocalOwned = true;
        _localPlayer.TryGetComponent(out _defeatLocalStats);
        _defeatLocalStats?.SetGameOverSuppressed(true);
        CloseIfOpen(UIType.InventoryUI);
        CloseIfOpen(UIType.RuneInventoryUI);
        CloseIfOpen(UIType.CraftingUI);
        CloseIfOpen(UIType.SettingUI);
        _localPlayer.SetEncounterControlLocked(true);
        BossNetwork boss = await ResolveBossAsync(token);
        CreateEncounterCamera(boss.transform.position);
        _cinematicUI = await OpenUIAsync<CinematicEffectUI>(UIType.CinematicEffectUI, token);
        await _cinematicUI.ShowAsync(token);
    }

    private async UniTask PlayDefeatAsync(uint run, BossEncounterPhase phase, CancellationToken token)
    {
        // A timed-out preparation can still recover when the sink step arrives.
        if (!_defeatLocalOwned || _encounterCamera == null || _cinematicUI == null)
            await PrepareDefeatAsync(token);
        BossNetwork boss = await ResolveBossAsync(token);
        while (boss != null && boss.IsSpawned && boss.DefeatStartTime < 0d)
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        if (boss == null || !boss.IsSpawned) throw new InvalidOperationException("처치 연출 보스가 없습니다.");
        double end = boss.DefeatStartTime + boss.DefeatDuration;
        _localDeadline = Time.unscaledTime + (float)Math.Max(0d, end - Network.ServerTime.Time) + _stepTimeout + 5f;
        while (Network.ServerTime.Time < end)
        {
            if (boss == null || !boss.IsSpawned) throw new InvalidOperationException("처치 연출 중 보스가 사라졌습니다.");
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }
        // Let the final sprite presentation run before removing the cinematic frame.
        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);
        if (_cinematicUI != null) await _cinematicUI.HideAsync(token);
        if (_cinematicUI != null) CloseIfOpen(UIType.CinematicEffectUI);
        _cinematicUI = null;
        RestoreEncounterCamera();
        CompleteLocalStep(run, phase);
        // Server completion does not wait for a person to read or confirm this window.
        _demoCompleteUI = await OpenUIAsync<DemoCompleteUI>(UIType.DemoCompleteUI, token);
        _demoCompleteUI.Dismissed += OnDemoCompleteDismissed;
        _readingDemoComplete = true;
        _localPlayer.SetEncounterConfirmationInput();
        _demoCompleteUI.FocusConfirmation();
    }

    private void MaintainDefeatLocalLock()
    {
        if (!_defeatLocalOwned || _localPlayer == null || _localPlayer.HasLocalEncounterLock) return;
        // Death presentation may release the earlier encounter lock before this frame.
        _localPlayer.SetEncounterControlLocked(true);
        if (_readingDemoComplete) _localPlayer.SetEncounterConfirmationInput();
    }

    private void OnDemoCompleteDismissed()
    {
        CleanupLocal();
    }

    private void CleanupDefeatLocal()
    {
        _readingDemoComplete = false;
        _defeatLocalOwned = false;
        if (_demoCompleteUI != null)
        {
            _demoCompleteUI.Dismissed -= OnDemoCompleteDismissed;
            CloseIfOpen(UIType.DemoCompleteUI);
            _demoCompleteUI = null;
        }
        if (_defeatLocalStats != null) _defeatLocalStats.SetGameOverSuppressed(false);
        _defeatLocalStats = null;
    }
}
