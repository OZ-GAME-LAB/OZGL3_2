using System.Collections.Generic;
using UnityEngine;

/// <summary>지역 이름과 관계없이 카메라가 머물 수 있는 2D 영역을 등록합니다.</summary>
[DisallowMultipleComponent]
public class CameraBoundsArea : MonoBehaviour
{
    private static readonly List<CameraBoundsArea> ActiveAreas = new List<CameraBoundsArea>();

    [Header("카메라 제한 영역")]
    [SerializeField] private Collider2D _boundsCollider;
    [Tooltip("영역이 겹치면 높은 값이 우선합니다. 같은 값이면 현재 영역을 유지합니다.")]
    [SerializeField] private int _priority;

    public Collider2D BoundsCollider => _boundsCollider;
    public bool IsAvailable => isActiveAndEnabled && _boundsCollider != null
        && _boundsCollider.enabled && _boundsCollider.gameObject.activeInHierarchy;

    private void Reset()
    {
        TryGetComponent(out _boundsCollider);
    }

    private void OnEnable()
    {
        if (_boundsCollider == null)
            TryGetComponent(out _boundsCollider);

        if (!(_boundsCollider is BoxCollider2D) && !(_boundsCollider is PolygonCollider2D)
            && !(_boundsCollider is CompositeCollider2D))
        {
            Debug.LogError("카메라 영역에는 Box, Polygon 또는 Composite Collider 2D가 필요합니다.", this);
            return;
        }

        if (!ActiveAreas.Contains(this))
            ActiveAreas.Add(this);

        // 런타임에 생성된 맵도 첫 카메라 갱신부터 정확한 위치로 검사합니다.
        Physics2D.SyncTransforms();
    }

    private void OnDisable()
    {
        ActiveAreas.Remove(this);
    }

    public bool Contains(Vector2 position)
    {
        return IsAvailable && _boundsCollider.OverlapPoint(position);
    }

    public static CameraBoundsArea SelectArea(Vector2 position, CameraBoundsArea current)
    {
        CameraBoundsArea selected = current != null && current.Contains(position) ? current : null;
        for (int i = ActiveAreas.Count - 1; i >= 0; i--)
        {
            CameraBoundsArea area = ActiveAreas[i];
            if (area == null)
            {
                ActiveAreas.RemoveAt(i);
                continue;
            }

            if (area.Contains(position) && (selected == null || area._priority > selected._priority))
                selected = area;
        }
        return selected;
    }
}
