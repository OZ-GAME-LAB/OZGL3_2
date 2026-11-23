using System;
using System.Collections.Generic;
using Unity.Netcode;

public partial class PlayerNetworkStats
{
    private readonly Dictionary<string, double> _consumableReadyAt = new();
    private uint _nextConsumableRequest;
    private uint _pendingConsumableRequest;
    private uint _lastServerConsumableRequest;
    private Action<bool> _consumableCompleted;

    private void InitializeConsumableUse()
    {
        _consumableReadyAt.Clear();
        _nextConsumableRequest = 0;
        _pendingConsumableRequest = 0;
        _lastServerConsumableRequest = 0;
        _consumableCompleted = null;
    }

    private void ReleaseConsumableUse()
    {
        CompleteConsumableUse(false);
    }

    // 소유 클라이언트는 아이템 ID만 요청하고, 효과 수치는 서버의 데이터에서 읽습니다.
    public bool RequestUseConsumable(string itemId, Action<bool> onCompleted)
    {
        if (!IsSpawned || !IsOwner || !IsClient || _consumableCompleted != null
            || string.IsNullOrWhiteSpace(itemId) || onCompleted == null)
            return false;

        _pendingConsumableRequest = ++_nextConsumableRequest;
        _consumableCompleted = onCompleted;
        UseConsumableServerRpc(_pendingConsumableRequest, itemId);
        return true;
    }

    [ServerRpc]
    private void UseConsumableServerRpc(uint requestId, string itemId, ServerRpcParams rpcParams = default)
    {
        if (!IsServer || !IsSpawned || rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        bool applied = false;
        if (requestId > _lastServerConsumableRequest)
        {
            _lastServerConsumableRequest = requestId;
            applied = TryApplyConsumableOnServer(itemId);
        }

        ClientRpcParams parameters = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        };
        CompleteConsumableClientRpc(requestId, applied, parameters);
    }

    private bool TryApplyConsumableOnServer(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || !IsAlive || IsEncounterLocked
            || DataManager.Inst == null
            || !DataManager.Inst.ReadData<ItemData>().ContainsKey(itemId)
            || !DataManager.Inst.ReadData<ConsumableData>().TryGetValue(itemId, out ConsumableData data)
            || data == null || data.EffectType != nameof(ConsumableEffectType.RestoreHp))
            return false;

        double now = NetworkManager.ServerTime.Time;
        if (float.IsNaN(data.CooldownSec) || float.IsInfinity(data.CooldownSec) || data.CooldownSec < 0f ||
            (_consumableReadyAt.TryGetValue(itemId, out double readyAt) && now < readyAt))
            return false;

        if (!HealOnServer(data.EffectValue))
            return false;

        _consumableReadyAt[itemId] = now + data.CooldownSec;
        return true;
    }

    [ClientRpc]
    private void CompleteConsumableClientRpc(uint requestId, bool applied, ClientRpcParams rpcParams = default)
    {
        if (!IsOwner || _consumableCompleted == null || requestId != _pendingConsumableRequest)
            return;

        CompleteConsumableUse(applied);
    }

    private void CompleteConsumableUse(bool applied)
    {
        Action<bool> completed = _consumableCompleted;
        _consumableCompleted = null;
        _pendingConsumableRequest = 0;
        completed?.Invoke(applied);
    }
}
