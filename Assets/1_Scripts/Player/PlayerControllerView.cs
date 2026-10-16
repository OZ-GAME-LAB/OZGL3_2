using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;



public class PlayerControllerView : MonoBehaviour
{
    [Header("이동 설정")]
    [SerializeField] private float _moveSpeed = 8f;
    [SerializeField] private float _jumpForce = 5f;
    [SerializeField] private PlayerInput _playerInput;
    [SerializeField] private Rigidbody2D _rigidbody2D;
    private InputAction moveAction;
    private InputAction jumpAction;

    [Header("지면 체크 설정")]
    [SerializeField] private Transform _groundCheck;    // 발 밑에 배치할 빈 오브젝트
    [SerializeField] private float _checkRadius = 0.5f; // 체크 범위
    [SerializeField] private LayerMask _groundLayer;    // 지면으로 인식할 레이어 (Platforms 등)

    private bool _isGrounded;
    private bool _lookRight = true;



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
        moveAction = _playerInput.actions["Move"];
        jumpAction = _playerInput.actions["Jump"];
    }

    private void OnEnable()
    {
        moveAction.performed += OnMove;
        moveAction.canceled += OnMoveCanceled;
        jumpAction.performed += OnJump;
    }

    private void OnDisable()
    {
        moveAction.performed -= OnMove;
        moveAction.canceled -= OnMoveCanceled;
        jumpAction.performed -= OnJump;
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
