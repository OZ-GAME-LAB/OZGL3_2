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

    [SerializeField]
    private bool _useAttackHeightForWarning;

    [SerializeField, Min(0f)]
    private float _warningSeconds = 1f;

    [SerializeField, Min(0.01f)]
    private float _riseSeconds = 0.2f;

    [System.Serializable]
    private sealed class NativeSpriteSet
    {
        [SerializeField] private Vector2 _attackSize;
        [SerializeField] private Sprite _warning;
        [SerializeField] private Sprite _thorn;
        [SerializeField] private Sprite _mask;

        public Vector2 AttackSize => _attackSize;
        public Sprite Warning => _warning;
        public Sprite Thorn => _thorn;
        public Sprite Mask => _mask;
    }

    [Header("Native sprites by attack size")]
    [SerializeField] private NativeSpriteSet[] _nativeSprites;

    // Allows pixel rounding at 100 PPU, without changing the attack bounds.
    private const float SpriteSizeTolerance = 0.02f;
    private bool _reportedNativeSpriteError;

    public event System.Action AttackStarted;

    private bool _initialized;
    private float _holdSeconds;

    public bool IsPlaying { get; private set; }

    private float _width;
    private float _height;
    
    private float _playWarningSeconds;
    public float WarningSeconds => _playWarningSeconds;

    public float RiseSeconds => Mathf.Max(0.01f, _riseSeconds);


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

    public bool Initialize(float width, float height, float holdSeconds, float warningSeconds)
    {
        if (IsPlaying)
            return false;

        _initialized = false;
        HideVisualsAndHitbox();

        if (float.IsNaN(warningSeconds) || float.IsInfinity(warningSeconds) || warningSeconds < 0f)
        {
            Debug.LogError("가시 예고 시간이 올바르지 않습니다.", this);
            return false;
        }

        if (float.IsNaN(holdSeconds) || float.IsInfinity(holdSeconds) || holdSeconds < 0f)
        {
            Debug.LogError("가시 유지 시간이 올바르지 않습니다.", this);
            return false;
        }

        if (_warningVisual == null || _thornMask == null ||
            _thornVisual == null ||
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

        float warningHeight = _useAttackHeightForWarning ? height : _warningHeight;
        if (!TryGetNativeSprites(width, height, warningHeight, out NativeSpriteSet sprites))
            return false;

        _width = width;
        _height = height;
        _holdSeconds = holdSeconds;
        _playWarningSeconds = warningSeconds;

        _warningVisual.sprite = sprites.Warning;
        _thornVisual.sprite = sprites.Thorn;
        _thornMask.sprite = sprites.Mask;
        _warningVisual.drawMode = SpriteDrawMode.Simple;
        _thornVisual.drawMode = SpriteDrawMode.Simple;
        _warningVisual.transform.localScale = Vector3.one;
        _thornVisual.transform.localScale = Vector3.one;
        _thornMask.transform.localScale = Vector3.one;
        _warningVisual.transform.localPosition = new Vector3(0f, warningHeight * 0.5f, 0f);
        _thornMask.transform.localPosition = new Vector3(0f, _height * 0.5f, 0f);

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

    private void HideVisualsAndHitbox()
    {
        if (_warningVisual != null)
            _warningVisual.gameObject.SetActive(false);
        if (_thornVisual != null)
            _thornVisual.gameObject.SetActive(false);
        if (_thornMask != null)
            _thornMask.gameObject.SetActive(false);
        if (_hitbox != null)
            _hitbox.gameObject.SetActive(false);
    }

    private bool TryGetNativeSprites(float width, float height, float warningHeight,
        out NativeSpriteSet selected)
    {
        selected = null;
        if (!HasUnitParentScale(_warningVisual.transform) ||
            !HasUnitParentScale(_thornVisual.transform) ||
            !HasUnitParentScale(_thornMask.transform) ||
            !HasUnitParentScale(_hitbox.transform))
            return ReportNativeSpriteError("Attack parent scale must remain (1, 1, 1).");

        if (_nativeSprites != null)
        {
            foreach (NativeSpriteSet candidate in _nativeSprites)
            {
                if (candidate == null ||
                    !MatchesSize(candidate.AttackSize, width, height))
                    continue;

                if (!MatchesSprite(candidate.Thorn, width, height) ||
                    !MatchesSprite(candidate.Mask, width, height) ||
                    !MatchesSprite(candidate.Warning, width, warningHeight))
                    return ReportNativeSpriteError("Native sprite size or center pivot is invalid.");

                selected = candidate;
                return true;
            }
        }

        return ReportNativeSpriteError($"No native sprite set for attack {width:F4} x {height:F4}.");
    }

    private static bool HasUnitParentScale(Transform visual)
    {
        Vector3 scale = visual.parent != null ? visual.parent.lossyScale : Vector3.one;
        return (scale - Vector3.one).sqrMagnitude < 0.000001f;
    }

    private static bool MatchesSize(Vector2 actual, float width, float height)
    {
        return Mathf.Abs(actual.x - width) <= SpriteSizeTolerance &&
            Mathf.Abs(actual.y - height) <= SpriteSizeTolerance;
    }

    private static bool MatchesSprite(Sprite sprite, float width, float height)
    {
        if (sprite == null || !MatchesSize(sprite.bounds.size, width, height))
            return false;

        Vector2 center = sprite.rect.size * 0.5f;
        return (sprite.pivot - center).sqrMagnitude < 0.0001f &&
            sprite.bounds.center.sqrMagnitude < 0.000001f;
    }

    private bool ReportNativeSpriteError(string message)
    {
        if (!_reportedNativeSpriteError)
        {
            Debug.LogError($"[BossThornAttackObject] {message}", this);
            _reportedNativeSpriteError = true;
        }
        return false;
    }

    public bool Initialize(float width, float height, float holdSeconds)
    {
        return Initialize(width, height, holdSeconds, _warningSeconds);
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

        float warningDuration = Mathf.Max(0f, _playWarningSeconds);
        float riseDuration = RiseSeconds;
        float holdDuration = Mathf.Max(0f, _holdSeconds);

        float totalDuration = warningDuration + riseDuration + holdDuration;

        bool attackStarted = false;

        Debug.Log($"가시 시작: 경과={manager.ServerTime.Time - startTime:F3}초, 예고={warningDuration}초, 유지={holdDuration}초", this);

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
