using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public struct PlacedInteractionSettings : INetworkSerializable, IEquatable<PlacedInteractionSettings>
{
    public bool UsesPlacedVisual;
    public bool UsesResourceLayout;
    public Vector2 Size;
    public Vector2 Offset;
    public bool IsValid => !UsesPlacedVisual || (Finite(Size.x) && Finite(Size.y) &&
        Finite(Offset.x) && Finite(Offset.y) && Size.x > 0f && Size.y > 0f);
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref UsesPlacedVisual);
        serializer.SerializeValue(ref UsesResourceLayout);
        serializer.SerializeValue(ref Size);
        serializer.SerializeValue(ref Offset);
    }
    public bool Equals(PlacedInteractionSettings other) => UsesPlacedVisual == other.UsesPlacedVisual &&
        UsesResourceLayout == other.UsesResourceLayout && Size.Equals(other.Size) && Offset.Equals(other.Offset);
}

public partial class InteractionNetwork : NetworkBehaviour
{
    NetworkVariable<bool> _isAvailable = new NetworkVariable<bool>
    (true,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server);

    private NetworkVariable<FixedString128Bytes> _objectId = new(
    default,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server);

    private NetworkVariable<bool> _isDormant = new(
    false,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server);

    private NetworkVariable<ulong> _reservedClientId = new(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private NetworkVariable<PlacedInteractionSettings> _placement = new(default,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private PlacedInteractionSettings _pendingPlacement;
    private bool _localInitialized;
    private int _initializationVersion;
    public bool IsReady => IsSpawned && _localInitialized;
    public bool IsDormant => _isDormant.Value;
    public bool IsAvailable => IsReady && _isAvailable.Value;

    public void SetPlacementBeforeSpawn(PlacedInteractionSettings placement)
    {
        if (IsSpawned || !placement.IsValid) return;
        _pendingPlacement = placement;
    }

    private void ApplyPlacement()
    {
        PlacedInteractionSettings placement = _placement.Value;
        if (!placement.UsesPlacedVisual || placement.UsesResourceLayout) return;
        if (!placement.IsValid) throw new InvalidOperationException("Invalid placed interaction geometry.");
        if (!TryGetComponent(out BoxCollider2D box)) box = gameObject.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = placement.Size;
        box.offset = placement.Offset;
        box.enabled = true;
    }

    public bool IsUsedByOtherPlayer => IsSpawned && IsClient
        && _reservedClientId.Value != ulong.MaxValue
        && _reservedClientId.Value != NetworkManager.LocalClientId;

    public event Action<bool> AvailabilityChanged;

    private FixedString128Bytes _pendingObjectId;

    private sealed class FishingSession
    {
        public readonly ulong RequestId;
        public readonly string FishId;
        public readonly int Quantity;

        public FishingSession(ulong requestId, string fishId, int quantity)
        {
            RequestId = requestId;
            FishId = fishId;
            Quantity = quantity;
        }
    }

    private readonly Dictionary<ulong, FishingSession> _fishingSessions = new();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        _localInitialized = false;
        _initializationVersion++;

        _isAvailable.OnValueChanged += OnAvailabilityChanged;
        _reservedClientId.OnValueChanged += OnReservedClientChanged;
        _isDormant.OnValueChanged += OnDormantChanged;
        _loggingPresentation.OnValueChanged += OnLoggingPresentationChanged;

        if (IsServer)
        {
            if (_loggingPresentation.Value.Phase == LoggingPresentationPhase.Sinking)
            {
                _serverAllowsInteraction = true;
                _isDormant.Value = false;
            }
            ClearLoggingPresentation();
            _loggingPresentation.Value = default;
            _objectId.Value = _pendingObjectId;
            _placement.Value = _pendingPlacement;

            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            RefreshAvailabilityOnServer();
        }

        InitializeObject(_initializationVersion).Forget();
    }

    public override void OnNetworkDespawn()
    {
        _localInitialized = false;
        _initializationVersion++;
        _isAvailable.OnValueChanged -= OnAvailabilityChanged;
        _reservedClientId.OnValueChanged -= OnReservedClientChanged;
        _isDormant.OnValueChanged -= OnDormantChanged;
        _loggingPresentation.OnValueChanged -= OnLoggingPresentationChanged;
        ClearLoggingPresentation();

        if (IsServer)
        {
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;

            _hasInteractor = false;
            _interactorClientId = 0;
            _interactionRequestId = 0;

            _fishingSessions.Clear();
        }

        AvailabilityChanged?.Invoke(false);

        base.OnNetworkDespawn();
    }

    private void OnAvailabilityChanged(bool previous, bool current)
    {
        AvailabilityChanged?.Invoke(IsAvailable);
    }

    private void OnReservedClientChanged(ulong previous, ulong current)
    {
        AvailabilityChanged?.Invoke(IsAvailable);
    }

    public bool TryBeginOnServer(ulong clientId, ulong requestId, out string fishId)
    {
        fishId = string.Empty;

        if (!IsServer || !IsReady)
            return false;

        if (_fishingSessions.TryGetValue(clientId, out FishingSession existingSession))
        {
            if (existingSession.RequestId != requestId)
                return false;

            fishId = existingSession.FishId;
            return true;
        }

        if (_hasInteractor && _interactorClientId == clientId && _interactionRequestId == requestId)
            return true;

        if (_hasInteractor || !_serverAllowsInteraction)
            return false;

        FieldObjectData data = DataManager.Inst.GetData<FieldObjectData>(_objectId.Value.ToString());

        if (data == null)
            return false;

        if (data.InteractionType == "Fishing")
        {
            if (!TryPrepareFishingReward(data, out string selectedFishId, out int quantity))
                return false;

            _fishingSessions.Add(clientId, new FishingSession(requestId, selectedFishId, quantity));
            fishId = selectedFishId;
            Debug.Log($"낚시 시작: 유저 {clientId}, 요청 {requestId}, 물고기 {fishId}, 수량 {quantity}", this);
            return true;
        }

        _hasInteractor = true;
        _interactorClientId = clientId;
        _interactionRequestId = requestId;

        RefreshAvailabilityOnServer();
        return true;
    }

    public bool HasReservationOnServer(ulong clientId, ulong requestId)
    {
        if (!IsServer || !IsReady || !_serverAllowsInteraction) return false;
        if (_fishingSessions.TryGetValue(clientId, out FishingSession session)) return session.RequestId == requestId;
        return _hasInteractor && _interactorClientId == clientId && _interactionRequestId == requestId;
    }

    public bool TryCancelOnServer(ulong clientId, ulong requestId)
    {
        if (!IsServer || !IsSpawned)
            return false;

        if (_fishingSessions.TryGetValue(clientId, out FishingSession session))
        {
            if (session.RequestId != requestId)
                return false;

            _fishingSessions.Remove(clientId);
            return true;
        }

        if (!_hasInteractor || _interactorClientId != clientId || _interactionRequestId != requestId)
            return false;

        ReleaseInteractorOnServer();
        return true;
    }

    public bool TrySetAvailableOnServer(bool isAvailable)
    {
        if (!IsServer || !IsSpawned)
            return false;

        if (isAvailable && _pendingLoggingReward != null) return false;
        _serverAllowsInteraction = isAvailable;
        RefreshAvailabilityOnServer();

        return true;
    }

    private void ReleaseInteractorOnServer()
    {
        CancelFutureLoggingImpact();
        _hasInteractor = false;
        _interactorClientId = 0;
        _interactionRequestId = 0;

        RefreshAvailabilityOnServer();
    }

    private void RefreshAvailabilityOnServer()
    {
        _reservedClientId.Value = _hasInteractor ? _interactorClientId : ulong.MaxValue;
        _isAvailable.Value =
            _serverAllowsInteraction && !_hasInteractor;
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer)
            return;

        _fishingSessions.Remove(clientId);

        if (_hasInteractor && _interactorClientId == clientId)
        {
            ReleaseInteractorOnServer();
        }
    }

