using UnityEngine;

// 플레이어 위치와 무관하게, 프리팹에 배치한 세 기준점을 통과하는 포물선입니다.
public class BossPoisonPath : MonoBehaviour
{
    [Header("왼쪽: 발사 위치 / 1단 발판 통과 위치 / 바닥 도착 위치")]
    [SerializeField] private Transform _leftStart;
    [SerializeField] private Transform _leftPass;
    [SerializeField] private Transform _leftEnd;

    [Header("오른쪽: 발사 위치 / 1단 발판 통과 위치 / 바닥 도착 위치")]
    [SerializeField] private Transform _rightStart;
    [SerializeField] private Transform _rightPass;
    [SerializeField] private Transform _rightEnd;

    public bool TryGetTrajectory(int direction, out Vector3 start, out Vector3 control, out Vector3 end)
    {
        start = control = end = default;
        if (direction != -1 && direction != 1)
            return false;

        Transform startPoint = direction < 0 ? _leftStart : _rightStart;
        Transform passPoint = direction < 0 ? _leftPass : _rightPass;
        Transform endPoint = direction < 0 ? _leftEnd : _rightEnd;
        if (startPoint == null || passPoint == null || endPoint == null)
            return false;

        start = startPoint.position;
        end = endPoint.position;
        return TryCalculateControl(start, passPoint.position, end, out control);
    }

    public static bool TryCalculateControl(Vector3 start, Vector3 pass, Vector3 end, out Vector3 control)
    {
        control = default;
        if (!IsFinite(start) || !IsFinite(pass) || !IsFinite(end))
            return false;

        float distanceX = end.x - start.x;
        if (Mathf.Abs(distanceX) < 0.01f)
            return false;

        // X 방향은 일정한 속도로 이동합니다. 통과점의 X로 해당 시점의 진행률을 구합니다.
        float t = (pass.x - start.x) / distanceX;
        if (t <= 0.01f || t >= 0.99f)
            return false;

        float remaining = 1f - t;
        control = (pass - remaining * remaining * start - t * t * end) / (2f * remaining * t);
        return IsFinite(control);
    }

    public static Vector3 Evaluate(Vector3 start, Vector3 control, Vector3 end, float progress)
    {
        float t = Mathf.Clamp01(progress);
        float remaining = 1f - t;
        return remaining * remaining * start + 2f * remaining * t * control + t * t * end;
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
            && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }

    private void OnDrawGizmosSelected()
    {
        DrawPath(-1, Color.cyan);
        DrawPath(1, Color.green);
    }

    private void DrawPath(int direction, Color color)
    {
        if (!TryGetTrajectory(direction, out Vector3 start, out Vector3 control, out Vector3 end))
            return;

        Gizmos.color = color;
        Vector3 previous = start;
        for (int i = 1; i <= 40; i++)
        {
            Vector3 next = Evaluate(start, control, end, i / 40f);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
        Gizmos.DrawWireSphere(start, 0.25f);
        Gizmos.DrawWireSphere(end, 0.25f);
    }
}
