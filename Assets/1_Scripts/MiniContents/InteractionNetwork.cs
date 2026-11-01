using Cysharp.Threading.Tasks;
using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class InteractionNetwork : NetworkBehaviour
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

    public bool IsAvailable => IsSpawned && _isAvailable.Value;

    public event Action<bool> AvailabilityChanged;

    

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        _isAvailable.OnValueChanged += OnAvailabilityChanged;

        if (IsServer)
        {
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;

            RefreshAvailabilityOnServer();
        }

        InitializeObject().Forget();
    }

    public override void OnNetworkDespawn()
    {
        _isAvailable.OnValueChanged -= OnAvailabilityChanged;

        if (IsServer)
        {
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;

            _hasInteractor = false;
            _interactorClientId = 0;
            _interactionRequestId = 0;
        }

        AvailabilityChanged?.Invoke(false);

        base.OnNetworkDespawn();
    }

    private void OnAvailabilityChanged(bool previous, bool current)
    {
        AvailabilityChanged?.Invoke(current);
    }

    public bool TryBeginOnServer(ulong clientId, ulong requestId)
    {
        if (!IsServer || !IsSpawned)
            return false;

        if (_hasInteractor &&
            _interactorClientId == clientId &&
            _interactionRequestId == requestId)
        {
            return true;
        }

        if (_hasInteractor || !_serverAllowsInteraction)
            return false;

        _hasInteractor = true;
        _interactorClientId = clientId;
        _interactionRequestId = requestId;

        RefreshAvailabilityOnServer();
        return true;
    }

    public bool TryCancelOnServer(ulong clientId, ulong requestId)
    {
        if (!IsServer || !IsSpawned || !_hasInteractor)
            return false;

        if (_interactorClientId != clientId || _interactionRequestId != requestId)
        {
            return false;
        }

        ReleaseInteractorOnServer();
        return true;
    }

    public bool TrySetAvailableOnServer(bool isAvailable)
    {
        if (!IsServer || !IsSpawned)
            return false;

        _serverAllowsInteraction = isAvailable;
        RefreshAvailabilityOnServer();

        return true;
    }

    private void ReleaseInteractorOnServer()
    {
        _hasInteractor = false;
        _interactorClientId = 0;
        _interactionRequestId = 0;

        RefreshAvailabilityOnServer();
    }

    private void RefreshAvailabilityOnServer()
    {
        _isAvailable.Value =
            _serverAllowsInteraction && !_hasInteractor;
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (IsServer && _hasInteractor && _interactorClientId == clientId)
        {
            ReleaseInteractorOnServer();
        }
    }

    public void SetObjectId(string id)
    {
        var manager = Unity.Netcode.NetworkManager.Singleton;

        if (manager == null || !manager.IsServer)
            return;

        if (string.IsNullOrWhiteSpace(id))
            return;

        _objectId.Value = new FixedString128Bytes(id);
    }

    private async UniTask InitializeObject()
    {
        if (!TryGetComponent<InteractableObject>(out var interactableObject))
            return;

        string id = _objectId.Value.ToString();

        await interactableObject.InitObject(id);

        if (!IsSpawned)
            return;

        AvailabilityChanged?.Invoke(IsAvailable);
    }

    public virtual bool CompleteInteraction(ulong clientId, ulong requestId)
    {
        if (!IsServer || !IsSpawned || !_hasInteractor)
            return false;

        if (_interactorClientId != clientId || _interactionRequestId != requestId)
            return false;

        _serverAllowsInteraction = false;
        ReleaseInteractorOnServer();

        SpawnRewardsOnServer();

        NetworkObject.Despawn();
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
        if (!IsServer || !IsSpawned || !_isDormant.Value)
            return false;

        _isDormant.Value = false;
        _serverAllowsInteraction = true;
        RefreshAvailabilityOnServer();

        return true;
    }

    private void SpawnRewardsOnServer()
    {
        if (!IsServer)
            return;

        FieldObjectData objectData = DataManager.Inst.GetData<FieldObjectData>(_objectId.Value.ToString());

        if (objectData == null || string.IsNullOrWhiteSpace(objectData.DropGroupId))
        {
            Debug.LogError("상호작용 오브젝트의 보상 그룹을 확인해 주세요.", this);
            return;
        }

        DropGroupData dropData = DataManager.Inst.GetData<DropGroupData>(objectData.DropGroupId);

        if (dropData == null)
            return;

        if (dropData.ItemId == null || dropData.DropChance == null || dropData.ItemId.Length == 0 || dropData.ItemId.Length != dropData.DropChance.Length)
        {
            Debug.LogError("아이템 목록과 확률 배열의 길이를 확인해 주세요.", this);
            return;
        }

        if (dropData.MinQuantity < 0 || dropData.MaxQuantity < dropData.MinQuantity || dropData.MaxQuantity == int.MaxValue)
        {
            Debug.LogError("보상 최소·최대 수량을 확인해 주세요.", this);
            return;
        }

        float totalChance = 0f;

        foreach (float chance in dropData.DropChance)
        {
            if (float.IsNaN(chance) || float.IsInfinity(chance) || chance < 0f)
            {
                Debug.LogError("드롭 확률은 0 이상의 유효한 숫자여야 합니다.", this);
                return;
            }

            totalChance += chance;
        }

        if (!Mathf.Approximately(totalChance, 1f))
        {
            Debug.LogError($"드롭 확률의 합이 1이 아닙니다: {totalChance}", this);
            return;
        }

        int quantity = UnityEngine.Random.Range(dropData.MinQuantity, dropData.MaxQuantity + 1);

        for (int i = 0; i < quantity; i++)
        {
            string itemId = SelectRewardItem(dropData, totalChance);

            var item = ObjectManager.Inst.SpawnItem(itemId, 1, transform.position);

            if (item == null)
            {
                Debug.LogError($"보상 아이템 생성 실패: {itemId}", this);
            }
        }
    }

    private string SelectRewardItem(DropGroupData dropData, float totalChance)
    {
        float roll = UnityEngine.Random.value * totalChance;
        float accumulatedChance = 0f;
        int lastPositiveIndex = -1;

        for (int i = 0; i < dropData.ItemId.Length; i++)
        {
            float chance = dropData.DropChance[i];

            if (chance <= 0f)
                continue;

            lastPositiveIndex = i;
            accumulatedChance += chance;

            if (roll < accumulatedChance)
                return dropData.ItemId[i];
        }

        return dropData.ItemId[lastPositiveIndex];
    }

    //-------------------------------------------------------------------------------
    // 서버 전용
    //-------------------------------------------------------------------------------
    private bool _hasInteractor;
    private ulong _interactorClientId;
    private ulong _interactionRequestId;
    private bool _serverAllowsInteraction = true;
}
