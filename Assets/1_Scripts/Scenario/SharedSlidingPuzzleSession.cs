using System;
using Unity.Netcode;
using UnityEngine;

// 석판 한 개의 로컬 요청 수명입니다. 보드 변경과 성공 판정은 서버가 담당합니다.
public sealed class SharedSlidingPuzzleSession : IDisposable
{
    private static ulong _nextRequestId;
    private readonly ScenarioSource _source;
    private ScenarioManager _manager;
    private ulong _requestId;
    private uint _sequence;
    private int _pendingTile;
    private bool _waiting;
    private float _remaining;

    public int[] Board { get; private set; }
    public bool Active => _requestId != 0;
    public bool CanMove => Active && !_waiting && Board != null && !IsCompleted;
    public bool IsCompleted => _manager != null && _manager.IsReady && _source != null &&
        _manager.IsCompleted(_source.ScenarioId);
    public bool CanStart => _manager != null && _manager.IsReady && ValidSource &&
        _manager.TryGetScenario(_source.ScenarioId, out ScenarioData data) &&
        data.IsSlidingPuzzle && data.CompleteWorldOnFinish && _manager.CanStart(data);
    private bool ValidSource => _source != null && _source.isActiveAndEnabled &&
        ScenarioSource.TryGet(_source.SourceId, out ScenarioSource registered) && registered == _source;

    public event Action Changed;
    public event Action<int> BoardChanged;
    public event Action Ended;

    public SharedSlidingPuzzleSession(ScenarioSource source)
    {
        _source = source;
        _source.AvailabilityChanged += OnProgressChanged;
        ScenarioManager.AvailabilityChanged += BindManager;
        BindManager();
    }

    private void BindManager()
    {
        bool active = Active;
        Cancel();
        if (_manager != null)
        {
            _manager.ProgressChanged -= OnProgressChanged;
            _manager.PuzzleReplyReceived -= OnReply;
        }
        _manager = ScenarioManager.Inst;
        Board = null;
        if (_manager != null)
        {
            _manager.ProgressChanged += OnProgressChanged;
            _manager.PuzzleReplyReceived += OnReply;
        }
        Changed?.Invoke();
        if (active)
            Ended?.Invoke();
    }

    public bool TryBegin()
    {
        if (Active || !CanStart || !ContainsLocalPlayer())
            return false;
        _requestId = ++_nextRequestId;
        if (_requestId == 0)
            _requestId = ++_nextRequestId;
        _sequence = 0;
        _pendingTile = 0;
        Board = null;
        WaitForReply();
        _manager.RequestPuzzleStart(_source.SourceId, _requestId);
        return true;
    }

    public void TryMove(int tileId)
    {
        if (!CanMove || tileId < 1 || tileId > 8)
            return;
        int empty = Array.IndexOf(Board, 0);
        int tile = Array.IndexOf(Board, tileId);
        if (tile < 0 || Math.Abs(empty / 3 - tile / 3) + Math.Abs(empty % 3 - tile % 3) != 1)
            return;
        _pendingTile = tileId;
        _sequence++;
        WaitForReply();
        _manager.RequestPuzzleMove(_source.SourceId, _requestId, _sequence, tileId);
    }

    private void WaitForReply()
    {
        _waiting = true;
        _remaining = _manager.RequestTimeout;
        Changed?.Invoke();
    }

    private void OnReply(string sourceId, ulong requestId, uint sequence, ScenarioPuzzleReply reply, int[] board)
    {
        if (!Active || sourceId != _source.SourceId || requestId != _requestId || sequence != _sequence || !_waiting)
            return;
        if (reply == ScenarioPuzzleReply.Rejected || board == null || board.Length != 9)
        {
            End();
            return;
        }
        Board = board;
        _waiting = reply == ScenarioPuzzleReply.Completed;
        _remaining = _manager.RequestTimeout;
        BoardChanged?.Invoke(_pendingTile);
        Changed?.Invoke();
        // 완료 응답만으로 표시를 확정하지 않습니다. 공유 목록의 변경을 기다립니다.
    }

    private void OnProgressChanged()
    {
        Changed?.Invoke();
        if (Active && !CanStart)
            End();
    }

    public void Tick(float deltaTime)
    {
        if (!Active)
            return;
        if (!CanStart || !ContainsLocalPlayer())
        {
            End();
            return;
        }
        if (_waiting)
        {
            _remaining -= deltaTime;
            if (_remaining <= 0f)
                End();
        }
    }

    private bool ContainsLocalPlayer()
    {
        NetworkManager network = NetworkManager.Singleton;
        NetworkObject player = network != null && network.LocalClient != null ? network.LocalClient.PlayerObject : null;
        return ValidSource && player != null && player.IsOwner && _source.ContainsPlayer(player);
    }

    private void End()
    {
        Cancel();
        Ended?.Invoke();
    }

    public void Cancel()
    {
        ulong request = _requestId;
        _requestId = 0;
        _waiting = false;
        if (request != 0 && _manager != null)
            _manager.RequestPuzzleCancel(request);
    }

    public void Dispose()
    {
        Cancel();
        ScenarioManager.AvailabilityChanged -= BindManager;
        if (_source != null)
            _source.AvailabilityChanged -= OnProgressChanged;
        if (_manager != null)
        {
            _manager.ProgressChanged -= OnProgressChanged;
            _manager.PuzzleReplyReceived -= OnReply;
        }
    }
}
