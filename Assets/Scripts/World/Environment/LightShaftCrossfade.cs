using System.Collections;
using UnityEngine;

/// <summary>맵에 고정된 두 빛 이미지를 번갈아 표시합니다.</summary>
[DisallowMultipleComponent]
public sealed class LightShaftCrossfade : MonoBehaviour
{
    [Header("겹쳐 놓을 빛 이미지")]
    [SerializeField] private SpriteRenderer _spriteA;
    [SerializeField] private SpriteRenderer _spriteB;

    [Header("빛의 강도와 전환")]
    [SerializeField, Range(0f, 1f)] private float _maxAlpha = 0.12f;
    [Tooltip("두 원본 이미지의 불투명도가 다를 때 B의 밝기를 맞춥니다.")]
    [SerializeField, Range(0f, 2f)] private float _spriteBIntensity = 1f;
    [SerializeField, Min(0.1f)] private float _transitionDuration = 5f;
    [SerializeField] private bool _randomizeStartPhase;

    private Coroutine _fadeRoutine;

    private void OnEnable()
    {
        if (_spriteA == null || _spriteB == null || _spriteA == _spriteB ||
            _spriteA.sprite == null || _spriteB.sprite == null)
        {
            Debug.LogWarning("서로 다른 두 SpriteRenderer와 빛 이미지를 연결해 주세요.", this);
            return;
        }

        _fadeRoutine = StartCoroutine(Crossfade());
    }

    private void OnDisable()
    {
        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }

        SetAlpha(_spriteA, 0f);
        SetAlpha(_spriteB, 0f);
    }

    private IEnumerator Crossfade()
    {
        float phase = _randomizeStartPhase ? Random.value * 2f : 0f;
        while (_spriteA != null && _spriteB != null)
        {
            float blend = (1f - Mathf.Cos(phase * Mathf.PI)) * 0.5f;
            float alpha = Mathf.Clamp01(_maxAlpha);
            SetAlpha(_spriteA, alpha * (1f - blend));
            SetAlpha(_spriteB, Mathf.Clamp01(alpha * blend * _spriteBIntensity));

            yield return null;
            phase = Mathf.Repeat(phase + Time.deltaTime / Mathf.Max(0.1f, _transitionDuration), 2f);
        }
    }

    private static void SetAlpha(SpriteRenderer sprite, float alpha)
    {
        if (sprite == null)
            return;

        Color color = sprite.color;
        color.a = alpha;
        sprite.color = color;
    }
}
