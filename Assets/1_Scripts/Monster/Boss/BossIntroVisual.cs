using UnityEngine;

// 충돌체와 공격 기준점은 그대로 두고 그림만 움직입니다.
public sealed class BossIntroVisual : MonoBehaviour
{
    [SerializeField] private BossNetwork _network;
    [SerializeField] private Animator _animator;
    [SerializeField] private SpriteRenderer _sprite;
    [SerializeField] private Sprite _closedEyes;
    [SerializeField] private Sprite _openedEyes;
    [SerializeField, Min(0.1f)] private float _eyeOpenSeconds = 6.859375f;
    [SerializeField, Min(0f)] private float _roarSeconds = 1.2f;
    [SerializeField, Min(0f)] private float _shakeDistance = 0.16f;
    [SerializeField, Min(1f)] private float _shakeFrequency = 27f;
    private Vector3 _restPosition;
    private bool _finished;
    public float Duration => _eyeOpenSeconds + _roarSeconds;

    private void Awake()
    {
        _restPosition = _sprite.transform.localPosition;
        _animator.enabled = false;
        _sprite.sprite = _closedEyes;
    }

    private void LateUpdate()
    {
        if (_finished || _network == null || !_network.IsSpawned) return;
        if (_network.CombatActive) { Finish(); return; }
        if (_network.IntroStartTime < 0d) return;
        float elapsed = (float)(_network.NetworkManager.ServerTime.Time - _network.IntroStartTime);
        if (elapsed < 0f) return;
        if (elapsed < _eyeOpenSeconds)
        {
            _animator.enabled = true;
            _animator.speed = 0f;
            _animator.Play("Awaken", 0, Mathf.Clamp01(elapsed / _eyeOpenSeconds));
            _animator.Update(0f);
            return;
        }
        _animator.enabled = false;
        _sprite.sprite = _openedEyes;
        if (elapsed >= Duration) { Finish(); return; }
        float shakeTime = elapsed - _eyeOpenSeconds;
        float strength = _shakeDistance * (1f - shakeTime / Mathf.Max(0.01f, _roarSeconds));
        _sprite.transform.localPosition = _restPosition + new Vector3(
            Mathf.Sin(shakeTime * _shakeFrequency * 6.28f),
            Mathf.Sin(shakeTime * _shakeFrequency * 4.13f), 0f) * strength;
    }

    private void Finish()
    {
        _finished = true;
        _animator.enabled = false;
        _sprite.sprite = _openedEyes;
        _sprite.transform.localPosition = _restPosition;
    }
}
