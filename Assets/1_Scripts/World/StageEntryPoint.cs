using UnityEngine;


public enum EntryFacingDirection
{
    Right,
    Left
}

public class StageEntryPoint : MonoBehaviour
{
    [Header("진입 지점 설정")]
    [SerializeField] private string _entryPointId;
    [SerializeField] private bool _isStartSpawnPoint;
    [SerializeField] private EntryFacingDirection _facingDirection = EntryFacingDirection.Right;

    public string EntryPointId => _entryPointId;

    public EntryFacingDirection FacingDirection => _facingDirection;
    public bool IsStartSpawnPoint => _isStartSpawnPoint;

    private void OnEnable()
    {
        if (WorldManager.Inst == null)
        {
            Debug.LogWarning("WorldManager를 먼저 준비한 뒤 EntryPoint를 활성화해 주세요.", this);
            return;
        }

        WorldManager.Inst.RegisterStageEntryPoint(this);
    }

    private void OnDisable()
    {
        if (WorldManager.Inst == null)
        {
            return;
        }

        WorldManager.Inst.UnregisterStageEntryPoint(this);
    }
}

