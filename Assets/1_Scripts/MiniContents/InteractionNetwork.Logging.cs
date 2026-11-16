using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum LoggingPresentationPhase : byte { None, Impact, Sinking }

public struct LoggingPresentationState : INetworkSerializable, IEquatable<LoggingPresentationState>
{
    public LoggingPresentationPhase Phase;
    public double StartTime;
    public float Duration;
    public ulong RequestId;
    public int Sequence;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Phase);
        serializer.SerializeValue(ref StartTime);
        serializer.SerializeValue(ref Duration);
        serializer.SerializeValue(ref RequestId);
        serializer.SerializeValue(ref Sequence);
    }
    public bool Equals(LoggingPresentationState other) => Phase == other.Phase &&
        StartTime.Equals(other.StartTime) && Duration.Equals(other.Duration) &&
        RequestId == other.RequestId && Sequence == other.Sequence;
}

public partial class InteractionNetwork
{
    private readonly NetworkVariable<LoggingPresentationState> _loggingPresentation = new(default,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private LoggingTreeEffect _loggingEffect;
    private InteractableObject _loggingContent;
    private bool _loggingVisualUpdating;
    private int _lastLoggingImpactSequence;
    private PendingLoggingReward _pendingLoggingReward;

    private sealed class PendingLoggingReward
    {
        public readonly string[] Items;
        public readonly int PlayerCount;
        public readonly Vector3 Position;
        public readonly double EndTime;
        public readonly bool DestroyAfterUse;
        public PendingLoggingReward(string[] items, int playerCount, Vector3 position, double endTime, bool destroyAfterUse)
        {
            Items = items; PlayerCount = playerCount; Position = position;
            EndTime = endTime; DestroyAfterUse = destroyAfterUse;
        }
    }

    private void InitializeLoggingPresentation(InteractableObject content)
    {
        _loggingContent = content;
        _loggingEffect = content.LoggingEffect;
        OnLoggingPresentationChanged(default, _loggingPresentation.Value);
    }

    private void OnLoggingPresentationChanged(LoggingPresentationState previous, LoggingPresentationState current)
    {
        _loggingVisualUpdating = current.Phase != LoggingPresentationPhase.None;
        if (current.Phase == LoggingPresentationPhase.None && _loggingEffect != null)
            _loggingEffect.ResetPresentation();
        UpdateLoggingVisual();
    }

    private void UpdateLoggingVisual()
    {
        if (!IsClient || !IsReady || !_loggingVisualUpdating || _loggingEffect == null) return;
        LoggingPresentationState state = _loggingPresentation.Value;
        _loggingVisualUpdating = _loggingEffect.Present(state.Phase == LoggingPresentationPhase.Sinking,
            state.StartTime, state.Duration, NetworkManager.ServerTime.Time);
    }

    private void Update()
    {
        if (!IsSpawned) return;
        UpdateLoggingVisual();
        if (IsServer && TryTakeLoggingReward(NetworkManager.ServerTime.Time, out PendingLoggingReward reward))
        {
            // 대기 상태를 먼저 소비합니다. 이후 취소/중복 완료가 같은 보상을 다시 지급할 수 없습니다.
            try { DropRewardUtil.SpawnItemsOnServer(reward.Items, reward.PlayerCount, reward.Position, this); }
            finally
            {
                if (IsSpawned && _loggingContent != null)
                {
                    if (reward.DestroyAfterUse)
                    {
                        _loggingContent.NotifyRespawnRequested();
                        NetworkObject.Despawn();
                    }
                    else
                    {
                        _isDormant.Value = true;
                        _loggingContent.NotifyRespawnRequested();
                    }
                }
            }
        }
    }

    private bool TryTakeLoggingReward(double now, out PendingLoggingReward reward)
    {
        reward = null;
        if (_pendingLoggingReward == null || now < _pendingLoggingReward.EndTime) return false;
        reward = _pendingLoggingReward;
        _pendingLoggingReward = null;
        return true;
    }

    public void ScheduleLoggingImpactOnServer(ulong clientId, ulong requestId, int sequence, double impactTime)
    {
        if (!IsServer || !IsReady || _loggingEffect == null || _pendingLoggingReward != null ||
            !HasReservationOnServer(clientId, requestId) || sequence <= _lastLoggingImpactSequence ||
            double.IsNaN(impactTime) || double.IsInfinity(impactTime)) return;
        _lastLoggingImpactSequence = sequence;
        _loggingPresentation.Value = new LoggingPresentationState
        {
            Phase = LoggingPresentationPhase.Impact, StartTime = impactTime,
            Duration = _loggingEffect.ImpactSeconds, RequestId = requestId, Sequence = sequence
        };
    }

    private void CancelFutureLoggingImpact()
    {
        _lastLoggingImpactSequence = 0;
        if (!IsServer || !IsSpawned) return;
        LoggingPresentationState state = _loggingPresentation.Value;
        if (state.Phase == LoggingPresentationPhase.Impact && NetworkManager.ServerTime.Time < state.StartTime)
            _loggingPresentation.Value = default;
    }

    private bool TryBeginLoggingCompletion(FieldObjectData data, InteractableObject target, float multiplier, int playerCount, ulong requestId)
    {
        if (_pendingLoggingReward != null || _loggingEffect == null || playerCount <= 0) return false;
        if (!TryPrepareLoggingRewards(data, multiplier, out string[] items)) return false;
        if (items.Length == 0)
        {
            // 정상적인 0개 추첨 결과는 데이터 오류와 구분하고 나무를 남깁니다.
            ReleaseInteractorOnServer();
            return true;
        }
        double start = NetworkManager.ServerTime.Time;
        float duration = _loggingEffect.SinkSeconds;
        _pendingLoggingReward = new PendingLoggingReward(items, playerCount, transform.position,
            start + duration, target.DestroyAfterUse);
        _serverAllowsInteraction = false;
        ReleaseInteractorOnServer();
        _loggingPresentation.Value = new LoggingPresentationState
        {
            Phase = LoggingPresentationPhase.Sinking, StartTime = start,
            Duration = duration, RequestId = requestId
        };
        return true;
    }

    private bool TryPrepareLoggingRewards(FieldObjectData data, float multiplier, out string[] items)
    {
        items = null;
        var selected = new List<string>();
        if (!data.IsMandatoryDropItem && string.IsNullOrWhiteSpace(data.ProbabilityDropGroupId))
        {
            Debug.LogError("벌목 보상 그룹이 설정되지 않았습니다.", this);
            return false;
        }
        if (data.IsMandatoryDropItem && !ValidateLoggingRewardGroup(data.MandatoryDropGroupId, true)) return false;
        if (!string.IsNullOrWhiteSpace(data.ProbabilityDropGroupId) &&
            !ValidateLoggingRewardGroup(data.ProbabilityDropGroupId, false)) return false;
        if (data.IsMandatoryDropItem &&
            !DropRewardUtil.TryAppendRewards(data.MandatoryDropGroupId, true, multiplier, selected, this)) return false;
        if (!string.IsNullOrWhiteSpace(data.ProbabilityDropGroupId) &&
            !DropRewardUtil.TryAppendRewards(data.ProbabilityDropGroupId, false, multiplier, selected, this)) return false;
        if (selected.Count > 0 && (ObjectManager.Inst == null || !ObjectManager.Inst.IsFieldItemPrefabReady))
        {
            Debug.LogError("벌목 보상 생성에 필요한 매니저/프리팹이 없습니다.", this);
            return false;
        }
        items = selected.ToArray();
        return true;
    }

    private bool ValidateLoggingRewardGroup(string groupId, bool mandatory)
    {
        if (!DropRewardUtil.TryGetRewardGroup(groupId, mandatory, out DropGroupData group, this)) return false;
        // 수량 0이 뽑혀도 잘못된 아이템 참조를 정상 무보상으로 처리하지 않습니다.
        foreach (string id in group.ItemId)
        {
            ItemData item = DataManager.Inst.GetData<ItemData>(id);
            if (item == null || item.MaxStack <= 0)
            {
                Debug.LogError("벌목 보상 아이템 데이터가 올바르지 않습니다: " + id, this);
                return false;
            }
        }
        return true;
    }

    private void ClearLoggingPresentation()
    {
        _pendingLoggingReward = null;
        _lastLoggingImpactSequence = 0;
        _loggingVisualUpdating = false;
        if (_loggingEffect != null) _loggingEffect.ResetPresentation();
        _loggingEffect = null;
        _loggingContent = null;
    }
}
