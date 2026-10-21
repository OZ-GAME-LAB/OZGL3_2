using UnityEngine;
using UnityEngine.InputSystem;



public class PlayerControllerView : MonoBehaviour
{
    [Header("이동 설정")]
    [SerializeField] private float _moveSpeed = 8f;
    [SerializeField] private float _jumpForce = 5f;
    [SerializeField] private PlayerInput _playerInput;
    [SerializeField] private Rigidbody2D _rigidbody2D;
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

    [Header("지면 체크 설정")]
    [SerializeField] private Transform _groundCheck;    // 발 밑에 배치할 빈 오브젝트
    [SerializeField] private float _checkRadius = 0.5f; // 체크 범위
    [SerializeField] private LayerMask _groundLayer;    // 지면으로 인식할 레이어 (Platforms 등)

    private bool _isGrounded;
    private bool _lookRight = true;

    // 상호작용 컨트롤러 추가
    [SerializeField]
    private PlayerInteractionController _interactionController;

    void Awake()
    {
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


    private void OnMove(InputAction.CallbackContext context)
    {
        Vector2 move = context.ReadValue<Vector2>();

        _rigidbody2D.linearVelocity = new Vector2(
            move.x * _moveSpeed,
            _rigidbody2D.linearVelocity.y
        );

        if (move.x > 0 && !_lookRight)
            Flip();
        else if (move.x < 0 && _lookRight)
            Flip();
    }

    private void OnMoveCanceled(InputAction.CallbackContext context)
    {
        _rigidbody2D.linearVelocity = new Vector2(
            0f,
            _rigidbody2D.linearVelocity.y
        );
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        _isGrounded = Physics2D.OverlapCircle(_groundCheck.position, _checkRadius, _groundLayer);
        if (_isGrounded)
            _rigidbody2D.AddForce(Vector2.up * _jumpForce, ForceMode2D.Impulse);
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
