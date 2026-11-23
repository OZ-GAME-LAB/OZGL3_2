using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Cinemachine;
using UnityEngine;

public sealed partial class BossEncounterController
{
    private CinemachineCamera _encounterCamera;

    private async UniTask<BossNetwork> ResolveBossAsync(CancellationToken token)
    {
        BossNetwork boss = null;
        while (boss == null)
        {
            token.ThrowIfCancellationRequested();
            if (Network != null && _manager != null &&
                Network.SpawnManager.SpawnedObjects.TryGetValue(_manager.EncounterBossId, out var instance))
                instance.TryGetComponent(out boss);
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }
        return boss;
    }

    private async UniTask PrepareIntroductionAsync(CancellationToken token)
    {
        BossNetwork boss = await ResolveBossAsync(token);
        CreateEncounterCamera(boss.transform.position);
        _cinematicUI = await OpenUIAsync<CinematicEffectUI>(UIType.CinematicEffectUI, token);
        await _cinematicUI.ShowAsync(token);
        if (_transitionUI != null) await _transitionUI.FadeAsync(false, token);
    }

    private void CreateEncounterCamera(Vector3 bossPosition)
    {
        RestoreEncounterCamera();
        GameObject cameraObject = new("BossEncounterCamera");
        _encounterCamera = cameraObject.AddComponent<CinemachineCamera>();
        cameraObject.transform.position = bossPosition + _cinematicCameraOffset;
        _encounterCamera.Priority = 1000;
        var lens = _encounterCamera.Lens;
        lens.OrthographicSize = _cinematicCameraSize;
        lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
        _encounterCamera.Lens = lens;
    }

    private async UniTask PlayIntroductionAsync(CancellationToken token)
    {
        BossNetwork boss = await ResolveBossAsync(token);
        BossIntroVisual visual = boss.GetComponent<BossIntroVisual>();
        if (visual == null) throw new System.InvalidOperationException("보스 눈뜨기 연출 연결이 없습니다.");
        while (boss != null && boss.IsSpawned && boss.IntroStartTime < 0d)
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        while (boss != null && boss.IsSpawned && Network.ServerTime.Time < boss.IntroStartTime + visual.Duration)
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        if (boss == null || !boss.IsSpawned) throw new System.InvalidOperationException("연출 중 보스가 사라졌습니다.");
    }

    partial void RestoreEncounterCamera()
    {
        if (_encounterCamera == null) return;
        _encounterCamera.enabled = false;
        Destroy(_encounterCamera.gameObject);
        _encounterCamera = null;
    }
}
