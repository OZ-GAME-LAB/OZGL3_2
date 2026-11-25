using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public sealed class LoadingUI : UIBase
{
    [Header("필드 진입")]
    [SerializeField] private RectTransform _fill;
    [SerializeField] private Button _cancelButton;
    [SerializeField, Min(0f)] private float _completedHoldSeconds = 0.5f;
    private Action _cancel;

    private void Awake() => _cancelButton.onClick.AddListener(Cancel);

    private void OnEnable()
    {
        transform.SetAsLastSibling();
        SetProgress(0f);
        SetCancelAction(null);
    }

    private void OnDisable() => SetCancelAction(null);
    private void Cancel() => _cancel?.Invoke();

    public void SetCancelAction(Action cancel)
    {
        _cancel = cancel;
        _cancelButton.gameObject.SetActive(cancel != null);
    }

    public void SetProgress(float progress)
    {
        _fill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
    }

    public async UniTask ShowCompleteAsync(CancellationToken token)
    {
        SetProgress(1f);
        await UniTask.NextFrame(cancellationToken: token);
        await UniTask.Delay(TimeSpan.FromSeconds(_completedHoldSeconds), ignoreTimeScale: true, cancellationToken: token);
    }
}
