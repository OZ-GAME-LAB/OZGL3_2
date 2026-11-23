using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

// 입력 View가 꺼진 원격 플레이어에서도 수신한 표현만 갱신합니다.
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerNetworkStats), typeof(PlayerControllerView))]
public sealed class PlayerMovementNetwork : NetworkBehaviour
{
    [Header("이동 애니메이션 통신")]
    [SerializeField, Min(0.01f)] private float _velocitySendInterval = 0.05f;
    private readonly NetworkVariable<PlayerMovementAnimationState> _movement = new(default,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    // 생존 판정은 Stats가 소유합니다. 이 번호는 이전 생명 주기의 패킷 식별에만 사용합니다.
    private readonly NetworkVariable<uint> _lifeVersion = new(0);
    private PlayerNetworkStats _stats;
    private PlayerControllerView _view;
    private PlayerRuneAttackController _attack;
    private PlayerInteractionController _interaction;
    private bool _wasAlive;
    private bool _hasPublished;
    private float _nextVelocitySend;
    private uint _ownerLifeVersion;

    public bool HandlesRemotePresentation => isActiveAndEnabled && IsSpawned && !IsOwner;

    private void Awake()
    {
        TryGetComponent(out _stats);
        TryGetComponent(out _view);
        TryGetComponent(out _attack);
        TryGetComponent(out _interaction);
        // 좌우 반전은 크기 보간 대신 이동 표현 상태가 즉시 적용합니다.
        if (TryGetComponent(out NetworkTransform networkTransform))
            networkTransform.SyncScaleX = false;
    }

    public override void OnNetworkSpawn()
    {
        _wasAlive = _stats.IsAlive;
        _hasPublished = false;
        _ownerLifeVersion = _lifeVersion.Value;
        _stats.StatsChanged += OnStatsChanged;
        _view.ResetRemoteMovementPresentation();
    }

    public override void OnNetworkDespawn()
    {
        _stats.StatsChanged -= OnStatsChanged;
        _hasPublished = false;
        _view.ResetRemoteMovementPresentation();
    }

    private void OnStatsChanged()
    {
        bool alive = _stats.IsAlive;
        if (IsServer && IsSpawned && alive != _wasAlive)
            _lifeVersion.Value = unchecked(_lifeVersion.Value + 1);
        _wasAlive = alive;
    }

    private void LateUpdate()
    {
        if (!IsSpawned) return;
        if (IsOwner)
        {
            PublishOwnerState();
            return;
        }

        PlayerMovementAnimationState state = _movement.Value;
        bool valid = _stats.IsAlive && state.HasSample && state.LifeVersion == _lifeVersion.Value;
        _view.ApplyRemoteMovementPresentation(state, valid,
            _attack != null ? _attack.AttackState : default,
            _interaction != null ? _interaction.ResourceAnimation : default,
            NetworkManager.ServerTime.Time);
    }

    private void PublishOwnerState()
    {
        if (_ownerLifeVersion != _lifeVersion.Value)
        {
            _ownerLifeVersion = _lifeVersion.Value;
            _hasPublished = false;
            // 전송 캐시만 무효화합니다. 부활 후 이미 시작한 새 점프는 보존합니다.
        }
        PlayerMovementAnimationState next = _view.CaptureMovementAnimation(
            _ownerLifeVersion, _stats.IsAlive);
        PlayerMovementAnimationState previous = _movement.Value;
        bool discreteChanged = !next.SameDiscreteState(previous);
        bool crossedFallThreshold = (next.VerticalSpeed < -0.1f) != (previous.VerticalSpeed < -0.1f);
        bool velocityChanged = Mathf.Abs(next.VerticalSpeed - previous.VerticalSpeed) >= 0.05f;
        if (_hasPublished && !discreteChanged && !crossedFallThreshold &&
            (!velocityChanged || Time.unscaledTime < _nextVelocitySend)) return;
        _movement.Value = next;
        _hasPublished = true;
        _nextVelocitySend = Time.unscaledTime + Mathf.Max(0.01f, _velocitySendInterval);
    }
}
