using UnityEngine;

public class MonsterVisual : MonoBehaviour
{
    [SerializeField] private Transform _groundCheck;
    [SerializeField] private Transform _rightWallCheck;
    [SerializeField] private Transform _leftWallCheck;
    [SerializeField] private Transform _eyePosition;
    [SerializeField] private Collider2D _monsterCollider;

    public bool CheckMonsterVisualReady()
    {
        if (_groundCheck == null || _rightWallCheck == null || _leftWallCheck == null || _eyePosition == null || _monsterCollider == null)
        {
            Debug.Log("몬스터 외형 프리팹에 체크용 오브젝트가 할당되지 않았습니다.");
            return false;
        }

        return true;
    }

    public Transform GroundCheckReturn()
    {
        return _groundCheck;
    }

    public Transform RightCheckReturn()
    {
        return _rightWallCheck;
    }

    public Transform LeftCheckReturn()
    {
        return _leftWallCheck;
    }

    public Transform EyePositionReturn()
    {
        return _eyePosition;
    }

    public Collider2D MonsterColliderReturn()
    {
        return _monsterCollider;
    }
}


