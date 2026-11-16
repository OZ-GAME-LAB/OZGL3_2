using UnityEngine;
using Unity.Netcode;

public class InteractableObjectSpawner : MonoBehaviour
{
    private bool _isRespawnPending = true;
    private InteractableObject _spawnedObject;
    private NetworkManager _sessionManager;
    private bool _placedSessionInitialized;
    private bool _usesPlacedVisual;
    private bool _usesResourceLayout;
    private Vector3 _placedPosition;
    private Quaternion _placedRotation;
    private Vector2 _placedSize;
    private Vector2 _placedOffset;

    public InteractableObject SpawnedObject => _spawnedObject;

    public void InitializePlacedPoint(string id, Vector3 position, Quaternion rotation, Vector2 size, Vector2 offset, bool usesResourceLayout = false)
    {
        NetworkManager network = NetworkManager.Singleton;
        if (network == null || !network.IsServer || !network.IsListening || _placedSessionInitialized)
            return;
        if (_sessionManager != network)
        {
            if (_sessionManager != null) _sessionManager.OnServerStopped -= OnServerStopped;
            _sessionManager = network;
            _sessionManager.OnServerStopped += OnServerStopped;
        }
        _usesPlacedVisual = true;
        _usesResourceLayout = usesResourceLayout;
        _placedPosition = position;
        _placedRotation = rotation;
        _placedSize = size;
        _placedOffset = offset;
        SpawnInteractableObject(id);
        _placedSessionInitialized = _spawnedObject != null;
    }

    private void OnServerStopped(bool wasHost)
    {
        ReleaseSpawnedObject(false);
        _placedSessionInitialized = false;
        _isRespawnPending = true;
    }

    public void SpawnInteractableObject(string spawnedInteractableId)
    {
        var networkManager = Unity.Netcode.NetworkManager.Singleton;

        if (networkManager == null || !networkManager.IsListening || !networkManager.IsServer)
            return;

        if (!_isRespawnPending)
            return;

        if (_spawnedObject != null)
        {
            if (_spawnedObject.ReactivateOnServer())
                _isRespawnPending = false;

            return;
        }

        if (string.IsNullOrWhiteSpace(spawnedInteractableId))
        {
            Debug.LogError("생성할 상호작용 오브젝트 ID가 없습니다.");
            return;
        }

        if (ObjectManager.Inst == null) return;
        _spawnedObject = _usesPlacedVisual
            ? ObjectManager.Inst.SpawnPlacedInteractionObject(spawnedInteractableId,
                _placedPosition, _placedRotation, _placedSize, _placedOffset, _usesResourceLayout)
            : ObjectManager.Inst.SpawnInteractionObject(spawnedInteractableId, transform.position, transform.rotation);

        if (_spawnedObject == null)
            return;

        _spawnedObject.RespawnRequested += OnRespawnRequested;
        _isRespawnPending = false;
    }

    private void OnRespawnRequested()
    {
        if (_spawnedObject == null)
            return;

        if (_spawnedObject.DestroyAfterUse)
        {
            _spawnedObject.RespawnRequested -= OnRespawnRequested;
            _spawnedObject = null;
        }

        _isRespawnPending = true;
    }

    private void OnDestroy()
    {
        if (_sessionManager != null) _sessionManager.OnServerStopped -= OnServerStopped;
        ReleaseSpawnedObject(_usesPlacedVisual);
    }

    private void ReleaseSpawnedObject(bool despawn)
    {
        if (_spawnedObject != null)
        {
            _spawnedObject.RespawnRequested -= OnRespawnRequested;
            if (despawn && _spawnedObject.TryGetComponent(out NetworkObject networkObject) &&
                networkObject.IsSpawned && _sessionManager != null && _sessionManager.IsServer)
                networkObject.Despawn();
        }
        _spawnedObject = null;
    }
}
