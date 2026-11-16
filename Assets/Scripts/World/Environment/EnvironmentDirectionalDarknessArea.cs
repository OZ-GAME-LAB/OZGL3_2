using UnityEngine;

/// <summary>기준점에서 지정한 월드 방향으로 진행한 거리를 화면 한쪽의 암전 농도로 바꿉니다.</summary>
[DisallowMultipleComponent]
public sealed class EnvironmentDirectionalDarknessArea : MonoBehaviour
{
    public enum ScreenSide { Left, Right, Both }

    [Header("적용 구역과 화면 방향")]
    [SerializeField] private Collider2D _scope;
    [SerializeField] private ScreenSide _screenSide;
    [Header("이 오브젝트 위치가 암전 시작점 — 방향은 월드 좌표")]
    [SerializeField] private Vector2 _darkeningDirection = Vector2.right;
    [SerializeField, Min(0.01f)] private float _fullDarknessDistance = 20f;
    [SerializeField, Range(0f, 1f)] private float _maximumAlpha = 0.8f;

    public ScreenSide Side => _screenSide;

    public float Evaluate(Vector2 playerPosition)
    {
        if (!isActiveAndEnabled || _darkeningDirection.sqrMagnitude < 0.0001f)
            return 0f;
        if (_scope != null && (!_scope.enabled || !_scope.gameObject.activeInHierarchy ||
            !_scope.OverlapPoint(playerPosition)))
            return 0f;

        float distance = Vector2.Dot(playerPosition - (Vector2)transform.position,
            _darkeningDirection.normalized);
        float t = Mathf.Clamp01(distance / Mathf.Max(0.01f, _fullDarknessDistance));
        return Mathf.SmoothStep(0f, 1f, t) * Mathf.Clamp01(_maximumAlpha);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 direction = _darkeningDirection.normalized;
        Vector3 end = transform.position + direction * Mathf.Max(0.01f, _fullDarknessDistance);
        Gizmos.color = new Color(1f, 0.5f, 0f, 1f);
        Gizmos.DrawWireSphere(transform.position, 0.3f);
        Gizmos.DrawLine(transform.position, end);
        Vector3 side = new Vector3(-direction.y, direction.x, 0f);
        Gizmos.DrawLine(end, end - direction + side * 0.5f);
        Gizmos.DrawLine(end, end - direction - side * 0.5f);
    }
}
