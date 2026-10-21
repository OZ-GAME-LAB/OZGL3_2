using UnityEngine;

public class Monster : MonoBehaviour
{
    [SerializeField] private Rigidbody2D _rigidbody2D;
    [SerializeField] private Transform _groundCheck;
    [SerializeField] private Transform _rightWallCheck;
    [SerializeField] private Transform _leftWallCheck;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private float _groundCheckRadius = 0.1f;
    [SerializeField] private float _jumpForce = 5f;

    private GameObject[] _patricList;
    private int _patricCurIndexNo = 0;
    private int _patricListCount;

    // test 속도 추후 데이터 반영 시 해당 변수 삭제
    private float _moveSpeed = 3f;
    /////////////////////////////////////////////////////


    public bool MonsterPatricMoveAndCheck(Vector3 targetPosition)
    {
        // TODO : 몬스터 애니메이션 연결 부분 필요(이동 애니메이션 관련 메서드 호출)

        float distanceX = targetPosition.x - _rigidbody2D.position.x;
        Vector2 velocity = _rigidbody2D.linearVelocity;

        if (Mathf.Abs(distanceX) <= 0.05f)
        {
            velocity.x = 0f;
            _rigidbody2D.linearVelocity = velocity;
            return true;
        }

        velocity.x = Mathf.Clamp(distanceX / Time.fixedDeltaTime, -_moveSpeed, _moveSpeed);

        _rigidbody2D.linearVelocity = velocity;
        return false;
    }

    public void MonsterJump()
    {
        bool isGrounded = Physics2D.OverlapCircle(
        _groundCheck.position,
        _groundCheckRadius,
        _groundLayer) != null;

        if (!isGrounded || _rigidbody2D.linearVelocity.y > 0f)
            return;

        _rigidbody2D.AddForce(Vector2.up * _jumpForce, ForceMode2D.Impulse);
    }

    public Vector3 MonsterPatricPositionSelect()
    {
        if (_patricCurIndexNo >= _patricListCount)
        {
            _patricCurIndexNo = 0;
        }

        Vector3 target = _patricList[_patricCurIndexNo].transform.position;

        _patricCurIndexNo++;

        return target;
    }

    public void MonsterChaisingTarget()
    {

    }
}
