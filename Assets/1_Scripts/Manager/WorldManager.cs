using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class WorldManager : MonoBehaviour
{
    private readonly Dictionary<string, StageEntryPoint> _stageEntryPointDic = new Dictionary<string, StageEntryPoint>();
    private StageEntryPoint _startSpawnPoint;

    public static WorldManager Inst { get; private set; }

    private void Awake()
    {
        Initialize();
    }

    public bool Initialize()
    {
        if (!isActiveAndEnabled)
        {
            Debug.LogWarning("WorldManager가 활성화되어 있어야 합니다.", this);
            return false;
        }

        if (Inst != null && Inst != this)
        {
            Debug.LogWarning("WorldManager가 중복되어 비활성화합니다.", this);
            enabled = false;
            return false;
        }

        Inst = this;
        return true;
    }

    private void OnDestroy()
    {
        if (Inst == this)
            Inst = null;
    }

    public bool PlacePlayerAtStart(PlayerControllerView player)
    {
        if (player == null)
        {
            Debug.LogWarning("첫 번째 작업 공간에서 활성 플레이어를 찾지 못했습니다.", this);
            return false;
        }

        if (!TryGetStartSpawnPoint(out StageEntryPoint entryPoint))
            return false;

        return player.PlaceAtPosition(
            entryPoint.transform.position,
            entryPoint.FacingDirection == EntryFacingDirection.Right);
    }

    public bool TryGetStartSpawnPoint(out StageEntryPoint entryPoint)
    {
        entryPoint = _startSpawnPoint;
        if (entryPoint != null)
            return true;

        Debug.LogWarning("등록된 시작 지점이 없습니다.", this);
        return false;
    }

    public void RegisterStageEntryPoint(StageEntryPoint stageEntryPoint)
    {
        if (stageEntryPoint == null)
        {
            Debug.LogWarning("Cannot register an empty StageEntryPoint.");
            return;
        }

        string entryPointId = stageEntryPoint.EntryPointId;
        if (string.IsNullOrWhiteSpace(entryPointId))
        {
            Debug.LogWarning($"{stageEntryPoint.name} has no EntryPointId, so it cannot be registered.");
            return;
        }

        if (_stageEntryPointDic.TryGetValue(entryPointId, out StageEntryPoint existingPoint))
        {
            if (existingPoint == stageEntryPoint)
                return;

            Debug.LogWarning($"EntryPoint ID가 중복되어 등록하지 않습니다: {entryPointId}", stageEntryPoint);
            return;
        }

        if (stageEntryPoint.IsStartSpawnPoint && _startSpawnPoint != null)
        {
            Debug.LogWarning("시작 지점은 하나만 지정할 수 있습니다.", stageEntryPoint);
            return;
        }

        _stageEntryPointDic.Add(entryPointId, stageEntryPoint);
        CacheStartSpawnPoint(stageEntryPoint);
    }

    public void UnregisterStageEntryPoint(StageEntryPoint stageEntryPoint)
    {
        if (stageEntryPoint == null || string.IsNullOrEmpty(stageEntryPoint.EntryPointId))
        {
            return;
        }

        if (_stageEntryPointDic.TryGetValue(stageEntryPoint.EntryPointId, out StageEntryPoint registeredPoint) == false)
        {
            return;
        }

        if (registeredPoint != stageEntryPoint)
        {
            return;
        }

        _stageEntryPointDic.Remove(stageEntryPoint.EntryPointId);

        if (_startSpawnPoint == stageEntryPoint)
        {
            _startSpawnPoint = null;
        }
    }

    private void CacheStartSpawnPoint(StageEntryPoint stageEntryPoint)
    {
        if (stageEntryPoint == null || stageEntryPoint.IsStartSpawnPoint == false)
        {
            return;
        }

        _startSpawnPoint = stageEntryPoint;
    }

    public bool TryGetStageEntryPoint(string entryPointId, out StageEntryPoint stageEntryPoint)
    {
        stageEntryPoint = null;

        if (string.IsNullOrWhiteSpace(entryPointId))
        {
            Debug.LogWarning("EntryPointId is empty, so player cannot be moved.");
            return false;
        }

        if (_stageEntryPointDic.TryGetValue(entryPointId, out stageEntryPoint) == false || stageEntryPoint == null)
        {
            Debug.LogWarning($"{entryPointId} StageEntryPoint could not be found.");
            return false;
        }

        return true;
    }

}
