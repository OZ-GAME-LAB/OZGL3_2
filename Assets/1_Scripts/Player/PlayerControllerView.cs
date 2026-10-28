using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerControllerView : MonoBehaviour
{
    [Header("이동 설정")]
    [SerializeField] private float _moveSpeed = 8f;
    [SerializeField] private float _jumpForce = 5f;
    [SerializeField] private PlayerInput _playerInput;
    [SerializeField] private Rigidbody2D _rigidbody2D;
    private float _moveInput;

    [Header("플레이어 애니메이션")]
    [SerializeField] private Animator _animator;
    [SerializeField, Min(0f)] private float _idleJumpInputTime = 0.12f;
    [Tooltip("걷다가 접지가 끊겨도 지상 애니메이션을 유지하는 시간(초). 점프에는 적용하지 않습니다.")]
    [SerializeField, Min(0f)] private float _animationGroundGraceTime = 0.05f;
    [Tooltip("지상에서 벽에 닿았을 때, 이 가로 속도 이하이면 달리기 모션을 멈춥니다.")]
    [SerializeField, Min(0f)] private float _blockedRunSpeedThreshold = 0.05f;
    private float _blockedRunDirection;
    private float _animationGroundLostTime;
    private float _moveInputStartedTime = -1f;
    private bool _jumpInProgress;
    private bool _wasGroundedForAnimation = true;
    private PlayerAnimationPresenter _animationPresenter;
    private readonly ContactPoint2D[] _groundContacts = new ContactPoint2D[16];

    [Header("애니메이션 진단")]
    [SerializeField] private bool _logAnimationDiagnostics;
    private bool _hasGroundDiagnosticState;
    private bool _lastDiagnosticRawGrounded;
    private bool _lastDiagnosticGrounded;
    private InputAction _moveAction;
    private InputAction _jumpAction;
    private bool _inputActionsInitialized;
    private InputAction _specialAbilityAction;
    private InputAction _consumableAction;
    private InputAction _fieldInteractAction;
    private InputAction _attackAction;
    private InputAction _specialAttackAction;
    private InputAction _switchConsumableAction;
    private InputAction _inventoryAction;
    private InputAction _journalAction;
    private InputAction _settingsAction;

    [Header("임시 관전자 모드")]
    [SerializeField, Min(0f)] private float _spectatorMoveSpeed = 15f;
    private bool _isSpectatorMode;
    private bool _wasSimulated;
    private Unity.Netcode.NetworkObject _networkObject;

    [Header("지면 체크 설정")]
    [SerializeField] private Transform _groundCheck;    // 발 밑에 배치할 빈 오브젝트
    [SerializeField] private float _checkRadius = 0.5f; // 체크 범위
    [SerializeField] private LayerMask _groundLayer;    // 지면으로 인식할 레이어 (Platforms 등)

    private bool _lookRight = true;

    [Header("얇은 발판 통과")]
    [SerializeField] private Collider2D _bodyCollider;
    [SerializeField] private LayerMask _thinPlatformLayer;
    private readonly List<Collider2D> _nearbyThinPlatforms = new List<Collider2D>(16);
    private readonly List<Collider2D> _ignoredThinPlatforms = new List<Collider2D>(16);

    [Header("앉기")]
    [SerializeField, Min(0.1f)] private float _crouchHeight = 4.199f;
    [SerializeField, Min(0f)] private float _crouchMoveSpeed = 4f;
    private const float StandClearanceInset = 0.01f;
    private BoxCollider2D _crouchCollider;
    private Vector2 _standingSize;
    private Vector2 _standingOffset;
    private bool _isCrouching;
    private readonly List<Collider2D> _standObstacles = new List<Collider2D>(16);

    [Header("벽 붙잡기")]
    [SerializeField] private PlayerWallCling _wallCling;
    [Tooltip("WallClingIdle 원본의 오른손 끝 X 위치입니다. 벽면에 손을 맞출 때 사용합니다.")]
    [SerializeField] private float _wallHandLocalX = 1.8462963f;

    // 상호작용 컨트롤러 추가
    [SerializeField]
    private PlayerInteractionController _interactionController;

    private PlayerStatViewModel _playerStatViewModel;

    void Awake()
    {
        TryGetComponent(out _networkObject);

        if (_playerInput == null && !TryGetComponent(out _playerInput))
        {
            Debug.LogError($"{nameof(_playerInput)}이 없습니다.");
            enabled = false;
            return;
        }

        if (_rigidbody2D == null && !TryGetComponent(out _rigidbody2D))
        {
            Debug.LogError($"{nameof(_rigidbody2D)}이 없습니다.");
            enabled = false;
            return;
        }
        _moveAction = _playerInput.actions["Move"];
        if (_bodyCollider == null)
            TryGetComponent(out _bodyCollider);
        InitializeCrouchCollider();
        _animationPresenter = new PlayerAnimationPresenter(_animator);
        if (_wallCling == null) TryGetComponent(out _wallCling);
        if (_wallCling != null) _wallCling.Initialize(_rigidbody2D, _bodyCollider);
        _jumpAction = _playerInput.actions["Jump"];
        _specialAbilityAction = _playerInput.actions["SpecialAbility"];
        _consumableAction = _playerInput.actions["Consumable"];
        _fieldInteractAction = _playerInput.actions["FieldInteract"];
        _attackAction = _playerInput.actions["Attack"];
        _specialAttackAction = _playerInput.actions["SpecialAttack"];
        _switchConsumableAction = _playerInput.actions["SwitchConsumable"];
        _inventoryAction = _playerInput.actions["OpenInventory"];
        _journalAction = _playerInput.actions["OpenJournal"];
        _settingsAction = _playerInput.actions["OpenSettings"];
        _inputActionsInitialized = true;
    }

    private void OnEnable()
    {
        if (!_inputActionsInitialized)
            return;

        _moveAction.performed += OnMove;
        _moveAction.canceled += OnMoveCanceled;
        _jumpAction.performed += OnJump;
        _specialAbilityAction.performed += OnSpecialAbility;
        _consumableAction.performed += OnConsumable;
        _fieldInteractAction.performed += OnFieldInteract;
        _attackAction.performed += OnAttack;
        _specialAttackAction.performed += OnSpecialAttack;
        _switchConsumableAction.performed += OnSwitchConsumable;
        _inventoryAction.performed += OnOpenInventory;
        _journalAction.performed += OnOpenJournal;
        _settingsAction.performed += OnOpenSettings;

        ServiceManager.Inst.PlayerService.BindPlayerControllerView(this);
    }

    private void OnDisable()
    {
        RestoreThinPlatformCollisions();
        // Awake에서 필수 참조를 찾지 못해 비활성화된 경우에는 구독한 입력이 없습니다.
        if (!_inputActionsInitialized)
            return;

        if (_isSpectatorMode)
            SetSpectatorMode(false);

        SetMoveInput(0f);
        ResetAirborneStateAndAnimation();
        UpdateMovementAnimation();

        _moveAction.performed -= OnMove;
        _moveAction.canceled -= OnMoveCanceled;
        _jumpAction.performed -= OnJump;
        _specialAbilityAction.performed -= OnSpecialAbility;
        _consumableAction.performed -= OnConsumable;
        _fieldInteractAction.performed -= OnFieldInteract;
        _attackAction.performed -= OnAttack;
        _specialAttackAction.performed -= OnSpecialAttack;
        _switchConsumableAction.performed -= OnSwitchConsumable;
        _inventoryAction.performed -= OnOpenInventory;
        _journalAction.performed -= OnOpenJournal;
        _settingsAction.performed -= OnOpenSettings;

        Unbind();
    }

    private void Update()
    {
        // 원격 플레이어까지 같은 키보드 입력으로 움직이지 않도록 제한합니다.
        if (_networkObject != null && _networkObject.IsSpawned && !_networkObject.IsOwner)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !_playerInput.isActiveAndEnabled || !_playerInput.inputIsActive)
            return;

        if (keyboard.digit0Key.wasPressedThisFrame || keyboard.numpad0Key.wasPressedThisFrame)
            SetSpectatorMode(!_isSpectatorMode);

        if (!_isSpectatorMode)
            return;

        Vector2 direction = Vector2.zero;
        if (keyboard.wKey.isPressed) direction.y += 1f;
        if (keyboard.sKey.isPressed) direction.y -= 1f;
        if (keyboard.aKey.isPressed) direction.x -= 1f;
        if (keyboard.dKey.isPressed) direction.x += 1f;

        // 물리 시뮬레이션이 꺼져 있으므로 위치를 직접 옮깁니다.
        transform.position += (Vector3)(direction.normalized * _spectatorMoveSpeed * Time.deltaTime);
    }

    public void Bind (PlayerStatViewModel playerStatViewModel)
    {
        Unbind();

        _playerStatViewModel = playerStatViewModel;

        if (_playerStatViewModel != null)
            _playerStatViewModel.PropertyChanged += OnPropertyChanged;
        _playerStatViewModel.InvokeOnceOnInit();
    }
    private void Unbind()
    {
        if (_playerStatViewModel != null)
            _playerStatViewModel.PropertyChanged -= OnPropertyChanged;

        _playerStatViewModel = null;
    }
    

    

    private void SetSpectatorMode(bool enabled)
    {
        if (_isSpectatorMode == enabled)
            return;

        _isSpectatorMode = enabled;
        RestoreThinPlatformCollisions();
        SetMoveInput(0f);
        ResetAirborneStateAndAnimation();

        if (_rigidbody2D == null)
            return;

        _rigidbody2D.linearVelocity = Vector2.zero;
        _rigidbody2D.angularVelocity = 0f;

        if (enabled)
        {
            _wasSimulated = _rigidbody2D.simulated;
            _rigidbody2D.simulated = false;
        }
        else
        {
            _rigidbody2D.position = transform.position;
            _rigidbody2D.simulated = _wasSimulated;
            SetMoveInput(_moveAction.ReadValue<Vector2>().x);
        }

        UpdateMovementAnimation();
    }

    private void FixedUpdate()
    {
        if (_networkObject != null && _networkObject.IsSpawned && !_networkObject.IsOwner)
            return;
        UpdateThinPlatformCollisions();
        UpdateCrouchState();
        UpdateWallState();
        UpdateGroundStateAndAnimation();

        if (_isSpectatorMode)
            return;

        _rigidbody2D.linearVelocity = new Vector2(
            _wallCling != null ? _wallCling.ResolveHorizontalVelocity(CurrentMoveVelocity) : CurrentMoveVelocity,
            _rigidbody2D.linearVelocity.y
        );
    }

    private void UpdateWallState()
    {
        if (_wallCling == null || _rigidbody2D == null) return;
        if (_isSpectatorMode)
        {
            _wallCling.ResetState();
            UpdateWallAnimation();
            return;
        }
        bool grounded = HasGroundContact();
        bool airborne = _jumpInProgress || _rigidbody2D.linearVelocity.y < -0.1f || _wallCling.IsClinging;
        bool inputEnabled = _playerInput != null && _playerInput.isActiveAndEnabled &&
            _playerInput.inputIsActive && _moveAction != null && _moveAction.enabled && !_isCrouching;
        _wallCling.UpdateState(_moveInput, grounded, airborne, inputEnabled);
        UpdateWallAnimation();
    }

    private void UpdateWallAnimation()
    {
        bool clinging = _wallCling != null && _wallCling.IsClinging;
        bool hasBodyGeometry = _crouchCollider != null;
        float bodyRightEdge = hasBodyGeometry
            ? _crouchCollider.offset.x + _crouchCollider.size.x * 0.5f
            : 0f;
        bool ledgeHanging = _wallCling != null && _wallCling.IsLedgeHanging;
        _animationPresenter?.SetWallClinging(clinging, ledgeHanging, hasBodyGeometry, bodyRightEdge, _wallHandLocalX);
    }

    private float CurrentMoveVelocity
    {
        get
        {
            float input = _moveInput;
            Keyboard keyboard = Keyboard.current;
            // Move의 2D 합성 입력은 S+A/D에서 X가 0.707로 줄어들므로 키보드 앉기 이동만 보정합니다.
            if (_isCrouching && Mathf.Abs(input) > 0.01f && keyboard != null &&
                (keyboard.aKey.isPressed || keyboard.dKey.isPressed ||
                 keyboard.leftArrowKey.isPressed || keyboard.rightArrowKey.isPressed))
                input = Mathf.Sign(input);
            return input * (_isCrouching ? _crouchMoveSpeed : _moveSpeed);
        }
    }

    private void InitializeCrouchCollider()
    {
        _crouchCollider = _bodyCollider as BoxCollider2D;
        if (_crouchCollider == null)
        {
            Debug.LogWarning("앉기에는 몸의 BoxCollider2D가 필요합니다.", this);
            return;
        }
        _standingSize = _crouchCollider.size;
        _standingOffset = _crouchCollider.offset;
    }

    private bool ReadCrouchInput()
    {
        return !_isSpectatorMode && _playerInput != null && _playerInput.isActiveAndEnabled &&
            _playerInput.inputIsActive && _moveAction != null && _moveAction.enabled &&
            Keyboard.current != null && Keyboard.current.sKey.isPressed;
    }

    private void UpdateCrouchState()
    {
        if (_crouchCollider == null || _rigidbody2D == null)
            return;
        if (_networkObject != null && _networkObject.IsSpawned && !_networkObject.IsOwner)
            return;
        if (_isSpectatorMode)
        {
            _animationPresenter?.SetCrouching(false);
            return;
        }

        if (ReadCrouchInput())
            SetCrouching(true);
        else if (_isCrouching && CanStandUp())
            SetCrouching(false);

        // S를 놓아도 머리가 막혀 있으면 실제 앉기 상태를 계속 보여줍니다.
        _animationPresenter?.SetCrouching(_isCrouching);
    }

    private void SetCrouching(bool crouching)
    {
        if (_crouchCollider == null || _isCrouching == crouching)
            return;
        float height = crouching ? Mathf.Clamp(_crouchHeight, 0.1f, _standingSize.y) : _standingSize.y;
        float bottom = _standingOffset.y - _standingSize.y * 0.5f;
        _crouchCollider.size = new Vector2(_standingSize.x, height);
        _crouchCollider.offset = new Vector2(_standingOffset.x, bottom + height * 0.5f);
        _isCrouching = crouching;
        _animationPresenter?.SetCrouching(crouching && !_isSpectatorMode);
    }

    private bool CanStandUp()
    {
        if (_crouchCollider == null || _rigidbody2D == null)
            return false;
        float currentTop = _crouchCollider.offset.y + _crouchCollider.size.y * 0.5f;
        float standingTop = _standingOffset.y + _standingSize.y * 0.5f;
        float extraHeight = standingTop - currentTop;
        if (extraHeight <= StandClearanceInset * 2f)
            return true;

        // 서면서 늘어나는 윗부분 전체를 검사합니다. 바닥과 기존 몸통은 포함하지 않습니다.
        Transform bodyTransform = _crouchCollider.transform;
        Vector2 center = bodyTransform.TransformPoint(new Vector3(
            _standingOffset.x, (currentTop + standingTop) * 0.5f, 0f));
        Vector3 scale = bodyTransform.lossyScale;
        Vector2 size = new Vector2(
            Mathf.Max(0.001f, _standingSize.x * Mathf.Abs(scale.x) - StandClearanceInset * 2f),
            Mathf.Max(0.001f, extraHeight * Mathf.Abs(scale.y) - StandClearanceInset * 2f));
        int mask = Physics2D.GetLayerCollisionMask(_crouchCollider.gameObject.layer);
        mask = (mask | _crouchCollider.includeLayers.value) & ~_crouchCollider.excludeLayers.value;
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(mask);
        filter.useTriggers = false;
        Physics2D.OverlapBox(center, size, bodyTransform.eulerAngles.z, filter, _standObstacles);
        for (int i = 0; i < _standObstacles.Count; i++)
        {
            Collider2D hit = _standObstacles[i];
            if (hit == null || hit.attachedRigidbody == _rigidbody2D ||
                Physics2D.GetIgnoreCollision(_crouchCollider, hit) || IsIgnoredThinPlatform(hit))
                continue;
            // 아래에서 통과할 수 있는 일방향 발판은 천장으로 취급하지 않습니다.
            if (hit.usedByEffector && hit.TryGetComponent(out PlatformEffector2D effector) &&
                effector.enabled && effector.useOneWay)
                continue;
            return false;
        }
        return true;
    }

    private bool IsDroppingThroughPlatforms()
    {
        if (_isSpectatorMode || _playerInput == null || !_playerInput.isActiveAndEnabled ||
            !_playerInput.inputIsActive || _moveAction == null || !_moveAction.enabled)
            return false;
        if (_networkObject != null && _networkObject.IsSpawned && !_networkObject.IsOwner)
            return false;

        return Keyboard.current != null && Keyboard.current.sKey.isPressed &&
            Keyboard.current.spaceKey.isPressed;
    }

    private bool IsThinPlatform(Collider2D platform)
    {
        return platform != null && (_thinPlatformLayer.value & (1 << platform.gameObject.layer)) != 0;
    }

    private bool IsIgnoredThinPlatform(Collider2D platform)
    {
        return IsThinPlatform(platform) &&
            (IsDroppingThroughPlatforms() || _ignoredThinPlatforms.Contains(platform));
    }

    private void UpdateThinPlatformCollisions()
    {
        if (_bodyCollider == null || _rigidbody2D == null)
            return;
        if (_networkObject != null && _networkObject.IsSpawned && !_networkObject.IsOwner)
        {
            RestoreThinPlatformCollisions();
            return;
        }

        bool dropping = IsDroppingThroughPlatforms();
        if (dropping && _bodyCollider.enabled && _rigidbody2D.simulated)
        {
            // 다음 물리 이동 위치까지 검사해 낙하 중 만날 발판도 미리 무시합니다.
            Bounds bounds = _bodyCollider.bounds;
            Vector2 travel = _rigidbody2D.linearVelocity * Time.fixedDeltaTime;
            travel.x = CurrentMoveVelocity * Time.fixedDeltaTime;
            Vector2 size = (Vector2)bounds.size +
                new Vector2(Mathf.Abs(travel.x), Mathf.Abs(travel.y)) + Vector2.one * 0.1f;
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(_thinPlatformLayer);
            filter.useTriggers = false;
            Physics2D.OverlapBox((Vector2)bounds.center + travel * 0.5f, size, 0f,
                filter, _nearbyThinPlatforms);
            for (int i = 0; i < _nearbyThinPlatforms.Count; i++)
            {
                Collider2D platform = _nearbyThinPlatforms[i];
                if (platform.attachedRigidbody == _rigidbody2D ||
                    _ignoredThinPlatforms.Contains(platform) ||
                    Physics2D.GetIgnoreCollision(_bodyCollider, platform))
                    continue;
                Physics2D.IgnoreCollision(_bodyCollider, platform, true);
                _rigidbody2D.WakeUp();
                _ignoredThinPlatforms.Add(platform);
            }
        }

        for (int i = _ignoredThinPlatforms.Count - 1; i >= 0; i--)
        {
            Collider2D platform = _ignoredThinPlatforms[i];
            if (platform == null)
            {
                _ignoredThinPlatforms.RemoveAt(i);
                continue;
            }
            if (!platform.isActiveAndEnabled || !_bodyCollider.isActiveAndEnabled ||
                !_rigidbody2D.simulated)
            {
                Physics2D.IgnoreCollision(_bodyCollider, platform, false);
                _ignoredThinPlatforms.RemoveAt(i);
                continue;
            }

            if (dropping && _nearbyThinPlatforms.Contains(platform))
            {
                Physics2D.IgnoreCollision(_bodyCollider, platform, true);
                continue;
            }
            ColliderDistance2D separation = _bodyCollider.Distance(platform);
            // S 또는 Space를 일찍 놓아도 몸이 발판을 벗어날 때까지 통과를 유지합니다.
            if (separation.isValid && !separation.isOverlapped && separation.distance > 0.05f)
            {
                Physics2D.IgnoreCollision(_bodyCollider, platform, false);
                _ignoredThinPlatforms.RemoveAt(i);
            }
            else
            {
                Physics2D.IgnoreCollision(_bodyCollider, platform, true);
            }
        }
    }

    private void RestoreThinPlatformCollisions()
    {
        if (_bodyCollider != null)
        {
            for (int i = 0; i < _ignoredThinPlatforms.Count; i++)
            {
                if (_ignoredThinPlatforms[i] != null)
                    Physics2D.IgnoreCollision(_bodyCollider, _ignoredThinPlatforms[i], false);
            }
        }
        _ignoredThinPlatforms.Clear();
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        if (_isSpectatorMode)
            return;

        SetMoveInput(context.ReadValue<Vector2>().x);
        UpdateMovementAnimation();

        if (_moveInput > 0 && !_lookRight)
            Flip();
        else if (_moveInput < 0 && _lookRight)
            Flip();
    }

    private void OnMoveCanceled(InputAction.CallbackContext context)
    {
        SetMoveInput(0f);
        UpdateMovementAnimation();
    }

    private void SetMoveInput(float input)
    {
        bool wasMoving = Mathf.Abs(_moveInput) > 0.01f;
        bool isMoving = Mathf.Abs(input) > 0.01f;
        if (!isMoving)
            _moveInputStartedTime = -1f;
        else if (!wasMoving)
            _moveInputStartedTime = Time.time;
        _moveInput = input;
        // 방향 입력을 놓거나 반대로 누르면 물리 업데이트를 기다리지 않고 벽을 놓습니다.
        if (_wallCling != null)
        {
            _wallCling.ReleaseForInput(input);
            UpdateWallAnimation();
        }
    }

    private void ResetAirborneStateAndAnimation()
    {
        _blockedRunDirection = 0f;
        if (_wallCling != null) _wallCling.ResetState();
        UpdateWallAnimation();
        ResetJumpTracking();
        _animationPresenter?.ResetJump();
    }

    private void ResetJumpTracking()
    {
        _jumpInProgress = false;
        _animationGroundLostTime = 0f;
        _wasGroundedForAnimation = true;
    }

    private bool IsMovingForAnimation => !_isSpectatorMode && Mathf.Abs(_moveInput) > 0.01f &&
        (_isCrouching || _jumpInProgress || _moveInput * _blockedRunDirection <= 0f);

    private void UpdateMovementAnimation()
    {
        _animationPresenter?.SetMoving(IsMovingForAnimation);
    }

    private void UpdateBlockedRunState(bool grounded)
    {
        _blockedRunDirection = 0f;
        if (!grounded || _isSpectatorMode || _isCrouching || _jumpInProgress ||
            _bodyCollider == null || Mathf.Abs(_moveInput) <= 0.01f ||
            Mathf.Abs(_rigidbody2D.linearVelocity.x) > _blockedRunSpeedThreshold)
            return;

        // 새 이동 속도를 넣기 전, 이전 물리 처리 결과로 정지 여부를 판단합니다.
        float direction = Mathf.Sign(_moveInput);
        int count = _bodyCollider.GetContacts(_groundContacts);
        for (int i = 0; i < count; i++)
        {
            ContactPoint2D contact = _groundContacts[i];
            if (IsIgnoredThinPlatform(contact.collider) || IsIgnoredThinPlatform(contact.otherCollider))
                continue;
            // 입력을 막는 옆면만 벽으로 보고, 바닥과 걸어 올라갈 수 있는 경사는 제외합니다.
            if (contact.normal.x * direction <= -0.7f && Mathf.Abs(contact.normal.y) < 0.5f)
            {
                _blockedRunDirection = direction;
                return;
            }
        }
    }

    private void UpdateGroundStateAndAnimation()
    {
        if (_rigidbody2D == null)
            return;
        if (_networkObject != null && _networkObject.IsSpawned && !_networkObject.IsOwner)
            return;

        // 접촉 판정, 표현용 유예, 행동 상태 갱신은 기존 순서를 유지합니다.
        float verticalSpeed = _rigidbody2D.linearVelocity.y;
        bool rawGrounded = _isSpectatorMode ||
            (verticalSpeed <= 0.1f && HasGroundContact());
        bool grounded = ResolveAnimationGrounded(rawGrounded);
        LogGroundAnimationDiagnostic(rawGrounded, grounded);

        // 점프 종류는 공중 입력으로 바꾸지 않습니다. 걸어서 떨어질 때만 해제합니다.
        if (!grounded && _wasGroundedForAnimation && !_jumpInProgress)
            _animationPresenter?.ClearIdleJump();

        UpdateJumpLandingState(grounded);
        UpdateBlockedRunState(rawGrounded);
        UpdateMovementAnimation();
        _animationPresenter?.SetGroundState(grounded, _isSpectatorMode ? 0f : verticalSpeed);
    }

    private bool ResolveAnimationGrounded(bool rawGrounded)
    {
        if (rawGrounded)
        {
            _animationGroundLostTime = 0f;
            return true;
        }

        _animationGroundLostTime += Time.fixedDeltaTime;
        // 짧은 접촉 끊김만 표현에서 보정하고, 실제 점프와 발판 통과는 즉시 공중으로 표시합니다.
        return !IsDroppingThroughPlatforms() && _ignoredThinPlatforms.Count == 0 &&
            !_jumpInProgress && (_wallCling == null || !_wallCling.IsClinging) && _wasGroundedForAnimation &&
            _animationGroundLostTime < _animationGroundGraceTime;
    }

    private void UpdateJumpLandingState(bool grounded)
    {
        // 이번 정리에서는 기존 착지 확정 조건을 유지합니다.
        // Presenter가 없어도 점프 제한 해제는 Controller에서 수행합니다.
        if (grounded && !_wasGroundedForAnimation)
            _jumpInProgress = false;
        _wasGroundedForAnimation = grounded;
    }

    private void LateUpdate()
    {
        if (!_logAnimationDiagnostics)
        {
            _animationPresenter?.ResetDiagnostics();
            _hasGroundDiagnosticState = false;
            return;
        }
        if (_animator == null || _rigidbody2D == null)
            return;
        if (_networkObject != null && _networkObject.IsSpawned && !_networkObject.IsOwner)
            return;

        if (_animationPresenter != null && _animationPresenter.TryGetStateDiagnostic(out string message))
            Debug.Log($"{message} {GetAnimationDiagnosticContext()}", this);
    }

    private void LogGroundAnimationDiagnostic(bool rawGrounded, bool grounded)
    {
        if (!_logAnimationDiagnostics)
            return;
        if (!_hasGroundDiagnosticState || rawGrounded != _lastDiagnosticRawGrounded ||
            grounded != _lastDiagnosticGrounded)
        {
            Debug.Log($"[PlayerAnim][Ground] raw={rawGrounded}, final={grounded}, " +
                $"이전최종={_wasGroundedForAnimation}, 접지끊김={_animationGroundLostTime:F3}s " +
                $"{GetAnimationDiagnosticContext()} (Animator 반영 전)", this);
            int count = _rigidbody2D.GetContacts(_groundContacts);
            for (int i = 0; i < count; i++)
            {
                ContactPoint2D contact = _groundContacts[i];
                Debug.Log($"[PlayerAnim][Contact] frame={Time.frameCount}, " +
                    $"collider={contact.collider.name}, other={contact.otherCollider.name}, " +
                    $"point={contact.point.ToString("F3")}, normal={contact.normal.ToString("F3")}", this);
            }
        }
        _lastDiagnosticRawGrounded = rawGrounded;
        _lastDiagnosticGrounded = grounded;
        _hasGroundDiagnosticState = true;
    }

    private string GetAnimationDiagnosticContext()
    {
        return $"frame={Time.frameCount}, time={Time.time:F3}, fixed={Time.fixedTime:F3}, " +
            $"position={transform.position.ToString("F3")}, vy={_rigidbody2D.linearVelocity.y:F3}, " +
            $"jump={_jumpInProgress}, IsGrounded={(_animationPresenter != null && _animationPresenter.DisplayedGrounded)}";
    }

    private bool HasGroundContact()
    {
        if (_groundCheck == null)
            return false;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(_groundLayer);
        filter.useTriggers = false;
        int count = _rigidbody2D.GetContacts(filter, _groundContacts);
        for (int i = 0; i < count; i++)
        {
            ContactPoint2D contact = _groundContacts[i];
            // 벽이나 발판 옆면은 착지로 보지 않습니다.
            if (IsIgnoredThinPlatform(contact.collider) || IsIgnoredThinPlatform(contact.otherCollider))
                continue;
            if (contact.normal.y >= 0.5f &&
                Mathf.Abs(contact.point.y - _groundCheck.position.y) <= _checkRadius)
                return true;
        }
        return false;
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        if (_isSpectatorMode)
            return;
        if (_networkObject != null && _networkObject.IsSpawned && !_networkObject.IsOwner)
            return;

        // S+Space는 내려가기 입력이므로 점프 힘과 점프 애니메이션을 실행하지 않습니다.
        if (IsDroppingThroughPlatforms())
        {
            UpdateThinPlatformCollisions();
            return;
        }

        bool grounded = HasGroundContact();
        SetMoveInput(_moveAction.ReadValue<Vector2>().x);
        if (!grounded && TryWallJump())
            return;
        if (_jumpInProgress || !grounded)
            return;

        TryGroundJump();
    }

    private bool TryWallJump()
    {
        if (_wallCling == null || !_wallCling.CanWallJump || ReadCrouchInput())
            return false;
        if (!TryStandForJump())
            return false;
        if (!_wallCling.TryJump(_moveInput, _jumpForce, out bool idleJump))
            return false;

        _jumpInProgress = true;
        _wasGroundedForAnimation = false;
        UpdateWallAnimation();
        PlayJumpAnimation(idleJump);
        return true;
    }

    private void TryGroundJump()
    {
        if (_wallCling != null) _wallCling.ResetState();
        UpdateWallAnimation();

        // 낮은 천장 아래에서는 점프가 서기 검사를 우회하지 않도록 합니다.
        if (ReadCrouchInput())
            return;
        if (!TryStandForJump())
            return;

        // 같은 프레임에 이동과 점프가 들어와도 현재 입력을 기준으로 판정합니다.
        SetMoveInput(_moveAction.ReadValue<Vector2>().x);
        float inputTime = _moveInputStartedTime < 0f ? 0f : Time.time - _moveInputStartedTime;
        bool idleJump = Mathf.Abs(_moveInput) <= 0.01f || inputTime < _idleJumpInputTime;
        _jumpInProgress = true;
        _wasGroundedForAnimation = true;
        _rigidbody2D.AddForce(Vector2.up * _jumpForce, ForceMode2D.Impulse);
        PlayJumpAnimation(idleJump);
    }

    private bool TryStandForJump()
    {
        if (!_isCrouching)
            return true;
        if (!CanStandUp())
            return false;

        SetCrouching(false);
        return true;
    }

    private void PlayJumpAnimation(bool idleJump)
    {
        if (_logAnimationDiagnostics)
            Debug.Log($"[PlayerAnim][Jump] 종류={(idleJump ? "IdleJump" : "RunningJump")} " +
                GetAnimationDiagnosticContext(), this);
        _animationPresenter?.PlayJump(idleJump, IsMovingForAnimation, _rigidbody2D.linearVelocity.y);
    }

    private void OnSpecialAbility(InputAction.CallbackContext context)
    {
        Debug.Log($"{nameof(OnSpecialAbility)} 실행");
    }

    private void OnConsumable(InputAction.CallbackContext context)
    {
        Debug.Log($"{nameof(OnConsumable)} 실행");
    }

    private void OnFieldInteract(InputAction.CallbackContext context)
    {
        Debug.Log($"{nameof(OnFieldInteract)} 실행");

        //상호작용 컨트롤러 호출 함수 추가
        if (_interactionController == null)
        {
            return;
        }

        _interactionController.TryRequestInteraction();
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        Debug.Log($"{nameof(OnAttack)} 실행");
    }

    private void OnSpecialAttack(InputAction.CallbackContext context)
    {
        Debug.Log($"{nameof(OnSpecialAttack)} 실행");

    }

    private void OnSwitchConsumable(InputAction.CallbackContext context)
    {
        Debug.Log($"{nameof(OnSwitchConsumable)} 실행");

    }

    private async void OnOpenInventory(InputAction.CallbackContext context)
    {
        var view = await UIManager.Inst.OpenInventoryUIAsync();
        ServiceManager.Inst.InventoryService.BindInventoryView(view);

    }

    private void OnOpenJournal(InputAction.CallbackContext context)
    {
        Debug.Log($"{nameof(OnOpenJournal)} 실행");

    }

    private void OnOpenSettings(InputAction.CallbackContext context)
    {
        Debug.Log($"{nameof(OnOpenSettings)} 실행");

    }

    public bool PlaceAtPosition(Vector3 footPosition, bool faceRight)
    {
        if (_rigidbody2D == null || _groundCheck == null)
        {
            Debug.LogWarning("플레이어 배치에 Rigidbody2D와 Ground Check가 필요합니다.", this);
            return false;
        }

        _lookRight = transform.localScale.x >= 0f;
        if (_lookRight != faceRight)
            Flip();

        Vector3 targetPosition = transform.position + footPosition - _groundCheck.position;
        RestoreThinPlatformCollisions();
        targetPosition.z = footPosition.z;

        _rigidbody2D.linearVelocity = Vector2.zero;
        _rigidbody2D.angularVelocity = 0f;
        _rigidbody2D.position = new Vector2(targetPosition.x, targetPosition.y);
        transform.position = targetPosition;
        ResetAirborneStateAndAnimation();
        return true;
    }

    void Flip()
    {
        _lookRight = !_lookRight;
        Vector3 scaler = transform.localScale;
        scaler.x *= -1;
        transform.localScale = scaler;
        if (_wallCling != null) _wallCling.SyncSensorTransform();
    }

    private void OnDrawGizmos()
    {
        if (_groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_groundCheck.position, _checkRadius);
        }
    }
    private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(_playerStatViewModel.MoveSpeed):
                {
                    _moveSpeed = _playerStatViewModel.MoveSpeed;
                    break;
                }
            case nameof(_playerStatViewModel.JumpForce):
                {
                    _jumpForce = _playerStatViewModel.JumpForce;

                    break;
                }
        }
    }

}
