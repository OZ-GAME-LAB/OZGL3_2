using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class ScreenTransitionUI : UIBase
{
    [Header("암전")]
    [SerializeField] private CanvasGroup _cover;
    [SerializeField, Min(0.01f)] private float _fadeSeconds = 0.6f;
    private int _animationVersion;
    private void OnEnable() { ++_animationVersion; _cover.alpha = 0f; _cover.blocksRaycasts = true; }
    private void OnDisable() { ++_animationVersion; }

    public async UniTask FadeAsync(bool black, CancellationToken token)
    {
        int version = ++_animationVersion;
        float start = _cover.alpha;
        float elapsed = 0f;
        while (elapsed < _fadeSeconds)
        {
            CheckAnimation(version, token);
            elapsed += Time.unscaledDeltaTime;
            _cover.alpha = Mathf.Lerp(start, black ? 1f : 0f, Mathf.SmoothStep(0f, 1f, elapsed / _fadeSeconds));
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }
        CheckAnimation(version, token);
        _cover.alpha = black ? 1f : 0f;
        _cover.blocksRaycasts = black;
    }

    private void CheckAnimation(int version, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (version != _animationVersion || !isActiveAndEnabled) throw new System.OperationCanceledException();
    }
}