    public void SetObjectId(string id)
    {
        var manager = Unity.Netcode.NetworkManager.Singleton;

        if (manager == null || !manager.IsServer)
            return;

        if (IsSpawned)
        {
            Debug.LogWarning("오브젝트 ID는 Spawn 전에 설정해 주세요.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(id))
            return;

        _pendingObjectId = new FixedString128Bytes(id);
    }

    private async UniTask InitializeObject(int version)
    {
        try
        {
            if (!TryGetComponent<InteractableObject>(out var interactableObject) || DataManager.Inst == null)
                throw new InvalidOperationException("Interaction initialization dependencies are missing.");
            ApplyPlacement();
            string id = _objectId.Value.ToString();
            FieldObjectData data = DataManager.Inst.GetData<FieldObjectData>(id);
            if (data == null) throw new InvalidOperationException("Missing field data: " + id);
            interactableObject.ConfigureResourceLayout(data.InteractionType, _placement.Value.UsesResourceLayout, _placement.Value.Size);
            interactableObject.ConfigureFieldPresentation(data.InteractionType, _placement.Value.UsesPlacedVisual);
            interactableObject.SetDestroyAfterUse(data.DestroyAfterUse);
            interactableObject.SetRequiredToolAction(data.RequiredToolAction);
            await interactableObject.InitObject(id);
            if (this == null || !IsSpawned || version != _initializationVersion) return;
            _localInitialized = true;
            InitializeLoggingPresentation(interactableObject);
            ApplyDormantState();
            AvailabilityChanged?.Invoke(IsAvailable);
        }
        catch (Exception exception)
        {
            if (this == null || version != _initializationVersion) return;
            _localInitialized = false;
            if (IsSpawned && IsServer) TrySetAvailableOnServer(false);
            AvailabilityChanged?.Invoke(false);
            Debug.LogException(exception, this);
        }
    }

    public virtual bool CompleteInteraction(ulong clientId, ulong requestId, float accuracy)
    {
        if (!IsServer || !IsSpawned)
            return false;

        if (_fishingSessions.TryGetValue(clientId, out FishingSession session))
        {
            if (session.RequestId != requestId)
                return false;

            _fishingSessions.Remove(clientId);
            Debug.Log($"낚시 완료: 유저 {clientId}, 요청 {requestId}, 물고기 {session.FishId}, 수량 {session.Quantity}", this);
            SpawnRewardsOnServer(1f, 1, session);
            return true;
        }

        if (!_hasInteractor || _interactorClientId != clientId || _interactionRequestId != requestId)
            return false;

        if (!TryGetComponent<InteractableObject>(out var target))
            return false;

        FieldObjectData data = DataManager.Inst.GetData<FieldObjectData>(_objectId.Value.ToString());

        if (data == null)
            return false;

        bool usesAccuracy = data.InteractionType == "Logging" || data.InteractionType == "Mining";

        float rewardMultiplier = 1f;

        if (usesAccuracy)
        {
            if (float.IsNaN(accuracy) || float.IsInfinity(accuracy) || accuracy < 0f || accuracy > 1f)
                return false;

            if (accuracy == 0f)
            {
                ReleaseInteractorOnServer();
                return true;
            }

            rewardMultiplier = 0.5f + accuracy;
        }

        int playerCount = NetworkManager.ConnectedClientsList.Count;
        if (data.InteractionType == "Logging" && target.LoggingEffect != null)
            return TryBeginLoggingCompletion(data, target, rewardMultiplier, playerCount, requestId);

        _serverAllowsInteraction = false;
        ReleaseInteractorOnServer();

        SpawnRewardsOnServer(rewardMultiplier, playerCount);

        if (target.DestroyAfterUse)
        {
            target.NotifyRespawnRequested();
            NetworkObject.Despawn();
        }
        else
        {
            _isDormant.Value = true;
            target.NotifyRespawnRequested();
        }

        return true;
    }

    private void OnDormantChanged(bool previous, bool current)
    {
        ApplyDormantState();
    }

    private void ApplyDormantState()
    {
        if (TryGetComponent<InteractableObject>(out var target))
            target.ApplyDormantState(_isDormant.Value);
    }

    public bool TryReactivateOnServer()
    {
        if (!IsServer || !IsSpawned || !_isDormant.Value || _pendingLoggingReward != null)
            return false;

        _loggingPresentation.Value = default;
        _lastLoggingImpactSequence = 0;
        _isDormant.Value = false;
        _serverAllowsInteraction = true;
        RefreshAvailabilityOnServer();

        return true;
    }

    private void SpawnRewardsOnServer(float rewardMultiplier, int playerCount, FishingSession fishingSession = null)
    {
        if (!IsServer || playerCount <= 0)
            return;

        FieldObjectData objectData = DataManager.Inst.GetData<FieldObjectData>(_objectId.Value.ToString());

        if (objectData == null)
        {
            Debug.LogError("상호작용 오브젝트 데이터를 찾지 못했습니다.", this);
            return;
        }

        List<string> selectedItems = new();

        if (objectData.IsMandatoryDropItem)
        {
            if (!DropRewardUtil.TryAppendRewards(
                    objectData.MandatoryDropGroupId,
                    true,
                    rewardMultiplier,
                    selectedItems, this))
            {
                return;
            }
        }

        if (objectData.InteractionType == "Fishing")
        {
            if (fishingSession == null || string.IsNullOrWhiteSpace(fishingSession.FishId))
            {
                Debug.LogError("낚시 시작 시 결정된 물고기가 없습니다.", this);
                return;
            }

            int quantity = Mathf.FloorToInt(fishingSession.Quantity * rewardMultiplier);

            for (int i = 0; i < quantity; i++)
            {
                selectedItems.Add(fishingSession.FishId);
            }

            Debug.Log($"낚시 보상: {fishingSession.FishId}, 수량 {quantity}개", this);
        }
        else if (!string.IsNullOrWhiteSpace(objectData.ProbabilityDropGroupId))
        {
            if (!DropRewardUtil.TryAppendRewards(objectData.ProbabilityDropGroupId, false, rewardMultiplier, selectedItems, this))
                return;
        }

        DropRewardUtil.SpawnItemsOnServer(selectedItems, playerCount, transform.position, this);
    }

    private bool TryPrepareFishingReward(FieldObjectData objectData, out string fishId, out int quantity)
    {
        fishId = string.Empty;
        quantity = 0;

        if (!DropRewardUtil.TryGetRewardGroup(objectData.ProbabilityDropGroupId, false, out DropGroupData dropData, this))
            return false;

        float totalChance = 0f;

        foreach (float chance in dropData.DropChance)
            totalChance += chance;

        fishId = DropRewardUtil.SelectRewardItem(dropData, totalChance);
        quantity = UnityEngine.Random.Range(dropData.MinQuantity, dropData.MaxQuantity + 1);
        return true;
    }

    //-------------------------------------------------------------------------------
    // 서버 전용
    //-------------------------------------------------------------------------------
    private bool _hasInteractor;
    private ulong _interactorClientId;
    private ulong _interactionRequestId;
    private bool _serverAllowsInteraction = true;
}
