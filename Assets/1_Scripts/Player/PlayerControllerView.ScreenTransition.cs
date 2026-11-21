using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Cinemachine;
using Unity.Netcode.Components;
using UnityEngine;

public partial class PlayerControllerView
{
    [Header("영역 화면 전환")]
    [SerializeField, Min(1f)] private float _screenTransitionTimeout = 15f;

    private CancellationTokenSource _screenTransitionCancellation;
    private ScreenTransitionZone _screenTransitionSource;
    private PlayerNetworkStats _screenTransitionStats;
    private UIManager _screenTransitionManager;
    private UIBase _screenTransitionUI;
    private float _screenTransitionDeadline;
    private readonly List<Collider2D> _screenTransitionOverlaps = new List<Collider2D>(8);
    public bool IsScreenTransitioning => _screenTransitionCancellation != null;

    public bool TryStartScreenTransition(ScreenTransitionZone source, string destinationId)
    {
        if (source == null || !source.isActiveAndEnabled || !CanStartScreenTransition()) return false;
        WorldManager world = WorldManager.Inst;
        UIManager manager = UIManager.Inst;
        if (world == null || manager == null ||
            !world.TryGetStageEntryPoint(destinationId, out StageEntryPoint destination)) return false;
        if (HasScreenTransitionUIConflict(manager)) return false;
        if (!TryGetComponent(out NetworkTransform networkTransform) || !networkTransform.CanCommitToTransform)
        {
            Debug.LogWarning("화면 전환에는 소유자가 위치를 갱신하는 NetworkTransform이 필요합니다.", this);
            return false;
        }

        CancellationTokenSource cancellation = new CancellationTokenSource();
        _screenTransitionCancellation = cancellation;
        _screenTransitionSource = source;
        _screenTransitionManager = manager;
        _screenTransitionDeadline = Time.unscaledTime + _screenTransitionTimeout;
        RunScreenTransitionAsync(destination, networkTransform, cancellation).Forget();
        return true;
    }

    private bool CanStartScreenTransition()
    {
        if (!isActiveAndEnabled || IsGameplayControlLocked || IsHitControlLocked || IsAttackLocked ||
            _isDialogueMode || _isMiniGameMode || _isSpectatorMode || _loggingAlignmentRequest != 0 ||
            _networkObject == null || !_networkObject.IsSpawned || !_networkObject.IsOwner ||
            _rigidbody2D == null || !_rigidbody2D.simulated || _groundCheck == null ||
            !_inputActionsInitialized || _playerInput == null || !_playerInput.isActiveAndEnabled ||
            !_playerInput.inputIsActive || _playerInput.currentActionMap?.name != "Player" ||
            _openingSettings || _openingRuneInventory || Time.timeScale <= 0f ||
            (_interactionController != null && _interactionController.HasLocalRequest) ||
            (_scenarioService != null && _scenarioService.IsBusy)) return false;
        if (_screenTransitionStats == null) TryGetComponent(out _screenTransitionStats);
        return _screenTransitionStats != null && _screenTransitionStats.IsAlive &&
            !_screenTransitionStats.IsEncounterLocked;
    }

    private static bool HasScreenTransitionUIConflict(UIManager manager)
    {
        return IsTransitionUIBusy(manager, UIType.ScreenTransitionUI) ||
            IsTransitionUIBusy(manager, UIType.BossEntryUI) ||
            IsTransitionUIBusy(manager, UIType.CinematicEffectUI) ||
            IsTransitionUIBusy(manager, UIType.GameOverUI) ||
            IsTransitionUIBusy(manager, UIType.DialogueUI) ||
            IsTransitionUIBusy(manager, UIType.SettingUI) ||
            IsTransitionUIBusy(manager, UIType.InventoryUI) ||
            IsTransitionUIBusy(manager, UIType.RuneInventoryUI) ||
            IsTransitionUIBusy(manager, UIType.CraftingUI) || manager.HasDialogueConflict();
    }

    private static bool IsTransitionUIBusy(UIManager manager, UIType type)
    {
        return manager.IsOpen(type) || manager.IsLoading(type);
    }

