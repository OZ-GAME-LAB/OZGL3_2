using UnityEngine;

public class MonsterSpawner : MonoBehaviour
{
    [SerializeField] private PatrolArea _patrolArea;

    [SerializeField] private string _placedMonsterId;

    public sealed class RestRecovery
    {
        public bool Finished { get; private set; }
        public bool Succeeded { get; private set; }
        internal void Complete(bool success)
        {
            if (Finished) return;
            Succeeded = success;
            Finished = true;
        }
    }

    private MonsterNetwork _placed;
    private bool _placementStarted;
    private bool _defeated;
    private RestRecovery _recovery;
    private RestRecovery _initialization;

    public void SpawnPlacedMonsterOnServer()
    {
        if (_placementStarted || string.IsNullOrEmpty(_placedMonsterId) ||
            Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsServer)
            return;

        _placementStarted = true;
        StartCoroutine(CreateAndObserve(null, double.PositiveInfinity));
    }

    public RestRecovery RestoreDefeatedOnServer(float timeoutSeconds)
    {
        if (!isActiveAndEnabled || Unity.Netcode.NetworkManager.Singleton == null ||
            !Unity.Netcode.NetworkManager.Singleton.IsServer) return null;
        if (_recovery != null && !_recovery.Finished) return _recovery;
        if (!_defeated) return null;

        _recovery = new RestRecovery();
        StartCoroutine(RestoreAfterRemoval(_recovery, timeoutSeconds));
        return _recovery;
    }

    private System.Collections.IEnumerator RestoreAfterRemoval(RestRecovery recovery, float timeoutSeconds)
    {
        while (_placed != null && _placed.IsSpawned) yield return null;
        yield return CreateAndObserve(recovery, Time.realtimeSinceStartupAsDouble + timeoutSeconds);
    }

    private System.Collections.IEnumerator CreateAndObserve(RestRecovery recovery, double deadline)
    {
        var attempt = recovery ?? new RestRecovery();
        _initialization = attempt;
        MonsterNetwork instance = null;
        try
        {
            Monster monster = CreateMonster(_placedMonsterId);
            if (monster != null) instance = monster.GetComponent<MonsterNetwork>();
        }
        catch (System.Exception exception) { Debug.LogException(exception, this); }

        if (instance != null && instance.IsSpawned)
        {
            _placed = instance;
            _defeated = false;
            instance.Died += OnPlacedDeath;
            instance.Despawned += OnPlacedDespawn;
            while (instance != null && instance.IsSpawned && !instance.IsReady && !attempt.Finished &&
                Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;

            if (attempt.Succeeded || (instance != null && instance.IsReady))
            {
                attempt.Complete(true);
                yield break;
            }

            if (recovery != null && instance != null && instance == _placed && instance.IsSpawned)
            {
                Debug.LogWarning($"Monster recovery timed out at {name}: {_placedMonsterId}", this);
                instance.NetworkObject.Despawn();
            }
        }

        _defeated = recovery != null;
        _placementStarted = _defeated;
        attempt.Complete(false);
        Debug.LogWarning($"Monster initialization failed at {name}: {_placedMonsterId}", this);
    }

    private void OnPlacedDeath(MonsterNetwork monster)
    {
        if (monster != _placed) return;
        _defeated = true;
        // Combat can kill the replacement before the initialization observer resumes.
        _initialization?.Complete(true);
    }

    private void OnPlacedDespawn(MonsterNetwork monster)
    {
        if (monster != _placed) return;
        DetachPlaced();
    }

    private void DetachPlaced()
    {
        if (_placed != null)
        {
            _placed.Died -= OnPlacedDeath;
            _placed.Despawned -= OnPlacedDespawn;
        }
        _placed = null;
    }

    private void OnDestroy()
    {
        DetachPlaced();
        _recovery?.Complete(false);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Retry placed monster initialization")]
    private void RetryPlacement() => SpawnPlacedMonsterOnServer();
#endif

    public void SpawnMonster(string monsterId) => CreateMonster(monsterId);

    private Monster CreateMonster(string monsterId)
    {
        var networkManager = Unity.Netcode.NetworkManager.Singleton;

        if (networkManager == null ||
            !networkManager.IsListening ||
            !networkManager.IsServer)
            return null;

        if (string.IsNullOrWhiteSpace(monsterId))
        {
            Debug.LogWarning("생성할 몬스터 ID가 없습니다.", this);
            return null;
        }

        if (ObjectManager.Inst == null)
        {
            Debug.LogWarning("ObjectManager가 준비되지 않았습니다.", this);
            return null;
        }

        return ObjectManager.Inst.SpawnMonster(
            monsterId,
            _patrolArea,
            transform.position);
    }
}
