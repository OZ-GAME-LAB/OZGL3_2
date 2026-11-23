using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum ScenarioLocalState
{
    Idle,
    WaitingForStart,
    OpeningDialogue,
    PlayingDialogue,
    WaitingForCompletion
}

public enum ScenarioEndReason
{
    Completed,
    Cancelled,
    Rejected,
    WorldChanged,
    Failed,
    TimedOut,
    Disconnected
}

// ServiceManager가 생성/해제합니다. 개인 완료 기록은 현재 네트워크 세션에만 유지합니다.
public sealed class ScenarioService : IDisposable
{
    private readonly HashSet<string> _personalCompleted = new HashSet<string>();
    private ScenarioManager _manager;
    private ScenarioData _scenario;
    private IReadOnlyList<DialogueLineData> _lines;
    private ulong _nextRequestId;
    private int _lineIndex;
    private int _lastAdvanceFrame;
    private float _remainingRequestTime;
    private bool _ending;
    private bool _binding;
    private bool _disposed;

    public DialogueViewModel Dialogue { get; } = new DialogueViewModel();
    public ScenarioLocalState State { get; private set; }
    public ScenarioSource ActiveSource { get; private set; }
    public ulong ActiveRequestId { get; private set; }
    public bool IsBusy => State != ScenarioLocalState.Idle;
    public bool IsReady => !_disposed && !_binding && _manager != null && _manager.IsReady;
    public event Action StateChanged;
    public event Action<ScenarioSource, ScenarioEndReason> Ended;

    public ScenarioService()
    {
        ScenarioManager.AvailabilityChanged += BindManager;
        BindManager();
    }

    private void BindManager()
    {
        if (_disposed || _binding || _manager == ScenarioManager.Inst)
            return;

        _binding = true;
        End(ScenarioEndReason.Disconnected);
        if (_manager != null)
        {
            _manager.ProgressChanged -= OnProgressChanged;
            _manager.ReplyReceived -= OnReplyReceived;
        }
        _manager = ScenarioManager.Inst;
        _personalCompleted.Clear();
        if (_manager != null)
        {
            _manager.ProgressChanged += OnProgressChanged;
            _manager.ReplyReceived += OnReplyReceived;
        }
        _binding = false;
        StateChanged?.Invoke();
    }

    public bool CanStart(ScenarioSource source)
    {
        return IsReady && !_ending && !IsBusy && source != null && source.isActiveAndEnabled &&
            _manager.TryGetScenario(source.ScenarioId, out ScenarioData data) &&
            data.IsDialogue && _manager.CanStartForPlayer(data, _personalCompleted);
    }

    public bool TryStart(ScenarioSource source)
    {
        if (!CanStart(source))
            return false;

        NetworkManager network = NetworkManager.Singleton;
        NetworkObject player = network != null && network.LocalClient != null ? network.LocalClient.PlayerObject : null;
        if (player == null || !player.IsOwner || !source.ContainsPlayer(player) ||
            !_manager.TryGetScenario(source.ScenarioId, out ScenarioData data))
            return false;

        IReadOnlyList<DialogueLineData> lines = _manager.GetDialogue(data.DialogueId);
        if (lines == null || lines.Count == 0)
            return false;

        _scenario = data;
        _lines = lines;
        _lineIndex = 0;
        ActiveSource = source;
        ActiveRequestId = ++_nextRequestId;
        SetWaitingState(ScenarioLocalState.WaitingForStart);
        // 입력 담당자가 시작을 거절/취소했다면 서버 요청을 보내지 않습니다.
        if (State == ScenarioLocalState.WaitingForStart)
            _manager.RequestStart(source.SourceId, ActiveRequestId);
        return IsBusy;
    }

    private void OnReplyReceived(ulong requestId, ScenarioReply reply, string personalId)
    {
        // 요청 UI가 이미 닫혔어도 서버가 확정한 개인 선행 조건을 복원합니다.
        if (reply == ScenarioReply.Completed && !string.IsNullOrEmpty(personalId) &&
            _personalCompleted.Add(personalId))
            StateChanged?.Invoke();

        if (!IsBusy || requestId != ActiveRequestId)
            return;

        if (reply == ScenarioReply.Rejected)
        {
            End(ScenarioEndReason.Rejected);
            return;
        }
        if (State == ScenarioLocalState.WaitingForStart && reply == ScenarioReply.Started)
        {
            if (!_manager.CanContinue(_scenario))
            {
                End(ScenarioEndReason.WorldChanged);
                return;
            }
            Dialogue.ShowLine(_lines[0], false);
            SetWaitingState(ScenarioLocalState.OpeningDialogue);
        }
        else if (State == ScenarioLocalState.WaitingForCompletion && reply == ScenarioReply.Completed)
        {
            // 공유 상태 알림이 먼저 도착했어도 이 승인 응답이 정상 완료의 기준입니다.
            if (!string.IsNullOrEmpty(_scenario.RewardRuneId) &&
                ServiceManager.Inst?.InventoryService?.TryReceiveStoryRune(_scenario.RewardRuneId) != true)
            {
                End(ScenarioEndReason.Failed);
                return;
            }
            if (_scenario.PlayOncePerPlayer)
                _personalCompleted.Add(_scenario.Id);
            End(ScenarioEndReason.Completed);
        }
    }

