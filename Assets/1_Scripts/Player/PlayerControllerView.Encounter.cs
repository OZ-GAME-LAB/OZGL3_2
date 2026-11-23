using Unity.Cinemachine;
using Unity.Netcode.Components;
using UnityEngine;

public partial class PlayerControllerView
{
    private bool _encounterLocalLocked;
    private bool _encounterConfirmationInput;
    private bool _encounterStartedWhileDead;
    internal bool HasLocalEncounterLock => _encounterLocalLocked;
    private string _encounterPreviousMap;
    private PlayerNetworkStats _encounterStats;
    private RigidbodyConstraints2D _encounterPreviousConstraints;
    public bool IsEncounterControlLocked => _encounterLocalLocked ||
        (_encounterStats != null && _encounterStats.IsEncounterLocked);
    public Vector3 EncounterFootPosition => _groundCheck != null ? _groundCheck.position : transform.position;

    // 연출이 소유한 잠금만 해제하며 대화/피격 잠금은 별도로 유지합니다.
    public void SetEncounterControlLocked(bool locked)
    {
        if (locked) RestTent.Active?.CancelLocalForPlayer(this);
        if (locked) CancelScreenTransition();
        if (_encounterStats == null) TryGetComponent(out _encounterStats);
        if (_encounterLocalLocked == locked) return;
        _encounterLocalLocked = locked;
        if (locked)
        {
            _encounterStartedWhileDead = IsDeathControlLocked;
            _interactionController?.CancelRequestedInteraction();
            _runeAttackController?.CancelAttackInput();
            SetMoveInput(0f);
            ResetRunUp();
            _wallCling?.ResetState();
            if (_rigidbody2D != null)
            {
                _rigidbody2D.linearVelocity = Vector2.zero;
                _encounterPreviousConstraints = _rigidbody2D.constraints;
                _rigidbody2D.constraints = RigidbodyConstraints2D.FreezeAll;
            }
            if (_playerInput != null && _playerInput.isActiveAndEnabled && !_isDialogueMode && !_isMiniGameMode)
            {
                _encounterPreviousMap = _playerInput.currentActionMap?.name;
                _playerInput.SwitchCurrentActionMap("MiniGame");
            }
        }
        else
        {
            if (_rigidbody2D != null) _rigidbody2D.constraints = _encounterPreviousConstraints;
            if (_playerInput != null && _playerInput.isActiveAndEnabled && !_isDialogueMode && !_isMiniGameMode &&
                (_playerInput.currentActionMap?.name == "MiniGame" ||
                    (_encounterConfirmationInput && _playerInput.currentActionMap?.name == "UI")) &&
                !string.IsNullOrEmpty(_encounterPreviousMap))
            {
                string restoreMap = _encounterPreviousMap;
                if (_encounterStartedWhileDead && !IsDeathControlLocked && restoreMap == "UI")
                    restoreMap = "Player";
                _playerInput.SwitchCurrentActionMap(restoreMap);
            }
            _encounterPreviousMap = null;
            _encounterConfirmationInput = false;
            _encounterStartedWhileDead = false;
        }
    }

    public void SetEncounterConfirmationInput()
    {
        if (!_encounterLocalLocked || _playerInput == null || !_playerInput.isActiveAndEnabled ||
            _isDialogueMode || _isMiniGameMode) return;
        _encounterConfirmationInput = true;
        _playerInput.SwitchCurrentActionMap("UI");
        _playerInput.ActivateInput();
    }

    public bool PlaceForEncounter(Vector3 footPosition, bool faceRight)
    {
        if (_networkObject == null || !_networkObject.IsSpawned || !_networkObject.IsOwner) return false;
        Vector3 before = transform.position;
        if (!PlaceAtPosition(footPosition, faceRight)) return false;
        if (TryGetComponent(out NetworkTransform networkTransform))
            networkTransform.Teleport(transform.position, transform.rotation, transform.localScale);
        CinemachineCamera camera = GetComponentInChildren<CinemachineCamera>(true);
        if (camera != null)
        {
            camera.OnTargetObjectWarped(transform, transform.position - before);
            camera.PreviousStateIsValid = false;
        }
        return true;
    }
}
