using UnityEngine;
using System.Collections;

public class BossThornAttackObject : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _warningVisual;
    [SerializeField] private SpriteMask _thornMask;
    [SerializeField] private SpriteRenderer _thornVisual;
    [SerializeField] private BoxCollider2D _hitbox;

    [SerializeField, Min(0.01f)]
    private float _warningHeight = 0.1f;

    [SerializeField, Min(0f)]
    private float _warningSeconds = 1f;

    [SerializeField, Min(0.01f)]
    private float _riseSeconds = 0.2f;

    [SerializeField, Min(0f)]
    private float _holdSeconds = 1f;

    public event System.Action AttackStarted;

    private bool _initialized;

    public bool IsPlaying { get; private set; }

    private float _width;
    private float _height;

    private void OnDisable()
    {
        StopAllCoroutines();

        if (_warningVisual != null)
            _warningVisual.gameObject.SetActive(false);

        if (_thornVisual != null)
            _thornVisual.gameObject.SetActive(false);

        if (_hitbox != null)
            _hitbox.gameObject.SetActive(false);

        IsPlaying = false;
    }

    public bool Initialize(float width, float height)
    {
        if (IsPlaying)
            return false;

        _initialized = false;

        if (_warningVisual == null || _warningVisual.sprite == null ||
            _thornMask == null || _thornMask.sprite == null ||
            _thornVisual == null || _thornVisual.sprite == null ||
            _hitbox == null)
        {
            Debug.LogError("가시 공격 프리팹의 참조가 연결되지 않았습니다.", this);
            return false;
        }

        if (float.IsNaN(width) || float.IsInfinity(width) || width <= 0f ||
            float.IsNaN(height) || float.IsInfinity(height) || height <= 0f)
        {
            Debug.LogError("가시 공격의 너비와 높이가 올바르지 않습니다.", this);
            return false;
        }

        Vector3 thornScale = _thornVisual.transform.localScale;

        if (thornScale.x <= 0f || thornScale.y <= 0f)
        {
            Debug.LogError("가시 외형의 Scale은 양수여야 합니다.", this);
            return false;
        }

        _width = width;
        _height = height;

        Vector3 warningSpriteSize = _warningVisual.sprite.bounds.size;

        _warningVisual.transform.localScale = new Vector3(_width / warningSpriteSize.x, _warningHeight / warningSpriteSize.y, 1f);

        _warningVisual.transform.localPosition = new Vector3(0f, _warningHeight * 0.5f, 0f);

        Vector3 maskSpriteSize = _thornMask.sprite.bounds.size;

        _thornMask.transform.localScale = new Vector3(_width / maskSpriteSize.x, _height / maskSpriteSize.y, 1f);

        _thornMask.transform.localPosition = new Vector3(0f, _height * 0.5f, 0f);

        _thornVisual.size = new Vector2(_width / thornScale.x, _height / thornScale.y);

        _thornVisual.transform.localPosition = new Vector3(0f, -_height * 0.5f, 0f);

        _hitbox.transform.localScale = Vector3.one;
        _hitbox.transform.localPosition = Vector3.zero;
        _hitbox.size = new Vector2(_width, _height);
        _hitbox.offset = new Vector2(0f, _height * 0.5f);

        _warningVisual.gameObject.SetActive(false);
        _thornVisual.gameObject.SetActive(false);
        _hitbox.gameObject.SetActive(false);
        _thornMask.gameObject.SetActive(true);

        _initialized = true;

        return true;
    }

    public void Play(double startTime)
    {
        if (!_initialized || !isActiveAndEnabled || IsPlaying)
            return;

        var manager = Unity.Netcode.NetworkManager.Singleton;

        if (manager == null || !manager.IsListening || !manager.IsServer)
            return;

        IsPlaying = true;
        StartCoroutine(PlayRoutine(startTime));
    }

    private IEnumerator PlayRoutine(double startTime)
    {
        var manager = Unity.Netcode.NetworkManager.Singleton;

        float warningDuration = Mathf.Max(0f, _warningSeconds);
        float riseDuration = Mathf.Max(0.01f, _riseSeconds);
        float holdDuration = Mathf.Max(0f, _holdSeconds);

        float totalDuration = warningDuration + riseDuration + holdDuration;

        bool attackStarted = false;

        _warningVisual.gameObject.SetActive(false);
        _thornVisual.gameObject.SetActive(false);
        _hitbox.gameObject.SetActive(false);

        while (manager != null && manager.IsListening)
        {
            double elapsed = manager.ServerTime.Time - startTime;

            if (elapsed >= totalDuration)
                break;

            if (elapsed < warningDuration)
            {
                _warningVisual.gameObject.SetActive(true);
                _thornVisual.gameObject.SetActive(false);
                _hitbox.gameObject.SetActive(false);
            }
            else
            {
                _warningVisual.gameObject.SetActive(false);
                _thornVisual.gameObject.SetActive(true);

                float progress = Mathf.Clamp01(
                    (float)(elapsed - warningDuration) / riseDuration);

                SetRiseProgress(progress);

                if (!attackStarted)
                {
                    attackStarted = true;

                    if (manager.IsServer)
                        AttackStarted?.Invoke();
                }
            }

            yield return null;
        }

        _warningVisual.gameObject.SetActive(false);
        _thornVisual.gameObject.SetActive(false);
        _hitbox.gameObject.SetActive(false);

        IsPlaying = false;
    }

    private void SetRiseProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);

        float visibleHeight = _height * progress;

        _thornVisual.transform.localPosition = new Vector3(0f, -_height * 0.5f + visibleHeight, 0f);

        if (visibleHeight <= 0f)
        {
            _hitbox.gameObject.SetActive(false);
            return;
        }

        _hitbox.size = new Vector2(_width, visibleHeight);

        _hitbox.offset = new Vector2(0f, visibleHeight * 0.5f);

        var manager = Unity.Netcode.NetworkManager.Singleton;
        bool isServer = manager != null && manager.IsServer;

        _hitbox.gameObject.SetActive(isServer);
    }

    public void PlayClientVisual(double startTime)
    {
        if (!_initialized || !isActiveAndEnabled || IsPlaying)
            return;

        var manager = Unity.Netcode.NetworkManager.Singleton;

        if (manager == null || !manager.IsListening || manager.IsServer)
            return;

        IsPlaying = true;
        StartCoroutine(PlayRoutine(startTime));
    }

    
}
