using System.Collections;
using UnityEngine;

/// <summary>문 이미지 높이를 기준으로 문 전체를 위쪽으로 한 번 엽니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class EventDoorOpen : MonoBehaviour
{
    [Header("문 열림 설정")]
    [SerializeField, Min(0f)] private float _openDuration = 2.5f;
    [Tooltip("문 이미지의 월드 높이에 대한 이동 비율입니다. 50이면 절반만큼 이동합니다.")]
    [SerializeField, Range(0f, 100f)] private float _movePercent = 50f;
    [SerializeField] private bool _openOnStart;

    private SpriteRenderer _spriteRenderer;
    private Coroutine _openRoutine;
    private Vector3 _startPosition;
    private Vector3 _targetPosition;
    private float _duration;
    private float _elapsed;
    private bool _hasStarted;
    private bool _isOpen;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        if (_openOnStart)
            Open();
    }

    /// <summary>이벤트에서 호출합니다. 중단된 연출은 남은 구간부터 이어집니다.</summary>
    public void Open()
    {
        if (!isActiveAndEnabled || _isOpen || _openRoutine != null)
            return;

        if (!_hasStarted)
        {
            if (_spriteRenderer == null || _spriteRenderer.sprite == null)
            {
                Debug.LogWarning("문을 열려면 SpriteRenderer에 문 이미지가 필요합니다.", this);
                return;
            }

            _startPosition = transform.position;
            float distance = _spriteRenderer.bounds.size.y * Mathf.Clamp(_movePercent, 0f, 100f) * 0.01f;
            _targetPosition = _startPosition + Vector3.up * distance;
            _duration = Mathf.Max(0f, _openDuration);
            _hasStarted = true;
        }

        if (_duration <= 0f || _startPosition == _targetPosition)
        {
            CompleteOpen();
            return;
        }

        _openRoutine = StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        while (_elapsed < _duration)
        {
            yield return null;
            _elapsed = Mathf.Min(_elapsed + Time.deltaTime, _duration);
            transform.position = Vector3.Lerp(_startPosition, _targetPosition, _elapsed / _duration);
        }

        CompleteOpen();
    }

    private void CompleteOpen()
    {
        transform.position = _targetPosition;
        _isOpen = true;
        _openRoutine = null;
    }

    private void OnDisable()
    {
        if (_openRoutine == null)
            return;

        StopCoroutine(_openRoutine);
        _openRoutine = null;
    }
}
