using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 소유자의 HP 변화와 UI만 연결합니다. HP/부활 위치의 결정은 서버에 맡깁니다.
public sealed class GameOverPresenter : IDisposable
{
    private readonly PlayerNetworkStats _stats;
    private readonly PlayerControllerView _view;
    private CancellationTokenSource _opening;
    private UIManager _manager;
    private GameOverUI _ui;
    private bool _dead;
    private bool _disposed;
    private bool _suppressed;

    public GameOverPresenter(PlayerNetworkStats stats, PlayerControllerView view)
    {
        _stats = stats;
        _view = view;
        _stats.StatsChanged += OnStatsChanged;
        _stats.RespawnFailed += OnRespawnFailed;
        _stats.RespawnSucceeded += OnRespawnSucceeded;
        OnStatsChanged();
    }

    private void OnStatsChanged()
    {
        if (_disposed || !_stats.IsSpawned || _stats.MaxHp <= 0f) return;
        if (_stats.IsAlive)
        {
            RestoreAliveState();
            return;
        }
        if (_dead) return;
        _dead = true;
        _view?.SetDeathControlLocked(true);
        if (!_suppressed) StartOpening();
    }

    public void SetSuppressed(bool suppressed)
    {
        if (_disposed || _suppressed == suppressed) return;
        _suppressed = suppressed;
        if (suppressed) ReleaseUI();
        else if (_dead && _stats.IsSpawned && !_stats.IsAlive &&
            _stats.NetworkManager != null && _stats.NetworkManager.IsListening) StartOpening();
    }

    private void StartOpening()
    {
        _opening = new CancellationTokenSource();
        OpenAsync(_opening.Token).Forget();
    }

    private void RestoreAliveState()
    {
        _dead = false;
        // UI 정리가 실패해도 조작 잠금 해제는 시도하며, 재호출 시 UI 정리를 다시 시도합니다.
        try { ReleaseUI(); }
        finally { _view?.SetDeathControlLocked(false); }
    }

    private void OnRespawnSucceeded()
    {
        if (!_disposed && _stats.IsAlive) RestoreAliveState();
    }

    private async UniTask OpenAsync(CancellationToken token)
    {
        try
        {
            _manager = UIManager.Inst;
            if (_manager == null) throw new InvalidOperationException("Game Over 화면에 UIManager가 필요합니다.");
            GameOverUI ui = await _manager.OpenGameOverUIAsync(token);
            if (_disposed || token.IsCancellationRequested || !_dead || _suppressed) return;
            if (ui == null) throw new InvalidOperationException("UI/GameOverUI 프리팹을 불러오지 못했습니다.");
            _ui = ui;
            _ui.transform.SetAsLastSibling();
            _ui.RespawnRequested += OnRespawnRequested;
            _ui.SetState(_stats.IsRespawning);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private void OnRespawnRequested()
    {
        if (_disposed || _suppressed || !_dead || _stats.IsRespawning) return;
        _ui?.SetState(true);
        if (!_stats.RequestRespawn())
        {
            OnStatsChanged();
            _ui?.SetState(false, "부활 요청을 시작하지 못했습니다. 다시 시도해 주세요.");
        }
    }

    private void OnRespawnFailed(string message)
    {
        if (!_disposed && _dead && _ui != null) _ui.SetState(false, message);
    }

    private void ReleaseUI()
    {
        if (_opening != null)
        {
            _opening.Cancel();
            _opening.Dispose();
            _opening = null;
        }
        if (_ui != null)
        {
            _ui.RespawnRequested -= OnRespawnRequested;
            if (_manager != null && _manager.IsOpen(UIType.GameOverUI))
                _manager.Close(UIType.GameOverUI);
        }
        _ui = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stats.StatsChanged -= OnStatsChanged;
        _stats.RespawnFailed -= OnRespawnFailed;
        _stats.RespawnSucceeded -= OnRespawnSucceeded;
        ReleaseUI();
        _view?.SetDeathControlLocked(false);
    }
}
