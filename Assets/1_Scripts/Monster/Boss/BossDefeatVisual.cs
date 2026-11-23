using UnityEngine;

// 서버의 사망/시간을 표시하기만 하며, 처치 판단과 보스 제거는 담당하지 않습니다.
public sealed class BossDefeatVisual : MonoBehaviour
{
    [Header("사망 연출 연결")]
    [SerializeField] private BossNetwork _network;
    [SerializeField] private LoggingTreeEffect _effect;
    [SerializeField] private SpriteRenderer _sprite;
    [SerializeField] private Animator _animator;
    [SerializeField] private BossIntroVisual _intro;
    [SerializeField] private Material _sinkMaterial;
    private Material _originalMaterial;
    private bool _animatorWasEnabled;
    private bool _introWasEnabled;
    private bool _presenting;
    private bool _finished;

    private void LateUpdate()
    {
        if (_finished || _network == null || !_network.IsSpawned || !_network.DeathConfirmed) return;
        if (!_presenting)
        {
            if (_effect == null || _sprite == null || _sprite.sprite == null || _sinkMaterial == null) return;
            _originalMaterial = _sprite.sharedMaterial;
            _animatorWasEnabled = _animator != null && _animator.enabled;
            _introWasEnabled = _intro != null && _intro.enabled;
            if (_animator != null) _animator.enabled = false;
            if (_intro != null) _intro.enabled = false;
            _sprite.sharedMaterial = _sinkMaterial;
            _effect.Initialize();
            _presenting = true;
        }
        if (_network.DefeatStartTime < 0d || _network.NetworkManager == null) return;
        _finished = !_effect.Present(true, _network.DefeatStartTime, _network.DefeatDuration,
            _network.NetworkManager.ServerTime.Time);
        // 완료 직후 재질/효과를 복원하면 보스가 다시 보이므로 제거 시점까지 숨김을 유지합니다.
    }

    private void OnDisable()
    {
        if (_presenting)
        {
            if (_effect != null) _effect.ResetPresentation();
            if (_sprite != null && _sprite.sharedMaterial == _sinkMaterial) _sprite.sharedMaterial = _originalMaterial;
            if (_animator != null) _animator.enabled = _animatorWasEnabled;
            if (_intro != null) _intro.enabled = _introWasEnabled;
        }
        _originalMaterial = null;
        _presenting = false;
        _finished = false;
    }
}
