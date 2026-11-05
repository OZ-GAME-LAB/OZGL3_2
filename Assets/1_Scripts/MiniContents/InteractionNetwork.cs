using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
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

    private FixedString128Bytes _pendingObjectId;

    private string _selectedFishId;
    private int _selectedFishQuantity;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        _isAvailable.OnValueChanged += OnAvailabilityChanged;
        _isDormant.OnValueChanged += OnDormantChanged;

        if (IsServer)
        {
            _objectId.Value = _pendingObjectId;

            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            RefreshAvailabilityOnServer();
        }

        InitializeObject().Forget();
    }

    public override void OnNetworkDespawn()
    {
        _isAvailable.OnValueChanged -= OnAvailabilityChanged;
        _isDormant.OnValueChanged -= OnDormantChanged;

        if (IsServer)
        {
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;

            _hasInteractor = false;
            _interactorClientId = 0;
            _interactionRequestId = 0;

            _selectedFishId = null;
            _selectedFishQuantity = 0;
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

        if (_hasInteractor && _interactorClientId == clientId && _interactionRequestId == requestId)
            return true;

        if (_hasInteractor || !_serverAllowsInteraction)
            return false;

        FieldObjectData data = DataManager.Inst.GetData<FieldObjectData>(_objectId.Value.ToString());

        if (data == null)
            return false;

        _selectedFishId = null;
        _selectedFishQuantity = 0;

        if (data.InteractionType == "Fishing")
        {
            if (!TryPrepareFishingReward(data))
                return false;
        }

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

        _selectedFishId = null;
        _selectedFishQuantity = 0;

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

        if (IsSpawned)
        {
            Debug.LogWarning("오브젝트 ID는 Spawn 전에 설정해 주세요.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(id))
            return;

        _pendingObjectId = new FixedString128Bytes(id);
    }

    private async UniTask InitializeObject()
    {
        if (!TryGetComponent<InteractableObject>(out var interactableObject))
            return;

        string id = _objectId.Value.ToString();

        FieldObjectData data = DataManager.Inst.GetData<FieldObjectData>(id);

        if (data == null)
            return;

        interactableObject.SetDestroyAfterUse(data.DestroyAfterUse);

        await interactableObject.InitObject(id);

        if (!IsSpawned)
            return;

        ApplyDormantState();
        AvailabilityChanged?.Invoke(IsAvailable);
    }

    public virtual bool CompleteInteraction(ulong clientId, ulong requestId, float accuracy)
    {
        if (!IsServer || !IsSpawned || !_hasInteractor)
            return false;

        if (_interactorClientId != clientId || _interactionRequestId != requestId)
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

        if (data.InteractionType == "Fishing")
        {
            SpawnRewardsOnServer(rewardMultiplier, playerCount);

            ReleaseInteractorOnServer();
            return true;
        }

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
        if (!IsServer || !IsSpawned || !_isDormant.Value)
            return false;

        _isDormant.Value = false;
        _serverAllowsInteraction = true;
        RefreshAvailabilityOnServer();

        return true;
    }

    private void SpawnRewardsOnServer(float rewardMultiplier, int playerCount)
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
            if (!TryAppendRewards(
                    objectData.MandatoryDropGroupId,
                    true,
                    rewardMultiplier,
                    selectedItems))
            {
                return;
            }
        }

        if (objectData.InteractionType == "Fishing")
        {
            if (string.IsNullOrWhiteSpace(_selectedFishId))
            {
                Debug.LogError("낚시 시작 시 결정된 물고기가 없습니다.", this);
                return;
            }

            int quantity = Mathf.FloorToInt(_selectedFishQuantity * rewardMultiplier);

            for (int i = 0; i < quantity; i++)
            {
                selectedItems.Add(_selectedFishId);
            }

            Debug.Log($"낚시 보상: {_selectedFishId}, 기본 보상 {quantity}개, 인원 {playerCount}명", this);
        }
        else if (!string.IsNullOrWhiteSpace(objectData.ProbabilityDropGroupId))
        {
            if (!TryAppendRewards(objectData.ProbabilityDropGroupId, false, rewardMultiplier, selectedItems))
                return;
        }

        foreach (string itemId in selectedItems)
        {
            for (int i = 0; i < playerCount; i++)
            {
                var item = ObjectManager.Inst.SpawnItem(itemId, 1, transform.position);

                if (item == null)
                {
                    Debug.LogError($"보상 아이템 생성 실패: {itemId}", this);
                }
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

    private bool TryAppendRewards(string groupId, bool isMandatory, float rewardMultiplier, List<string> selectedItems)
    {
        if (!TryGetRewardGroup(groupId, isMandatory, out DropGroupData dropData))
        {
            return false;
        }

        if (isMandatory)
        {
            foreach (string itemId in dropData.ItemId)
            {
                int baseQuantity = UnityEngine.Random.Range(dropData.MinQuantity, dropData.MaxQuantity + 1);

                int quantity = Mathf.FloorToInt(baseQuantity * rewardMultiplier);

                for (int i = 0; i < quantity; i++)
                {
                    selectedItems.Add(itemId);
                }
            }
        }
        else
        {
            int baseQuantity = UnityEngine.Random.Range(dropData.MinQuantity, dropData.MaxQuantity + 1);

            int quantity = Mathf.FloorToInt(baseQuantity * rewardMultiplier);

            float totalChance = 0f;

            foreach (float chance in dropData.DropChance)
            {
                totalChance += chance;
            }

            for (int i = 0; i < quantity; i++)
            {
                selectedItems.Add(SelectRewardItem(dropData, totalChance));
            }
        }

        return true;
    }

    private bool TryGetRewardGroup(
    string groupId,
    bool isMandatory,
    out DropGroupData dropData)
    {
        dropData = null;

        if (string.IsNullOrWhiteSpace(groupId))
        {
            Debug.LogError("필수 드롭 그룹 ID가 비어 있습니다.", this);
            return false;
        }

        dropData = DataManager.Inst.GetData<DropGroupData>(groupId);

        if (dropData == null)
        {
            Debug.LogError(
                $"드롭 그룹을 찾지 못했습니다: {groupId}", this);
            return false;
        }

        if (dropData.ItemId == null || dropData.ItemId.Length == 0)
        {
            Debug.LogError(
                $"드롭 그룹의 아이템 목록이 비어 있습니다: {groupId}", this);
            return false;
        }

        foreach (string itemId in dropData.ItemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                Debug.LogError(
                    $"드롭 그룹에 빈 아이템 ID가 있습니다: {groupId}", this);
                return false;
            }
        }

        if (dropData.MinQuantity < 0 ||
            dropData.MaxQuantity < dropData.MinQuantity ||
            dropData.MaxQuantity == int.MaxValue)
        {
            Debug.LogError(
                $"드롭 수량 범위를 확인해 주세요: {groupId}", this);
            return false;
        }

        // 필수 그룹은 확률을 사용하지 않음.
        if (isMandatory)
            return true;

        if (dropData.DropChance == null ||
            dropData.ItemId.Length != dropData.DropChance.Length)
        {
            Debug.LogError(
                $"아이템 목록과 확률 배열의 길이가 다릅니다: {groupId}", this);
            return false;
        }

        float totalChance = 0f;

        foreach (float chance in dropData.DropChance)
        {
            if (float.IsNaN(chance) || float.IsInfinity(chance) || chance < 0f || chance > 1f)
            {
                Debug.LogError($"드롭 확률은 0~1의 유효한 숫자여야 합니다: {groupId}", this);
                return false;
            }
            totalChance += chance;
        }

        if (!Mathf.Approximately(totalChance, 1f))
        {
            Debug.LogError($"드롭 확률 합계가 1이 아닙니다: {groupId}, 합계: {totalChance}", this);
            return false;
        }

        return true;
    }

    private bool TryPrepareFishingReward(FieldObjectData objectData)
    {
        if (!TryGetRewardGroup(objectData.ProbabilityDropGroupId, false, out DropGroupData dropData))
            return false;

        float totalChance = 0f;

        foreach (float chance in dropData.DropChance)
            totalChance += chance;

        _selectedFishId = SelectRewardItem(dropData, totalChance);

        _selectedFishQuantity = UnityEngine.Random.Range(dropData.MinQuantity, dropData.MaxQuantity + 1);

        Debug.Log($"낚시 시작: 물고기 {_selectedFishId}, 기본 수량 {_selectedFishQuantity}", this);

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
