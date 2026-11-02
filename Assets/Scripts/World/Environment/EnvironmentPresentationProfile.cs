using System;
using UnityEngine;

/// <summary>공간에서 사용할 효과 조합입니다. 실행 상태는 컨트롤러가 보관합니다.</summary>
[CreateAssetMenu(menuName = "Environment/Presentation Profile")]
public sealed class EnvironmentPresentationProfile : ScriptableObject
{
    [Serializable]
    public sealed class EffectEntry
    {
        [SerializeField] private EnvironmentScreenEffect _prefab;
        [SerializeField, Range(0f, 1f)] private float _intensity = 1f;
        [SerializeField, Min(0f)] private float _fadeInDuration = 2f;
        [SerializeField, Min(0f)] private float _fadeOutDuration = 2f;

        public EnvironmentScreenEffect Prefab => _prefab;
        public float Intensity => Mathf.Clamp01(_intensity);
        public float FadeInDuration => Mathf.Max(0f, _fadeInDuration);
        public float FadeOutDuration => Mathf.Max(0f, _fadeOutDuration);
    }

    [Header("효과 조합 — 같은 프리팹은 한 번만 등록")]
    [SerializeField] private EffectEntry[] _effects = Array.Empty<EffectEntry>();

    public int EffectCount => _effects == null ? 0 : _effects.Length;
    public EffectEntry GetEffect(int index) => _effects[index];
}
