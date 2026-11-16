using UnityEngine;

// 외형과 실제 감지 영역을 함께 저장하는 프리팹입니다. 상태 결정은 본체가 담당합니다.
public sealed class ResourceInteractionLayout : MonoBehaviour
{
    [Header("외형과 상호작용 영역")]
    [SerializeField] private SpriteRenderer _visual;
    [SerializeField] private Sprite _depletedSprite;
    [SerializeField] private BoxCollider2D[] _areas;
    [SerializeField] private ResourceRangeEffect[] _effects;
    [SerializeField] private bool _requiresSide;
    [Header("벌목 타격 기준")]
    [SerializeField] private Transform _loggingHitPoint;
    [SerializeField] private LoggingTreeEffect _loggingEffect;
    private Sprite _fullSprite;
    private bool _dormant;

    public bool RequiresSide => _requiresSide;
    public Vector3 TargetPosition => _visual.transform.position;
    public LoggingTreeEffect LoggingEffect => _loggingEffect;

    public bool TryGetLoggingHitPoint(out Vector3 position)
    {
        position = _loggingHitPoint != null ? _loggingHitPoint.position : default;
        return _loggingHitPoint != null;
    }

    public void Initialize()
    {
        _fullSprite = _visual.sprite;
        if (_loggingEffect != null) _loggingEffect.Initialize();
        for (int i = 0; i < _areas.Length; i++)
        {
            _areas[i].enabled = true;
            _effects[i].Configure(_areas[i]);
        }
    }

    public void SetAvailablePresentation(bool visible, bool showEffects)
    {
        _visual.enabled = visible && (!_dormant || _depletedSprite != null);
        for (int i = 0; i < _areas.Length; i++)
        {
            _areas[i].enabled = visible && !_dormant;
            _effects[i].SetVisible(visible && !_dormant && showEffects);
        }
    }

    public void SetDormant(bool dormant)
    {
        _dormant = dormant;
        _visual.sprite = dormant && _depletedSprite != null ? _depletedSprite : _fullSprite;
    }

    // 몸통이 영역에 살짝 걸쳐도 캐릭터 중심이 중앙이면 시작하지 않습니다.
    public bool ContainsPlayerPosition(Vector3 position)
    {
        if (!_requiresSide) return true;
        foreach (BoxCollider2D area in _areas)
        {
            Vector2 local = area.transform.InverseTransformPoint(position);
            if (area.enabled && Mathf.Abs(local.x - area.offset.x) <= area.size.x * 0.5f)
                return true;
        }
        return false;
    }
}
