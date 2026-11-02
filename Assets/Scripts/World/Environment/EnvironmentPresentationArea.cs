using UnityEngine;

/// <summary>카메라 제한과 독립적인 환경 연출 영역입니다.</summary>
[DisallowMultipleComponent]
public sealed class EnvironmentPresentationArea : MonoBehaviour
{
    [Header("영역 — 면적이 있는 Trigger Collider2D")]
    [SerializeField] private Collider2D _areaCollider;
    [SerializeField] private int _priority;

    [Header("지역 연출 — 비워 두면 효과 없는 지역")]
    [SerializeField] private EnvironmentPresentationProfile _profile;

    public int Priority => _priority;
    public EnvironmentPresentationProfile Profile => _profile;

    private void Reset()
    {
        TryGetComponent(out _areaCollider);
    }

    public bool Contains(Vector2 position)
    {
        return isActiveAndEnabled && _areaCollider != null
            && _areaCollider.enabled && _areaCollider.gameObject.activeInHierarchy
            && _areaCollider.isTrigger
            && (_areaCollider is BoxCollider2D || _areaCollider is PolygonCollider2D
                || _areaCollider is CircleCollider2D || _areaCollider is CapsuleCollider2D)
            && _areaCollider.OverlapPoint(position);
    }

    private void OnValidate()
    {
        if (_areaCollider != null && !_areaCollider.isTrigger)
            Debug.LogWarning("환경 연출 영역은 플레이어를 막지 않도록 Is Trigger를 켜 주세요.", this);
    }
}
