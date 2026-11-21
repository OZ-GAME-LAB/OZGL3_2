using Unity.Netcode;

public partial class PlayerNetworkStats
{
    private readonly NetworkVariable<bool> _isResting = new(false);
    private PlayerControllerView _restView;
    public bool IsResting => _isResting.Value;

    private void InitializeRestState()
    {
        TryGetComponent(out _restView);
        if (IsServer) _isResting.Value = false;
        _isResting.OnValueChanged += OnRestingChanged;
        OnRestingChanged(false, _isResting.Value);
    }

    private void ReleaseRestState()
    {
        _isResting.OnValueChanged -= OnRestingChanged;
        if (IsOwner) RestTent.Active?.CancelLocalForPlayer(_restView);
        else _restView?.SetRestVisualState(false);
    }

    private void OnRestingChanged(bool previous, bool current)
    {
        // 소유자는 유효한 로컬 UI 요청으로 표시합니다. 취소 뒤 늦은 true로 다시 잠그지 않습니다.
        if (!IsOwner) _restView?.SetRestVisualState(current);
    }

    public void SetRestingOnServer(bool resting)
    {
        if (IsServer && IsSpawned) _isResting.Value = resting;
    }
}
