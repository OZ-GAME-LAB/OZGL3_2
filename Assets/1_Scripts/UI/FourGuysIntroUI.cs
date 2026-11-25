using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class FourGuysIntroUI : UIBase
{
    private static readonly int IntroState = Animator.StringToHash("Base Layer.FourGuysIntro");
    private const float MaxPlaybackStep = 0.05f;

    [Header("로고 애니메이션")]
    [SerializeField] private RectTransform _logoLayout;
    [SerializeField] private Animator _animator;

    private void OnRectTransformDimensionsChange()
    {
        FitLogo();
    }

    private void FitLogo()
    {
        if (_logoLayout == null) return;
        Rect screen = ((RectTransform)transform).rect;
        Rect design = _logoLayout.rect;
        if (design.width <= 0f || design.height <= 0f) return;
        float scale = Mathf.Min(screen.width / design.width, screen.height / design.height);
        _logoLayout.localScale = new Vector3(scale, scale, 1f);
    }

    public async UniTask PlayAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_animator == null || !_animator.isActiveAndEnabled || !_animator.HasState(0, IntroState))
        {
            Debug.LogWarning("개발사 인트로의 Animator 상태를 확인해 주세요.", this);
            return;
        }

        FitLogo();
        _animator.Play(IntroState, 0, 0f);
        _animator.Update(0f);
        // 첫 화면을 준비하는 동안 밀린 시간이 낙하 동작 전체를 건너뛰지 않도록 한다.
        float previousSpeed = _animator.speed;
        _animator.speed = 0f;
        try
        {
            do
            {
                await UniTask.NextFrame(cancellationToken: cancellationToken);
                if (this == null || !isActiveAndEnabled || !_animator.isActiveAndEnabled)
                    return;

                float delta = Time.unscaledDeltaTime;
                _animator.speed = delta > MaxPlaybackStep ? MaxPlaybackStep / delta : 1f;
            }
            while (_animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f);
        }
        finally
        {
            if (_animator != null)
                _animator.speed = previousSpeed;
        }
    }
}
