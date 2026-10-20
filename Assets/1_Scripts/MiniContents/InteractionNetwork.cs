using Cysharp.Threading.Tasks;
using System;
using Unity.Collections;
using Unity.Netcode;

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

        // TODO : ObjectManager를 통해 아이템 스폰 부분 추가 필요

        NetworkObject.Despawn();
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
