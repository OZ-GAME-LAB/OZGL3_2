using Unity.Netcode;
using UnityEngine;

// 배치 정보만 제공하며, 생성된 본체의 수명은 기존 Spawner가 관리합니다.
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D), typeof(InteractableObjectSpawner))]
public sealed class FieldInteractionPoint : MonoBehaviour
{
    [Header("필드 데이터와 감지 영역")]
    [SerializeField] private string _fieldObjectId;
    [SerializeField] private BoxCollider2D _interactionBounds;
    [SerializeField] private ResourceInteractionLayout _resourceLayout;
    private InteractableObjectSpawner _spawner;

    public string FieldObjectId => _fieldObjectId;

    private void Awake()
    {
        _spawner = GetComponent<InteractableObjectSpawner>();
        if (_interactionBounds == null) _interactionBounds = GetComponent<BoxCollider2D>();
        if (_resourceLayout != null) _resourceLayout.gameObject.SetActive(false);
        // 감지는 서버가 생성하고 동기화한 본체가 담당합니다.
        if (_interactionBounds != null) _interactionBounds.enabled = false;
    }

    public void InitializeOnServer()
    {
        NetworkManager network = NetworkManager.Singleton;
        if (!isActiveAndEnabled || network == null || !network.IsServer || !network.IsListening)
            return;
        if (_spawner == null || _interactionBounds == null || string.IsNullOrWhiteSpace(_fieldObjectId))
        {
            Debug.LogError("필드 포인트의 데이터 ID와 Collider를 확인해 주세요.", this);
            return;
        }
        Transform area = _resourceLayout != null ? _resourceLayout.transform : _interactionBounds.transform;
        Vector3 right = area.TransformVector(Vector3.right);
        Vector3 up = area.TransformVector(Vector3.up);
        if (right.sqrMagnitude <= 0f || up.sqrMagnitude <= 0f ||
            Mathf.Abs(Vector3.Dot(right.normalized, up.normalized)) > 0.001f ||
            Mathf.Abs(right.z) > 0.001f || Mathf.Abs(up.z) > 0.001f)
        {
            Debug.LogError("필드 포인트에는 기울어진 축이나 0 크기 변환을 사용할 수 없습니다.", this);
            return;
        }
        Quaternion rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(right.y, right.x) * Mathf.Rad2Deg);
        Vector3 center = area.TransformPoint(_interactionBounds.offset);
        Vector2 offset = Quaternion.Inverse(rotation) * (center - area.position);
        Vector2 size = new Vector2(_interactionBounds.size.x * right.magnitude,
            _interactionBounds.size.y * up.magnitude);
        if (_resourceLayout != null)
        {
            size = new Vector2(right.magnitude, up.magnitude);
            offset = Vector2.zero;
        }
        _spawner.InitializePlacedPoint(_fieldObjectId, area.position, rotation, size, offset, _resourceLayout != null);
    }
}