    private async UniTask RunScreenTransitionAsync(StageEntryPoint destination,
        NetworkTransform networkTransform, CancellationTokenSource cancellation)
    {
        CancellationToken token = cancellation.Token;
        try
        {
            // 이동 입력과 물리는 유지하고, 행동 제한은 IsGameplayControlLocked가 담당합니다.
            _runeAttackController?.CancelAttackInput();
            UIBase ui = await _screenTransitionManager.OpenVeryFrontRootAsync(UIType.ScreenTransitionUI, token);
            token.ThrowIfCancellationRequested();
            _screenTransitionUI = ui;
            if (ui is not ScreenTransitionUI transition)
                throw new InvalidOperationException("UI/ScreenTransitionUI를 불러오지 못했습니다.");
            transition.transform.SetAsLastSibling();
            await transition.FadeAsync(true, token);
            // 완전히 검은 프레임을 출력하고, 이동 전에 목적지와 실행 권한을 다시 확인합니다.
            await UniTask.NextFrame(PlayerLoopTiming.Update, token);
            ValidateScreenTransition(destination, networkTransform, token);
            PlaceForScreenTransition(destination, networkTransform);
            // 카메라의 LateUpdate가 도착 위치를 반영한 다음 밝아지게 합니다.
            await UniTask.NextFrame(PlayerLoopTiming.Update, token);
            await transition.FadeAsync(false, token);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
        finally
        {
            // 취소된 이전 작업이 새 전환 또는 보스전의 UI/잠금을 해제하지 않게 합니다.
            if (_screenTransitionCancellation == cancellation) ReleaseScreenTransition();
            cancellation.Dispose();
        }
    }

    private void ValidateScreenTransition(StageEntryPoint destination, NetworkTransform networkTransform,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        UpdateScreenTransitionLifetime();
        token.ThrowIfCancellationRequested();
        if (destination == null || !destination.isActiveAndEnabled || networkTransform == null ||
            !networkTransform.CanCommitToTransform)
            throw new InvalidOperationException("화면 전환 도중 목적지 또는 이동 권한이 없어졌습니다.");
    }

    private void PlaceForScreenTransition(StageEntryPoint destination, NetworkTransform networkTransform)
    {
        Vector3 previousPosition = transform.position;
        // 진입 순간의 방향을 고정하지 않고, 암전 중 놓거나 바꾼 최신 입력을 사용합니다.
        float input = _moveAction != null && _moveAction.enabled && _playerInput != null &&
            _playerInput.isActiveAndEnabled && _playerInput.inputIsActive &&
            _playerInput.currentActionMap?.name == "Player" ? _moveAction.ReadValue<Vector2>().x : 0f;
        SetMoveInput(input);
        bool faceRight = Mathf.Abs(input) > 0.01f ? input > 0f :
            destination.FacingDirection == EntryFacingDirection.Right;
        SetCrouching(false);
        if (!PlaceAtPosition(destination.transform.position, faceRight))
            throw new InvalidOperationException("화면 전환 중 플레이어 배치에 실패했습니다.");
        // 배치가 초기화한 수평 속도를 즉시 이어갑니다. 수직 속도는 도착지 배치 규칙을 따릅니다.
        _rigidbody2D.linearVelocity = new Vector2(CurrentMoveVelocity, 0f);
        UpdateMovementAnimation();
        networkTransform.Teleport(transform.position, transform.rotation, transform.localScale);
        CinemachineCamera camera = GetComponentInChildren<CinemachineCamera>(true);
        if (camera != null)
        {
            camera.OnTargetObjectWarped(transform, transform.position - previousPosition);
            camera.PreviousStateIsValid = false;
        }
        SuppressScreenTransitionArrival();
    }

    private void SuppressScreenTransitionArrival()
    {
        Physics2D.SyncTransforms();
        ContactFilter2D filter = ContactFilter2D.noFilter;
        filter.useTriggers = true;
        // 순간이동할 때만 조회합니다. 물리 콜백보다 먼저 도착 Zone에 접촉을 기록합니다.
        foreach (Collider2D body in GetComponentsInChildren<Collider2D>())
        {
            if (!body.enabled || body.isTrigger || body.attachedRigidbody != _rigidbody2D) continue;
            body.Overlap(filter, _screenTransitionOverlaps);
            foreach (Collider2D overlap in _screenTransitionOverlaps)
            {
                if (overlap.TryGetComponent(out ScreenTransitionZone zone)) zone.SuppressArrival(body);
            }
        }
        _screenTransitionOverlaps.Clear();
    }

    private void UpdateScreenTransitionLifetime()
    {
        if (!IsScreenTransitioning) return;
        if (_screenTransitionSource == null || !_screenTransitionSource.isActiveAndEnabled ||
            _screenTransitionManager == null || _networkObject == null || !_networkObject.IsSpawned ||
            !_networkObject.IsOwner || _screenTransitionStats == null || !_screenTransitionStats.IsAlive ||
            _screenTransitionStats.IsEncounterLocked || IsEncounterControlLocked || IsDeathControlLocked ||
            Time.unscaledTime >= _screenTransitionDeadline)
            CancelScreenTransition();
    }

    public void CancelScreenTransition(ScreenTransitionZone source)
    {
        if (_screenTransitionSource == source) CancelScreenTransition();
    }

    private void CancelScreenTransition()
    {
        CancellationTokenSource cancellation = _screenTransitionCancellation;
        if (cancellation == null) return;
        // 정리는 즉시 끝내고, 진행 중인 비동기 호출의 finally에서 토큰을 폐기합니다.
        ReleaseScreenTransition();
        cancellation.Cancel();
    }

    private void ReleaseScreenTransition()
    {
        _screenTransitionCancellation = null;
        _screenTransitionSource = null;
        if (_screenTransitionUI != null && _screenTransitionManager != null &&
            _screenTransitionManager.IsOpen(UIType.ScreenTransitionUI))
            _screenTransitionManager.Close(UIType.ScreenTransitionUI);
        _screenTransitionUI = null;
        _screenTransitionManager = null;
    }
}
