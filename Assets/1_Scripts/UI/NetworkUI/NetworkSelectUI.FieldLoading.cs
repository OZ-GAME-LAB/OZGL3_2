using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public partial class NetworkSelectUI
{
    [Header("필드 준비 대기")]
    [SerializeField, Min(1f)] private float _fieldReadyTimeout = 30f;
    private bool _enteringField;

    private async UniTask EnterFieldAsync(NetCodeNetworkManager manager)
    {
        _enteringField = true;
        RefreshInteractable();
        var token = this.GetCancellationTokenOnDestroy();
        UIManager ui = UIManager.Inst;
        LoadingUI loading = null;
        bool started = false;
        try
        {
            loading = await ui.OpenLoadingUIAsync(token);
            if (loading == null) throw new InvalidOperationException("로딩 화면을 열 수 없습니다.");
            await UniTask.NextFrame(cancellationToken: token);
            await DataManager.Inst.PrepareFieldTablesAsync(ReportTableProgress, token);

            var inventory = ServiceManager.Inst.InventoryService;
            if (!inventory.TryRestoreSaveItems(GameStartSelection.Character.Items, out string error))
                throw new InvalidOperationException($"캐릭터 인벤토리를 불러오지 못했습니다.\n{error}");
            loading.SetProgress(0.55f);
            await UniTask.NextFrame(cancellationToken: token);

            started = GameStartSelection.IsHost
                ? manager.TryStartHost(GameStartSelection.Port, out error)
                : manager.TryStartClient(GameStartSelection.Address, GameStartSelection.Port, out error);
            if (!started) throw new InvalidOperationException(error);
            if (manager.IsConnecting)
            {
                loading.SetCancelAction(manager.CancelConnection);
                while (manager.IsConnecting)
                    await UniTask.NextFrame(cancellationToken: token);
                loading.SetCancelAction(null);
            }
            if (!manager.IsFieldSessionActive) return;
            loading.SetProgress(0.65f);
            bool ready = await WaitForFieldAsync(manager, ui, loading, token);
            if (ready)
                await loading.ShowCompleteAsync(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            Debug.LogWarning($"필드 진입 실패: {ex.Message}", this);
            if (started && manager != null && manager.IsFieldSessionActive)
            {
                await manager.AbortFieldEntryAsync();
                if (ui != null && !token.IsCancellationRequested)
                    await ui.OpenSimplePopup(ex.Message);
            }
            else if (this != null)
                SetMessage(ex.Message);
        }
        finally
        {
            if (ui != null && ui.IsOpen(UIType.LoadingUI))
                ui.Close(UIType.LoadingUI);
            _enteringField = false;
            if (this != null && isActiveAndEnabled)
                RefreshInteractable();
        }

        void ReportTableProgress(float progress) => loading.SetProgress(progress * 0.5f);
    }

    private async UniTask<bool> WaitForFieldAsync(NetCodeNetworkManager manager, UIManager ui, LoadingUI loading, CancellationToken token)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + _fieldReadyTimeout;
        var game = GameManager.Inst;
        PlayerNetworkStats stats = null;
        PlayerRuneEquipmentController runes = null;
        int lastCount = -1;
        while (manager.IsFieldSessionActive)
        {
            var player = game.ReadyLocalPlayer;
            if (player != null && stats == null)
            {
                if (!player.TryGetComponent(out stats) || !player.TryGetComponent(out runes))
                    throw new InvalidOperationException("플레이어의 필드 데이터 컴포넌트를 확인해 주세요.");
            }
            int count = 0;
            if (game.IsFieldWorldReady) count++;
            if (ScenarioManager.Inst != null && ScenarioManager.Inst.IsReady) count++;
            if (player != null && player.IsSpawned) count++;
            if (stats != null && stats.IsFieldRegistered) count++;
            if (runes != null && runes.CanCaptureRunes) count++;
            if (ui.IsOpen(UIType.PlayerHudUI)) count++;
            if (count == 6) return true;
            if (count != lastCount)
            {
                loading.SetProgress(0.65f + 0.35f * count / 6f);
                lastCount = count;
            }
            if (Time.realtimeSinceStartupAsDouble >= deadline)
                throw new TimeoutException("필드 준비가 완료되지 않았습니다. 로비로 돌아갑니다.");
            await UniTask.NextFrame(cancellationToken: token);
        }
        return false;
    }
}
