using Unity.Cinemachine;
using Unity.Netcode.Components;
using UnityEngine;

public partial class PlayerControllerView
{
    private bool _deathControlLocked;
    private bool _deathWasSimulated;
    public bool IsDeathControlLocked => _deathControlLocked;
    // 화면 전환은 이동을 허용하지만 공격·상호작용·메뉴는 계속 막습니다.
    private bool IsMovementControlLocked => IsDeathControlLocked || IsEncounterControlLocked;
    private bool IsGameplayControlLocked => IsMovementControlLocked || IsScreenTransitioning;
    public bool CanPlaceForRespawn => _rigidbody2D != null && _groundCheck != null &&
        _networkObject != null && _networkObject.IsSpawned;

    public void SetDeathControlLocked(bool locked)
    {
        if (_deathControlLocked == locked) return;
        if (locked && (_networkObject == null || !_networkObject.IsSpawned || !_networkObject.IsOwner)) return;
        if (locked) CancelScreenTransition();
        _deathControlLocked = locked;

        if (locked)
        {
            _interactionController?.CancelRequestedInteraction();
            _scenarioService?.Cancel();
            ExitMiniGameMode();
            ExitDialogueMode();
            if (_isSpectatorMode) SetSpectatorMode(false);
            SetEncounterControlLocked(false);
            _restoreInputAfterHit = false;
            _hitControlLockRemaining = 0f;
            _runeAttackController?.CancelAttackInput();
            SetMoveInput(0f);
            ResetAirborneStateAndAnimation();
            RestoreThinPlatformCollisions();
            if (_rigidbody2D != null)
            {
                _deathWasSimulated = _rigidbody2D.simulated;
                _rigidbody2D.linearVelocity = Vector2.zero;
                _rigidbody2D.angularVelocity = 0f;
                _rigidbody2D.simulated = false;
            }
            CloseDeathConflictingUI(UIType.InventoryUI);
            CloseDeathConflictingUI(UIType.RuneInventoryUI);
            CloseDeathConflictingUI(UIType.CraftingUI);
            CloseDeathConflictingUI(UIType.SettingUI);
            MaintainDeathInput();
        }
        else
        {
            SetMoveInput(0f);
            if (_rigidbody2D != null) _rigidbody2D.simulated = _deathWasSimulated;
            if (_playerInput != null && _playerInput.isActiveAndEnabled &&
                _networkObject != null && _networkObject.IsSpawned && _networkObject.IsOwner &&
                !IsEncounterControlLocked && !_isMiniGameMode && !_isDialogueMode)
            {
                _playerInput.SwitchCurrentActionMap("Player");
                _playerInput.ActivateInput();
            }
        }
        UpdateMovementAnimation();
    }

    private void MaintainDeathInput()
    {
        if (!_deathControlLocked || _playerInput == null || !_playerInput.isActiveAndEnabled) return;
        // 이전 상호작용의 늦은 종료 콜백이 입력 맵을 복원해도 사망 잠금이 우선합니다.
        if (_playerInput.currentActionMap == null || _playerInput.currentActionMap.name != "UI")
            _playerInput.SwitchCurrentActionMap("UI");
        if (!_playerInput.inputIsActive) _playerInput.ActivateInput();
    }

    private void CloseDeathConflictingUI(UIType type)
    {
        UIManager manager = UIManager.Inst;
        if (manager != null && manager.IsOpen(type)) manager.Close(type);
    }

    public bool PlaceForRespawn(Vector3 footPosition, bool faceRight)
    {
        if (!CanPlaceForRespawn) return false;
        Vector3 previousPosition = transform.position;
        SetMoveInput(0f);
        _hitControlLockRemaining = 0f;
        _restoreInputAfterHit = false;
        SetCrouching(false);
        if (!PlaceAtPosition(footPosition, faceRight)) return false;
        if (TryGetComponent(out NetworkTransform networkTransform) && networkTransform.CanCommitToTransform)
            networkTransform.Teleport(transform.position, transform.rotation, transform.localScale);
        if (_networkObject.IsOwner)
        {
            CinemachineCamera camera = GetComponentInChildren<CinemachineCamera>(true);
            if (camera != null)
            {
                camera.OnTargetObjectWarped(transform, transform.position - previousPosition);
                camera.PreviousStateIsValid = false;
            }
        }
        UpdateMovementAnimation();
        return true;
    }
}
