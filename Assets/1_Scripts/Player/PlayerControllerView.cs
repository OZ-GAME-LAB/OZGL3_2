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
    private float _moveInputStartedTime = -1f;
    private bool _jumpInProgress;
    private bool _wasGroundedForAnimation = true;
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
    private static readonly int IsIdleJumpHash = Animator.StringToHash("IsIdleJump");
    private static readonly int RunningJumpHash = Animator.StringToHash("RunningJump");
    private static readonly int IdleJumpHash = Animator.StringToHash("IdleJump");
    private readonly ContactPoint2D[] _animationGroundContacts = new ContactPoint2D[16];
    private InputAction _moveAction;
    private InputAction _jumpAction;
    private InputAction _upAction;
    private InputAction _downAction;
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

    private bool _isGrounded;
    private bool _lookRight = true;
    private readonly Collider2D[] _groundResults = new Collider2D[16];

    // 상호작용 컨트롤러 추가
    [SerializeField]
    private PlayerInteractionController _interactionController;

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
    }

    private void OnEnable()
    {
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
    }

    private void OnDisable()
    {
        if (_isSpectatorMode)
            SetSpectatorMode(false);

        SetMoveInput(0f);
        ResetJumpAnimation();
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

    private void SetSpectatorMode(bool enabled)
    {
        if (_isSpectatorMode == enabled)
            return;

        _isSpectatorMode = enabled;
        SetMoveInput(0f);
        ResetJumpAnimation();
        _isGrounded = false;

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
        UpdateFallAnimation();

        if (_isSpectatorMode)
            return;

        _rigidbody2D.linearVelocity = new Vector2(
            _moveInput * _moveSpeed,
            _rigidbody2D.linearVelocity.y
        );
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
    }

    private void ResetJumpAnimation()
    {
        _jumpInProgress = false;
        _wasGroundedForAnimation = true;
        if (_animator == null)
            return;
        _animator.ResetTrigger(RunningJumpHash);
        _animator.ResetTrigger(IdleJumpHash);
        _animator.SetBool(IsIdleJumpHash, false);
        _animator.SetBool(IsGroundedHash, true);
        _animator.SetFloat(VerticalSpeedHash, 0f);
    }

    private void UpdateMovementAnimation()
    {
        if (_animator == null)
            return;

        // 이동은 기존 물리 처리에 맡기고, 그림의 상태만 전달합니다.
        _animator.SetBool(IsMovingHash, !_isSpectatorMode && Mathf.Abs(_moveInput) > 0.01f);
    }

    private void UpdateFallAnimation()
    {
        if (_rigidbody2D == null)
            return;
        if (_networkObject != null && _networkObject.IsSpawned && !_networkObject.IsOwner)
            return;

        // 관전자 모드에는 물리적인 낙하가 없으므로 지상 상태로 표시합니다.
        float verticalSpeed = _rigidbody2D.linearVelocity.y;
        bool grounded = _isSpectatorMode ||
            (verticalSpeed <= 0.1f && HasGroundContactForAnimation());
        // 점프 종류는 공중 입력으로 바꾸지 않습니다. 걸어서 떨어질 때만 해제합니다.
        if (!grounded && _wasGroundedForAnimation && !_jumpInProgress && _animator != null)
            _animator.SetBool(IsIdleJumpHash, false);
        if (grounded && !_wasGroundedForAnimation)
            _jumpInProgress = false;
        _wasGroundedForAnimation = grounded;
        if (_animator == null)
            return;
        _animator.SetBool(IsGroundedHash, grounded);
        _animator.SetFloat(VerticalSpeedHash, _isSpectatorMode ? 0f : verticalSpeed);
    }

    private bool HasGroundContactForAnimation()
    {
        if (_groundCheck == null)
            return false;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(_groundLayer);
        filter.useTriggers = false;
        int count = _rigidbody2D.GetContacts(filter, _animationGroundContacts);
        for (int i = 0; i < count; i++)
        {
            ContactPoint2D contact = _animationGroundContacts[i];
            // 벽이나 발판 옆면은 착지로 보지 않습니다.
            if (contact.normal.y >= 0.5f &&
                Mathf.Abs(contact.point.y - _groundCheck.position.y) <= _checkRadius)
                return true;
        }
        return false;
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        if (_isSpectatorMode || _jumpInProgress)
            return;
        if (_networkObject != null && _networkObject.IsSpawned && !_networkObject.IsOwner)
            return;

        _isGrounded = HasGroundBelowFeet();
        if (!_isGrounded)
            return;

        // 같은 프레임에 이동과 점프가 들어와도 현재 입력을 기준으로 판정합니다.
        SetMoveInput(_moveAction.ReadValue<Vector2>().x);
        float inputTime = _moveInputStartedTime < 0f ? 0f : Time.time - _moveInputStartedTime;
        bool idleJump = Mathf.Abs(_moveInput) <= 0.01f || inputTime < _idleJumpInputTime;
        _jumpInProgress = true;
        _wasGroundedForAnimation = true;
        _rigidbody2D.AddForce(Vector2.up * _jumpForce, ForceMode2D.Impulse);
        if (_animator != null)
        {
            UpdateMovementAnimation();
            _animator.SetBool(IsIdleJumpHash, idleJump);
            _animator.SetBool(IsGroundedHash, false);
            _animator.ResetTrigger(RunningJumpHash);
            _animator.ResetTrigger(IdleJumpHash);
            if (idleJump)
                _animator.SetTrigger(IdleJumpHash);
            else
                _animator.SetTrigger(RunningJumpHash);
        }
    }

    private bool HasGroundBelowFeet()
    {
        if (_groundCheck == null || _rigidbody2D == null)
        {
            Debug.LogWarning("바닥 감지에 Ground Check와 Rigidbody2D가 필요합니다.", this);
            return false;
        }

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(_groundLayer);
        filter.useTriggers = false;

        int count = Physics2D.OverlapCircle(
            _groundCheck.position, _checkRadius, filter, _groundResults);

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = _groundResults[i];
            // 발 위치의 감지 원은 자기 몸과도 겹치므로 같은 Rigidbody는 제외합니다.
            if (hit != null && hit.attachedRigidbody != _rigidbody2D)
                return true;
        }

        return false;
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
        NetworkManager.Inst.InventoryService.BindInventoryView(view);

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
        targetPosition.z = footPosition.z;

        _rigidbody2D.linearVelocity = Vector2.zero;
        _rigidbody2D.angularVelocity = 0f;
        _rigidbody2D.position = new Vector2(targetPosition.x, targetPosition.y);
        transform.position = targetPosition;
        _isGrounded = false;
        ResetJumpAnimation();
        return true;
    }

    void Flip()
    {
        _lookRight = !_lookRight;
        Vector3 scaler = transform.localScale;
        scaler.x *= -1;
        transform.localScale = scaler;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {

    }
    private void OnDrawGizmos()
    {
        if (_groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_groundCheck.position, _checkRadius);
        }
    }

}