    // 비동기 UI 로딩을 연결하는 단계에서 요청 번호를 그대로 돌려줍니다.
    public bool NotifyDialogueOpened(ulong requestId)
    {
        if (State != ScenarioLocalState.OpeningDialogue || ActiveRequestId != requestId)
            return false;
        if (!_manager.CanContinue(_scenario))
        {
            End(ScenarioEndReason.WorldChanged);
            return false;
        }
        State = ScenarioLocalState.PlayingDialogue;
        _lastAdvanceFrame = Time.frameCount;
        Dialogue.SetCanAdvance(true);
        StateChanged?.Invoke();
        return State == ScenarioLocalState.PlayingDialogue;
    }

    public void NotifyDialogueFailed(ulong requestId)
    {
        if (IsBusy && requestId == ActiveRequestId)
            End(ScenarioEndReason.Failed);
    }

    public void AdvanceDialogue()
    {
        if (State != ScenarioLocalState.PlayingDialogue || Time.frameCount == _lastAdvanceFrame)
            return;
        _lastAdvanceFrame = Time.frameCount;

        if (_lineIndex + 1 < _lines.Count)
        {
            _lineIndex++;
            Dialogue.ShowLine(_lines[_lineIndex], true);
            return;
        }

        Dialogue.SetCanAdvance(false);
        SetWaitingState(ScenarioLocalState.WaitingForCompletion);
        if (State == ScenarioLocalState.WaitingForCompletion)
            _manager.RequestComplete(ActiveRequestId);
    }

    private void SetWaitingState(ScenarioLocalState state)
    {
        State = state;
        _remainingRequestTime = _manager.RequestTimeout;
        StateChanged?.Invoke();
    }

    // ServiceManager.Update에서 unscaledDeltaTime을 전달합니다. 전체 사건을 순회하지 않습니다.
    public void Tick(float unscaledDeltaTime)
    {
        if (!IsBusy)
            return;
        if (!IsReady)
        {
            End(ScenarioEndReason.Disconnected);
            return;
        }
        // 구출 완료로 NPC가 숨겨져도 이미 보낸 완료 요청의 응답을 기다립니다.
        if (State != ScenarioLocalState.WaitingForCompletion &&
            (ActiveSource == null || !ActiveSource.isActiveAndEnabled))
        {
            End(ScenarioEndReason.Cancelled);
            return;
        }
        if (State == ScenarioLocalState.PlayingDialogue)
            return;

        _remainingRequestTime -= Mathf.Max(0f, unscaledDeltaTime);
        if (_remainingRequestTime <= 0f)
            End(ScenarioEndReason.TimedOut);
    }

    private void OnProgressChanged()
    {
        if (IsBusy && State != ScenarioLocalState.WaitingForCompletion && !_manager.CanContinue(_scenario))
            End(ScenarioEndReason.WorldChanged);
        else
            StateChanged?.Invoke();
    }

    public void Cancel()
    {
        End(ScenarioEndReason.Cancelled);
    }

    public void CancelFromSource(ScenarioSource source)
    {
        if (ActiveSource == source && State != ScenarioLocalState.WaitingForCompletion)
            Cancel();
    }

    private void End(ScenarioEndReason reason)
    {
        if (!IsBusy || _ending)
            return;
        _ending = true;
        ScenarioSource source = ActiveSource;
        ulong requestId = ActiveRequestId;
        State = ScenarioLocalState.Idle;
        ActiveRequestId = 0;
        ActiveSource = null;
        _scenario = null;
        _lines = null;
        Dialogue.ShowLine(null, false);
        if (reason != ScenarioEndReason.Completed && _manager != null)
            _manager.RequestCancel(requestId);
        Ended?.Invoke(source, reason);
        _ending = false;
        StateChanged?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        ScenarioManager.AvailabilityChanged -= BindManager;
        End(ScenarioEndReason.Disconnected);
        if (_manager != null)
        {
            _manager.ProgressChanged -= OnProgressChanged;
            _manager.ReplyReceived -= OnReplyReceived;
        }
        _manager = null;
        _personalCompleted.Clear();
    }
}
