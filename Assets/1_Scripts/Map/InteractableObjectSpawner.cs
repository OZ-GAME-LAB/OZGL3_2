using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections.Generic;

public class InteractableObjectSpawner : MonoBehaviour
{
    private static readonly HashSet<InteractableObjectSpawner> ActiveSpawners = new();
    private string _spawnedId;
    private bool _depleted;
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { ActiveSpawners.Clear(); }

    private void OnEnable() { ActiveSpawners.Add(this); }
    private void OnDisable() { ActiveSpawners.Remove(this); }

    private void BindSession(NetworkManager network)
    {
        ActiveSpawners.Add(this);
        if (_sessionManager == network) return;
        ResetSession();
        _sessionManager = network;
        _sessionManager.OnServerStopped += OnServerStopped;
    }

    private void ResetSession()
    {
        if (_sessionManager != null) _sessionManager.OnServerStopped -= OnServerStopped;
        ReleaseSpawnedObject(false);
        _sessionManager = null;
        _spawnedId = null;
        _depleted = false;
        _placedSessionInitialized = false;
        _isRespawnPending = true;
        _usesPlacedVisual = false;
        _usesResourceLayout = false;
    }

    public void InitializePlacedPoint(string id, Vector3 position, Quaternion rotation, Vector2 size, Vector2 offset, bool usesResourceLayout = false)
    {
        NetworkManager network = NetworkManager.Singleton;
        if (network == null || !network.IsServer || !network.IsListening)
            return;
        // 세션 정리는 이번 배치 좌표와 크기를 저장하기 전에 처리합니다.
        BindSession(network);
        if (_placedSessionInitialized) return;
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
        ResetSession();
        ActiveSpawners.Remove(this);
    }

    public void SpawnInteractableObject(string spawnedInteractableId)
    {
        var networkManager = Unity.Netcode.NetworkManager.Singleton;

        if (networkManager == null || !networkManager.IsListening || !networkManager.IsServer)
            return;

        BindSession(networkManager);

        if (!_isRespawnPending)
            return;

        if (_spawnedObject != null)
        {
            if (_spawnedObject.ReactivateOnServer())
            {
                _isRespawnPending = false;
                _depleted = false;
            }

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
        _spawnedId = spawnedInteractableId;
        _depleted = false;
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
        _depleted = true;
    }

    // 생성/재활성화 요청의 성공만 반환합니다. 외형의 비동기 IsReady 완료는 호출자가 기다립니다.
    public static bool RespawnDepletedResourcesOnServer(List<InteractionNetwork> restored)
    {
        if (restored == null) throw new ArgumentNullException(nameof(restored));
        restored.Clear();
        NetworkManager network = NetworkManager.Singleton;
        if (network == null || !network.IsListening || !network.IsServer) return false;
        bool success = true;
        var snapshot = new List<InteractableObjectSpawner>(ActiveSpawners);
        foreach (InteractableObjectSpawner spawner in snapshot)
        {
            if (spawner == null || !spawner.isActiveAndEnabled || spawner._sessionManager != network ||
                !spawner._depleted || !spawner._isRespawnPending || string.IsNullOrEmpty(spawner._spawnedId)) continue;
            try
            {
                FieldObjectData data = DataManager.Inst != null ? DataManager.Inst.GetData<FieldObjectData>(spawner._spawnedId) : null;
                if (data == null) throw new InvalidOperationException("휴식 재생성 데이터가 없습니다: " + spawner._spawnedId);
                if (data.InteractionType != "Logging" && data.InteractionType != "Mining" && data.InteractionType != "Gathering") continue;
                spawner.SpawnInteractableObject(spawner._spawnedId);
                if (spawner._isRespawnPending || spawner._spawnedObject == null ||
                    !spawner._spawnedObject.TryGetComponent(out InteractionNetwork resource) || !resource.IsSpawned)
                    throw new InvalidOperationException("휴식 자원을 복원하지 못했습니다: " + spawner._spawnedId);
                restored.Add(resource);
            }
            catch (Exception exception)
            {
                success = false;
                Debug.LogException(exception, spawner);
            }
        }
        return success;
    }

    private void OnDestroy()
    {
        ActiveSpawners.Remove(this);
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
