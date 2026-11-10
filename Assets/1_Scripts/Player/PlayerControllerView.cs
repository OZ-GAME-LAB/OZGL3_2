using Cysharp.Threading.Tasks;
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

    [Header("점프 비거리")]
    [SerializeField, Min(0f)] private float _normalJumpDistanceMultiplier = 0.85f;
    [SerializeField, Min(0f)] private float _runUpJumpDistanceMultiplier = 1.2f;
    [SerializeField, Min(0f)] private float _runUpRequiredTime = 0.3f;
    private float _runUpTime;
    private float _runUpDirection;
    private float _runUpJumpDirection;
    private float _jumpDistanceMultiplier = 1f;

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
    private InputAction _runeInventoryAction;
    private bool _openingRuneInventory;

    [Header("임시 관전자 모드")]
    [SerializeField, Min(0f)] private float _spectatorMoveSpeed = 15f;
    private bool _isSpectatorMode;
    private bool _wasSimulated;
    private Unity.Netcode.NetworkObject _networkObject;

    [Header("지면 체크 설정")]
    [SerializeField] private Transform _groundCheck;    // 발 밑에 배치할 빈 오브젝트
    [SerializeField] private float _checkRadius = 0.5f; // 체크 범위
    [SerializeField] private LayerMask _groundLayer;    // 지면으로 인식할 레이어 (Platforms 등)
    [Tooltip("수평 바닥 기준으로 걸어 올라갈 수 있는 최대 각도입니다. 레이어와 관계없이 급경사 이동을 제한합니다.")]
    [SerializeField, Range(0f, 89f)] private float _maxSlopeAngle = 45f;

    [Header("미세한 바닥 이음새 보정")]
    [Tooltip("걸어서 넘을 수 있는 작은 턱의 높이(월드 단위). 0이면 보정을 끕니다.")]
    [SerializeField, Min(0f)] private float _maxStepHeight = 0.03f;
    [Tooltip("발 앞에서 이어지는 바닥을 확인할 최대 거리(월드 단위). 큰 구멍을 건너뛰는 기능은 아닙니다.")]
    [SerializeField, Min(0f)] private float _maxSeamGap = 0.03f;
    private const float SeamClearance = 0.002f;
    private readonly RaycastHit2D[] _seamGroundHits = new RaycastHit2D[16];
    private readonly Collider2D[] _seamObstacles = new Collider2D[16];

    private bool _lookRight = true;

    [Header("얇은 발판 통과")]
    [SerializeField] private Collider2D _bodyCollider;
    [SerializeField] private LayerMask _thinPlatformLayer;
    private readonly List<Collider2D> _nearbyThinPlatforms = new List<Collider2D>(16);
    private readonly List<Collider2D> _ignoredThinPlatforms = new List<Collider2D>(16);
    private readonly List<Collider2D> _sidePassThinPlatforms = new List<Collider2D>(16);
    private readonly List<RaycastHit2D> _thinPlatformSurfaceHits = new List<RaycastHit2D>(16);
    private const float ThinPlatformClearance = 0.05f;

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

    //룬 적용한 공격을 위한 컨트롤러 추가
    [SerializeField]
    private PlayerRuneAttackController _runeAttackController;

    private PlayerStatViewModel _playerStatViewModel;

    private InputAction _miniGameConfirmAction;
    private InputAction _miniGameCancelAction;

    private bool _isMiniGameMode;
    private int _miniGameEnteredFrame;

    void Awake()
    {
        TryGetComponent(out _networkObject);

        if (_runeAttackController == null)
        {
            _runeAttackController = GetComponent<PlayerRuneAttackController>();
        }

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
        {
            TryGetComponent(out _bodyCollider);
        }

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
        _runeInventoryAction = _playerInput.actions["OpenRuneInventory"];
        _inputActionsInitialized = true;
        _miniGameConfirmAction = _playerInput.actions["MiniGame/Confirm"];
        _miniGameCancelAction = _playerInput.actions["MiniGame/Cancel"];
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
        _fieldInteractAction.canceled += OnFieldInteractCanceled;
        _runeInventoryAction.performed += OnOpenRuneInventory;
        _miniGameConfirmAction.performed += OnMiniGameConfirm;
        _miniGameCancelAction.performed += OnMiniGameCancel;

        //ServiceManager.Inst.PlayerService.BindPlayerControllerView(this);

        PlayerNetworkStats stats = GetComponent<PlayerNetworkStats>();

        if (_interactionController != null)
        {
            _interactionController.InteractionStartResolved += OnInteractionStartResolved;
            _interactionController.InteractionEnded += ExitMiniGameMode;
        }

        if (stats != null)
        {
            Bind(stats.ViewModel);
        }
        else
        {
            Debug.LogError("PlayerNetworkStats가 없습니다.", this);
        }
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
        _fieldInteractAction.canceled -= OnFieldInteractCanceled;

        _miniGameConfirmAction.performed -= OnMiniGameConfirm;
        _miniGameCancelAction.performed -= OnMiniGameCancel;

        if (_interactionController != null)
        {
            _interactionController.InteractionStartResolved -= OnInteractionStartResolved;
            _interactionController.InteractionEnded -= ExitMiniGameMode;
        }
        ExitMiniGameMode();

        _interactionController?.CancelRequestedInteraction();
        _runeInventoryAction.performed -= OnOpenRuneInventory;

        Unbind();
    }

    private void Update()
    {
        /*
        // 원격 플레이어까지 같은 키보드 입력으로 움직이지 않도록 제한합니다.
        if (_networkObject != null && _networkObject.IsSpawned && !_networkObject.IsOwner)
            return;
        */

        if (_networkObject == null || _networkObject.IsSpawned == false || _networkObject.IsOwner == false)
        {
            return;
        }

        if (_isMiniGameMode || _fieldInteractAction.IsPressed())
        {
            _interactionController?.TickInteraction(Time.deltaTime);
        }

        if (_isMiniGameMode)
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

        if (_playerStatViewModel == null)
        {
            return;
        }
        
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

        if (_isMiniGameMode)
        {
            SetMoveInput(0f);

            Vector2 velocity = _rigidbody2D.linearVelocity;
            velocity.x = 0f;
            _rigidbody2D.linearVelocity = velocity;

            UpdateGroundStateAndAnimation();
            return;
        }

        UpdateThinPlatformCollisions();
        UpdateCrouchState();
        UpdateWallState();
        UpdateGroundStateAndAnimation();

        if (_isSpectatorMode)
            return;

        Vector2 movementVelocity = new Vector2(CurrentMoveVelocity, _rigidbody2D.linearVelocity.y);
        float stepContactCeiling = _bodyCollider != null
            ? _bodyCollider.bounds.min.y + _maxStepHeight + SeamClearance : 0f;
        Collider2D steppedGround = TryCorrectGroundSeam(movementVelocity);
        LimitSteepSlopeMovement(ref movementVelocity, steppedGround, stepContactCeiling);
        if (steppedGround != null)
        {
            _blockedRunDirection = 0f;
            UpdateMovementAnimation();
        }
        // 벽 점프의 고정 속도와 붙잡기 정지는 기존 벽 상태가 최종 결정합니다.
        if (_wallCling != null)
            movementVelocity.x = _wallCling.ResolveHorizontalVelocity(movementVelocity.x);
        _rigidbody2D.linearVelocity = movementVelocity;
    }

    private float MinimumGroundNormalY => Mathf.Cos(_maxSlopeAngle * Mathf.Deg2Rad);

    private bool IsSteepSurface(Vector2 normal)
    {
        // 천장은 제외하고, 최대 경사각을 넘는 오르막과 수직 벽만 검사합니다.
        return normal.y >= 0f && normal.y < MinimumGroundNormalY - 0.0001f;
    }

    private void LimitSteepSlopeMovement(ref Vector2 velocity, Collider2D steppedGround, float stepContactCeiling)
    {
        if (_bodyCollider == null)
            return;

        // Ground 밖의 절벽도 실제로 몸에 닿았다면 이동을 막아야 합니다.
        int count = _bodyCollider.GetContacts(_groundContacts);
        float requestedX = velocity.x;
        for (int i = 0; i < count; i++)
        {
            ContactPoint2D contact = _groundContacts[i];
            // ThinPlatform 접촉은 벽/급경사에 의한 이동 제한 대상으로 취급하지 않습니다.
            if (IsThinPlatform(contact.collider) || IsThinPlatform(contact.otherCollider) ||
                !IsSteepSurface(contact.normal))
                continue;
            // 법선은 표면 바깥 방향입니다. 벽에서 멀어지는 이동은 그대로 허용합니다.
            if (requestedX * contact.normal.x > 0f)
                continue;

            // 보정 전 접촉 정보가 남아 있으므로, 통과를 확인한 발밑의 면만 제외합니다.
            if (steppedGround != null && contact.point.y <= stepContactCeiling &&
                (contact.collider == steppedGround || contact.otherCollider == steppedGround))
                continue;

            velocity.x = 0f;
            // 충돌로 생긴 상승만 제거하고, 지상/벽 점프의 상승 속도는 유지합니다.
            if (!_jumpInProgress && velocity.y > 0f)
                velocity.y = 0f;
        }
    }

    private Collider2D TryCorrectGroundSeam(Vector2 velocity)
    {
        if (_maxStepHeight <= 0f || _maxSeamGap <= 0f || _crouchCollider == null ||
            !_bodyCollider.enabled || !_rigidbody2D.simulated || _jumpInProgress ||
            Mathf.Abs(velocity.x) <= 0.01f || Mathf.Abs(velocity.y) > 0.1f ||
            (_wallCling != null && _wallCling.IsClinging) || IsDroppingThroughPlatforms() ||
            _ignoredThinPlatforms.Count > 0 || !HasGroundContact())
            return null;

        // 현재 플레이어처럼 회전하지 않는 박스 형태에만 적용합니다.
        if (Mathf.Abs(Mathf.DeltaAngle(_bodyCollider.transform.eulerAngles.z, 0f)) > 0.01f)
            return null;

        Bounds bounds = _bodyCollider.bounds;
        float direction = Mathf.Sign(velocity.x);
        Collider2D obstacle = FindSmallStepContact(bounds, direction);
        if (obstacle == null)
            return null;

        ContactFilter2D filter = new ContactFilter2D();
        int mask = Physics2D.GetLayerCollisionMask(_bodyCollider.gameObject.layer);
        mask = (mask | _bodyCollider.includeLayers.value) & ~_bodyCollider.excludeLayers.value;
        filter.SetLayerMask(mask);
        filter.useTriggers = false;

        float frontX = direction > 0f ? bounds.max.x : bounds.min.x;
        float advance = Mathf.Min(Mathf.Abs(velocity.x) * Time.fixedDeltaTime, _maxSeamGap);
        Vector2 origin = new Vector2(frontX + direction * advance,
            bounds.min.y + _maxStepHeight + SeamClearance);
        int count = Physics2D.Raycast(origin, Vector2.down, filter, _seamGroundHits,
            _maxStepHeight * 2f + SeamClearance);
        if (count == _seamGroundHits.Length)
            return null;

        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = _seamGroundHits[i];
            if (hit.collider != obstacle || hit.fraction <= 0f || hit.normal.y < MinimumGroundNormalY)
                continue;

            float difference = hit.point.y - bounds.min.y;
            if (Mathf.Abs(difference) > _maxStepHeight)
                continue;
            // 아래로 끌어당기지 않고, 발 모서리가 걸리지 않을 높이까지만 올립니다.
            float lift = Mathf.Max(0f, difference) + SeamClearance;
            if (!HasSeamClearance(bounds, lift, velocity.x * Time.fixedDeltaTime, filter))
                return null;

            _rigidbody2D.position += Vector2.up * lift;
            return obstacle;
        }
        return null;
    }

    private Collider2D FindSmallStepContact(Bounds bounds, float direction)
    {
        int count = _bodyCollider.GetContacts(_groundContacts);
        if (count == _groundContacts.Length)
            return null;
        Collider2D step = null;
        for (int i = 0; i < count; i++)
        {
            ContactPoint2D contact = _groundContacts[i];
            if (!IsSteepSurface(contact.normal) || contact.normal.x * direction >= 0f)
                continue;
            Collider2D other = contact.collider == _bodyCollider ? contact.otherCollider : contact.collider;
            if (IsIgnoredThinPlatform(other))
                continue;
            if (contact.point.y > bounds.min.y + _maxStepHeight + SeamClearance ||
                !IsStaticSeamGround(other) || (step != null && step != other))
                return null;
            step = other;
        }
        return step;
    }

    private bool IsStaticSeamGround(Collider2D other)
    {
        return other != null && !other.isTrigger && !other.usedByEffector &&
            (_groundLayer.value & (1 << other.gameObject.layer)) != 0 &&
            (_thinPlatformLayer.value & (1 << other.gameObject.layer)) == 0 &&
            (other.attachedRigidbody == null || other.attachedRigidbody.bodyType == RigidbodyType2D.Static) &&
            !Physics2D.GetIgnoreCollision(_bodyCollider, other);
    }

    private bool HasSeamClearance(Bounds bounds, float lift, float travel, ContactFilter2D filter)
    {
        // 위로 움직일 때의 머리 공간과, 올라간 몸이 옆으로 이동할 범위 전체를 검사합니다.
        Vector2 headCenter = new Vector2(bounds.center.x, bounds.max.y + lift * 0.5f);
        if (HasSeamObstacle(headCenter, new Vector2(bounds.size.x, lift), filter))
            return false;
        Vector2 center = (Vector2)bounds.center + new Vector2(travel * 0.5f, lift);
        Vector2 size = new Vector2(bounds.size.x + Mathf.Abs(travel), bounds.size.y);
        return !HasSeamObstacle(center, size, filter);
    }

    private bool HasSeamObstacle(Vector2 center, Vector2 size, ContactFilter2D filter)
    {
        int count = Physics2D.OverlapBox(center, size, 0f, filter, _seamObstacles);
        // 결과가 배열을 넘었을 가능성이 있으면 보정하지 않습니다.
        if (count == _seamObstacles.Length)
            return true;
        for (int i = 0; i < count; i++)
        {
            Collider2D other = _seamObstacles[i];
            if (other.attachedRigidbody == _rigidbody2D ||
                Physics2D.GetIgnoreCollision(_bodyCollider, other) || IsIgnoredThinPlatform(other))
                continue;
            return true;
        }
        return false;
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
            float speed = _isCrouching ? _crouchMoveSpeed : _moveSpeed;
            return input * speed * _jumpDistanceMultiplier;
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

        if (!_bodyCollider.isActiveAndEnabled || !_rigidbody2D.simulated)
        {
            RestoreThinPlatformCollisions();
            return;
        }

        bool dropping = IsDroppingThroughPlatforms();
        Bounds bounds = _bodyCollider.bounds;
        // 옆으로 진입하기 전에 감지하고, 남아 있는 접촉도 급경사 제한보다 먼저 무시합니다.
        Vector2 travel = _rigidbody2D.linearVelocity * Time.fixedDeltaTime;
        travel.x = CurrentMoveVelocity * Time.fixedDeltaTime;
        Vector2 size = (Vector2)bounds.size +
            new Vector2(Mathf.Abs(travel.x), Mathf.Abs(travel.y)) + Vector2.one * 0.1f;
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(_thinPlatformLayer);
        filter.useTriggers = false;
        Physics2D.OverlapBox((Vector2)bounds.center + travel * 0.5f, size, 0f,
            filter, _nearbyThinPlatforms);
        int contactCount = _nearbyThinPlatforms.Count > 0 ? _bodyCollider.GetContacts(_groundContacts) : 0;
        for (int i = 0; i < _nearbyThinPlatforms.Count; i++)
        {
            Collider2D platform = _nearbyThinPlatforms[i];
            if (platform.attachedRigidbody == _rigidbody2D ||
                _ignoredThinPlatforms.Contains(platform) ||
                Physics2D.GetIgnoreCollision(_bodyCollider, platform))
                continue;
            if (!dropping && !ShouldPassThinPlatform(platform, bounds, filter, contactCount))
                continue;
            Physics2D.IgnoreCollision(_bodyCollider, platform, true);
            _rigidbody2D.WakeUp();
            _ignoredThinPlatforms.Add(platform);
            if (!dropping)
                _sidePassThinPlatforms.Add(platform);
        }

        for (int i = _ignoredThinPlatforms.Count - 1; i >= 0; i--)
        {
            Collider2D platform = _ignoredThinPlatforms[i];
            if (platform == null)
            {
                _sidePassThinPlatforms.Remove(platform);
                _ignoredThinPlatforms.RemoveAt(i);
                continue;
            }
            if (!platform.isActiveAndEnabled || !_bodyCollider.isActiveAndEnabled ||
                !_rigidbody2D.simulated)
            {
                Physics2D.IgnoreCollision(_bodyCollider, platform, false);
                _sidePassThinPlatforms.Remove(platform);
                _ignoredThinPlatforms.RemoveAt(i);
                continue;
            }

            if (dropping && _nearbyThinPlatforms.Contains(platform))
            {
                // 명시적으로 내려가기를 누르면 옆 통과가 아닌 하강 통과로 유지합니다.
                _sidePassThinPlatforms.Remove(platform);
                Physics2D.IgnoreCollision(_bodyCollider, platform, true);
                continue;
            }

            // 옆 통과 상태가 상승 점프와 위에서의 착지까지 막지 않도록 먼저 해제합니다.
            if (_sidePassThinPlatforms.Contains(platform) &&
                (_rigidbody2D.linearVelocity.y > 0.1f ||
                 (TryGetThinPlatformSurfaceHeight(platform, bounds.center.x, filter, out float surfaceY) &&
                  bounds.min.y >= surfaceY + 0.005f)))
            {
                Physics2D.IgnoreCollision(_bodyCollider, platform, false);
                _sidePassThinPlatforms.Remove(platform);
                _ignoredThinPlatforms.RemoveAt(i);
                continue;
            }

            // 접촉 전에 무시한 발판을 아직 떨어져 있다는 이유로 즉시 복원하지 않습니다.
            if (_nearbyThinPlatforms.Contains(platform) &&
                _sidePassThinPlatforms.Contains(platform) &&
                ShouldPassThinPlatform(platform, bounds, filter, contactCount))
            {
                Physics2D.IgnoreCollision(_bodyCollider, platform, true);
                continue;
            }
            ColliderDistance2D separation = _bodyCollider.Distance(platform);
            // S 또는 Space를 일찍 놓아도 몸이 발판을 벗어날 때까지 통과를 유지합니다.
            if (separation.isValid && !separation.isOverlapped && separation.distance > ThinPlatformClearance)
            {
                Physics2D.IgnoreCollision(_bodyCollider, platform, false);
                _sidePassThinPlatforms.Remove(platform);
                _ignoredThinPlatforms.RemoveAt(i);
            }
            else
            {
                Physics2D.IgnoreCollision(_bodyCollider, platform, true);
            }
        }
    }

    private bool ShouldPassThinPlatform(Collider2D platform, Bounds bodyBounds,
        ContactFilter2D filter, int contactCount)
    {
        // ThinPlatform 레이어여도 일방향 발판 설정이 없는 콜라이더는 변경하지 않습니다.
        if (!platform.usedByEffector || !platform.TryGetComponent(out PlatformEffector2D effector) ||
            !effector.enabled || !effector.useOneWay)
            return false;

        // 상승 통과는 Effector가 처리합니다. 표면 아래에 있다는 이유만으로 무시하지 않습니다.
        float moveVelocity = CurrentMoveVelocity;
        if (_rigidbody2D.linearVelocity.y > 0.1f || Mathf.Abs(moveVelocity) <= 0.01f)
            return false;
        Bounds platformBounds = platform.bounds;
        bool enteringFromSide = moveVelocity > 0f
            ? bodyBounds.max.x <= platformBounds.min.x + ThinPlatformClearance
            : bodyBounds.min.x >= platformBounds.max.x - ThinPlatformClearance;
        if (!enteringFromSide)
            return false;

        for (int i = 0; i < contactCount; i++)
        {
            ContactPoint2D contact = _groundContacts[i];
            if ((contact.collider == platform || contact.otherCollider == platform) &&
                contact.normal.y >= MinimumGroundNormalY &&
                Mathf.Abs(contact.point.y - bodyBounds.min.y) <= ThinPlatformClearance)
                return false;
        }

        return TryGetThinPlatformSurfaceHeight(platform, bodyBounds.center.x, filter, out float surfaceY) &&
            bodyBounds.min.y < surfaceY - ThinPlatformClearance;
    }

    private bool TryGetThinPlatformSurfaceHeight(Collider2D platform, float bodyCenterX,
        ContactFilter2D filter, out float surfaceY)
    {
        surfaceY = 0f;
        Bounds platformBounds = platform.bounds;
        float inset = Mathf.Min(ThinPlatformClearance, platformBounds.size.x * 0.25f);
        float probeX = Mathf.Clamp(bodyCenterX,
            platformBounds.min.x + inset, platformBounds.max.x - inset);
        Vector2 origin = new Vector2(probeX, platformBounds.max.y + ThinPlatformClearance);
        Physics2D.Raycast(origin, Vector2.down, filter, _thinPlatformSurfaceHits,
            platformBounds.size.y + ThinPlatformClearance * 2f);
        // 전체 bounds의 최고점 대신 실제 진입 위치의 표면을 확인합니다.
        for (int i = 0; i < _thinPlatformSurfaceHits.Count; i++)
        {
            RaycastHit2D hit = _thinPlatformSurfaceHits[i];
            if (hit.collider == platform && hit.normal.y > 0f && hit.fraction > 0f)
            {
                surfaceY = hit.point.y;
                return true;
            }
        }
        return false;
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
        _sidePassThinPlatforms.Clear();
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
        if (Mathf.Abs(input) <= 0.01f || input * _runUpDirection < 0f)
            ResetRunUp();

        // 입력을 놓거나 반대로 누른 뒤에는 일반 점프 속도로 조작합니다.
        if (_runUpJumpDirection != 0f && input * _runUpJumpDirection <= 0.01f)
        {
            _runUpJumpDirection = 0f;
            _jumpDistanceMultiplier = _normalJumpDistanceMultiplier;
        }

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
        ResetJumpDistance();
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
            // 실제 이동 제한과 동일하게 ThinPlatform은 벽 막힘 애니메이션에서도 제외합니다.
            if (IsThinPlatform(contact.collider) || IsThinPlatform(contact.otherCollider))
                continue;
            // 입력을 막는 옆면만 벽으로 보고, 바닥과 걸어 올라갈 수 있는 경사는 제외합니다.
            if (contact.normal.x * direction < 0f && IsSteepSurface(contact.normal))
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
        UpdateRunUpState(rawGrounded);
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
        {
            _jumpInProgress = false;
            ResetJumpDistance();
        }
        _wasGroundedForAnimation = grounded;
    }

    private bool HasRunUpMovement()
    {
        if (_isSpectatorMode || _isCrouching || ReadCrouchInput() ||
            _playerInput == null || !_playerInput.isActiveAndEnabled ||
            !_playerInput.inputIsActive || Mathf.Abs(_moveInput) <= 0.01f)
            return false;

        // 입력만 유지한 시간 대신, 이전 물리 처리 후 실제 이동 속도를 확인합니다.
        float minimumSpeed = Mathf.Max(0.01f, _moveSpeed * 0.5f);
        return _rigidbody2D.linearVelocity.x * Mathf.Sign(_moveInput) >= minimumSpeed;
    }

    private void UpdateRunUpState(bool grounded)
    {
        if (_wallCling != null && _wallCling.IsClinging)
        {
            ResetJumpDistance();
            return;
        }

        if (!grounded || _jumpInProgress || !HasRunUpMovement())
        {
            ResetRunUp();
            return;
        }

        float direction = Mathf.Sign(_moveInput);
        if (_runUpDirection != direction)
        {
            _runUpTime = 0f;
            _runUpDirection = direction;
        }

        _runUpTime = Mathf.Min(_runUpTime + Time.fixedDeltaTime, _runUpRequiredTime);
    }

    private void ResetRunUp()
    {
        _runUpTime = 0f;
        _runUpDirection = 0f;
    }

    private void ResetJumpDistance()
    {
        _jumpDistanceMultiplier = 1f;
        _runUpJumpDirection = 0f;
        ResetRunUp();
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
            if (contact.normal.y >= MinimumGroundNormalY - 0.0001f &&
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

        ResetJumpDistance();
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
        bool runUpJump = _runUpDirection * _moveInput > 0.01f &&
            _runUpTime >= _runUpRequiredTime && HasRunUpMovement();
        _jumpDistanceMultiplier = runUpJump
            ? _runUpJumpDistanceMultiplier : _normalJumpDistanceMultiplier;
        _runUpJumpDirection = runUpJump ? Mathf.Sign(_moveInput) : 0f;
        ResetRunUp();

        float inputTime = _moveInputStartedTime < 0f ? 0f : Time.time - _moveInputStartedTime;
        bool idleJump = !runUpJump &&
            (Mathf.Abs(_moveInput) <= 0.01f || inputTime < _idleJumpInputTime);
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

        if (_interactionController == null)
        {
            return;
        }

        if (UIManager.Inst != null)
        {
            UIManager.Inst.TryCloseLoggingMiningResult();
        }

        bool requested = _interactionController.TryRequestInteraction();

        Debug.Log(
            $"상호작용 요청: {requested}, " +
            $"Spawn: {_interactionController.IsSpawned}, " +
            $"Owner: {_interactionController.IsOwner}, " +
            $"대상: {_interactionController.SelectedTarget}");
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        if (_isSpectatorMode || _networkObject == null || _networkObject.IsSpawned == false || _networkObject.IsOwner == false || _runeAttackController == null)
        {
            return;
        }

        _runeAttackController.RequestAttack(_lookRight);

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
        if (_networkObject == null ||
        !_networkObject.IsSpawned ||
        !_networkObject.IsOwner)
        {
            return;
        }

        if (UIManager.Inst != null &&
            UIManager.Inst.TryCloseLoggingMiningResult())
        {
            return;
        }

        Debug.Log($"{nameof(OnOpenSettings)} 실행");
    }

    private void OnOpenRuneInventory(InputAction.CallbackContext context)
    {
        if (_networkObject == null ||
            !_networkObject.IsSpawned ||
            !_networkObject.IsOwner)
        {
            return;
        }

        OpenRuneInventoryAsync().Forget();
    }

    private async UniTask OpenRuneInventoryAsync()
    {
        if (_openingRuneInventory)
        {
            return;
        }

        UIManager uiManager = UIManager.Inst;
        ServiceManager services = ServiceManager.Inst;

        if (uiManager == null || services == null)
        {
            return;
        }

        _openingRuneInventory = true;

        try
        {
            RuneInventoryUI view = await uiManager.OpenRuneInventoryUIAsync();

            if (uiManager == null || view == null)
            { 
                return;
            }

            // UI 로딩 중 플레이어가 사라지거나 연결이 종료된 경우.
            if (this == null || isActiveAndEnabled == false || _networkObject == null || _networkObject.IsSpawned == false || _networkObject.IsOwner == false || services == null)
            {
                uiManager.CloseRuneInventoryUI();
                return;
            }

            if (services.NetworkRuneService.BindRuneInventoryView(view) == false)
            {
                uiManager.CloseRuneInventoryUI();
            }
        }
        finally
        {
            _openingRuneInventory = false;
        }
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

    private void OnFieldInteractCanceled(InputAction.CallbackContext context)
    {
        if (_isMiniGameMode)
            return;

        if (_interactionController == null)
            return;

        InteractableObject target = _interactionController.LockedTarget;

        if (target != null && target.UsesMiniGameInput)
            return;

        _interactionController.CancelRequestedInteraction();
    }

    private void OnInteractionStartResolved(bool started)
    {
        if (!started || _interactionController == null)
            return;

        InteractableObject target = _interactionController.LockedTarget;

        if (target == null || !target.UsesMiniGameInput)
            return;

        EnterMiniGameMode();
    }

    private void EnterMiniGameMode()
    {
        if (_isMiniGameMode)
            return;

        _isMiniGameMode = true;
        _miniGameEnteredFrame = Time.frameCount;

        if (_isSpectatorMode)
            SetSpectatorMode(false);

        SetMoveInput(0f);
        _wallCling?.ResetState();

        Vector2 velocity = _rigidbody2D.linearVelocity;
        velocity.x = 0f;
        _rigidbody2D.linearVelocity = velocity;

        _playerInput.SwitchCurrentActionMap("MiniGame");
    }

    private void ExitMiniGameMode()
    {
        if (!_isMiniGameMode)
            return;

        _playerInput.SwitchCurrentActionMap("Player");
        _isMiniGameMode = false;
    }

    private void OnMiniGameConfirm(InputAction.CallbackContext context)
    {
        if (!_isMiniGameMode || Time.frameCount == _miniGameEnteredFrame)
            return;

        _interactionController?.ConfirmCurrentInteraction();
    }

    private void OnMiniGameCancel(InputAction.CallbackContext context)
    {
        if (!_isMiniGameMode)
            return;

        _interactionController?.CancelRequestedInteraction();
    }
}
