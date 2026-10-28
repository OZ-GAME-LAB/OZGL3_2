using UnityEngine;

public class InteractableObjectSpawner : MonoBehaviour
{
    private bool _isRespawnPending = true;
    private InteractableObject _spawnedObject;

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

        _spawnedObject = ObjectManager.Inst.SpawnInteractionObject(
            spawnedInteractableId,
            transform.position,
            transform.rotation);

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
        if (_spawnedObject != null)
            _spawnedObject.RespawnRequested -= OnRespawnRequested;
    }
}
