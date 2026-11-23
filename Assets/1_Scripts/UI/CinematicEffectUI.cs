using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class CinematicEffectUI : UIBase
{
    [Header("시네마틱 상하 검은 띠")]
    [SerializeField] private RectTransform _top;
    [SerializeField] private RectTransform _bottom;
    [SerializeField] private CanvasGroup _group;
    [SerializeField, Min(0.01f)] private float _transitionSeconds = 0.65f;
    private float _visibility;
    private int _animationVersion;
    private void OnEnable() { ++_animationVersion; SetVisibility(0f); }
    private void OnDisable() { ++_animationVersion; }

    public UniTask ShowAsync(CancellationToken token) => AnimateAsync(1f, token);
    public UniTask HideAsync(CancellationToken token) => AnimateAsync(0f, token);

    private async UniTask AnimateAsync(float target, CancellationToken token)
    {
        int version = ++_animationVersion;
        float start = _visibility;
        float elapsed = 0f;
        while (elapsed < _transitionSeconds)
        {
            CheckAnimation(version, token);
            elapsed += Time.unscaledDeltaTime;
            SetVisibility(Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, elapsed / _transitionSeconds)));
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }
        CheckAnimation(version, token);
        SetVisibility(target);
    }

    private void CheckAnimation(int version, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (version != _animationVersion || !isActiveAndEnabled) throw new System.OperationCanceledException();
    }

    internal void SetVisibility(float value)
    {
        _visibility = value;
        _group.alpha = value;
        _top.anchoredPosition = new Vector2(0f, _top.rect.height * (1f - value));
        _bottom.anchoredPosition = new Vector2(0f, -_bottom.rect.height * (1f - value));
    }
}
