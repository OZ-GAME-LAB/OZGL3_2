using System;
using Unity.Netcode;
using UnityEngine;

public partial class PlayerNetworkStats
{
    [Header("부활 진단")]
    [SerializeField] private bool _logRespawnFlow = true;
    private GameOverPresenter _gameOverPresenter;
    private PlayerControllerView _deathView;
    private uint _localRespawnRequest;
    private uint _serverRespawnRequest;
    private Vector3 _respawnFootPosition;
    private bool _respawnFaceRight;
    private double _respawnDeadline;
    private const double RespawnTimeoutSeconds = 10d;
    private bool _localRespawnPending;
    private double _localRespawnDeadline;
    public bool IsRespawning => _localRespawnPending;

    public event Action<string> RespawnFailed;
    public event Action RespawnSucceeded;

    private void InitializeDeathFlow()
    {
        _serverRespawnRequest = 0;
        _localRespawnRequest = 0;
        _localRespawnPending = false;
        TryGetComponent(out _deathView);
        if (IsOwner)
            _gameOverPresenter = new GameOverPresenter(this, _deathView);
    }

    private void ReleaseDeathFlow()
    {
        _serverRespawnRequest = 0;
        _localRespawnPending = false;
        _gameOverPresenter?.Dispose();
        _gameOverPresenter = null;
    }

    protected override void OnDiedOnServer()
    {
        _serverRespawnRequest = 0;
        if (TryGetComponent(out PlayerInteractionController interaction))
            interaction.CancelInteractionOnServer();
        if (TryGetComponent(out PlayerRuneAttackController attack))
            attack.ResetForRespawnOnServer();
    }

    protected override void Update()
    {
        base.Update();
        if (IsOwner && IsSpawned && _localRespawnPending)
        {
            if (IsAlive) FinishLocalRespawn(true, null);
            else if (Time.realtimeSinceStartupAsDouble >= _localRespawnDeadline)
            {
                TraceRespawn(_localRespawnRequest, "LocalTimeout");
                FinishLocalRespawn(false, "부활 응답을 확인하지 못했습니다. 다시 시도해 주세요.");
            }
        }
        if (IsServer && IsSpawned && _serverRespawnRequest != 0 &&
            Time.realtimeSinceStartupAsDouble >= _respawnDeadline)
        {
            FinishServerRespawn(_serverRespawnRequest, IsAlive, "부활 응답이 지연되었습니다. 다시 시도해 주세요.");
        }
    }

    // 반환값은 요청 시작 여부입니다. 호스트에서는 이 호출 안에서 이미 완료될 수도 있습니다.
    public bool RequestRespawn()
    {
        if (!IsOwner || !IsSpawned || IsAlive || _localRespawnPending) return false;
        ++_localRespawnRequest;
        if (_localRespawnRequest == 0) ++_localRespawnRequest;
        _localRespawnPending = true;
        _localRespawnDeadline = Time.realtimeSinceStartupAsDouble + RespawnTimeoutSeconds + 5d;
        TraceRespawn(_localRespawnRequest, "LocalRequested");
        try { RequestRespawnServerRpc(_localRespawnRequest); }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            FinishLocalRespawn(IsAlive, "부활 요청을 보내지 못했습니다. 다시 시도해 주세요.");
        }
        return true;
    }

    [ServerRpc]
    private void RequestRespawnServerRpc(uint request, ServerRpcParams rpcParams = default)
    {
        if (!IsServer || !IsSpawned || rpcParams.Receive.SenderClientId != OwnerClientId || request == 0)
            return;
        if (IsAlive || _serverRespawnRequest != 0)
        {
            ReceiveRespawnResultClientRpc(request, IsAlive, "이미 부활 중이거나 살아 있는 상태입니다.", OwnerRpcParams());
            return;
        }
        if (WorldManager.Inst == null || !WorldManager.Inst.TryGetStartSpawnPoint(out StageEntryPoint point))
        {
            ReceiveRespawnResultClientRpc(request, false, "시작 스폰 지점이 없습니다. Start Spawn Point 설정을 확인해 주세요.", OwnerRpcParams());
            return;
        }
        if (_deathView == null || !_deathView.CanPlaceForRespawn)
        {
            ReceiveRespawnResultClientRpc(request, false, "플레이어의 부활 위치 설정을 확인해 주세요.", OwnerRpcParams());
            return;
        }

        _serverRespawnRequest = request;
        _respawnFootPosition = point.transform.position;
        _respawnFaceRight = point.FacingDirection == EntryFacingDirection.Right;
        _respawnDeadline = Time.realtimeSinceStartupAsDouble + RespawnTimeoutSeconds;
        TraceRespawn(request, "ServerAccepted");
        try
        {
            // 기존 배치 권한 정책을 유지합니다. Teleport는 권한이 있는 쪽에서만 전송합니다.
            if (!_deathView.PlaceForRespawn(_respawnFootPosition, _respawnFaceRight))
            {
                FinishServerRespawn(request, false, "시작 지점으로 이동하지 못했습니다.");
                return;
            }
            PlaceForRespawnClientRpc(request, _respawnFootPosition, _respawnFaceRight, OwnerRpcParams());
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            FinishServerRespawn(request, IsAlive, "부활 위치 처리 중 오류가 발생했습니다. 다시 시도해 주세요.");
        }
    }

    [ClientRpc]
    private void PlaceForRespawnClientRpc(uint request, Vector3 footPosition, bool faceRight, ClientRpcParams rpcParams = default)
    {
        if (!IsOwner || !IsSpawned || !_localRespawnPending || request != _localRespawnRequest || IsAlive) return;
        if (Time.realtimeSinceStartupAsDouble >= _localRespawnDeadline)
        {
            FinishLocalRespawn(false, "부활 응답이 지연되었습니다. 다시 시도해 주세요.");
            return;
        }
        bool placed = false;
        try
        {
            placed = _deathView != null && _deathView.PlaceForRespawn(footPosition, faceRight);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
        TraceRespawn(request, placed ? "OwnerPlaced" : "OwnerPlacementFailed");
        CompleteRespawnServerRpc(request, placed);
    }

    [ServerRpc]
    private void CompleteRespawnServerRpc(uint request, bool placed, ServerRpcParams rpcParams = default)
    {
        if (!IsServer || !IsSpawned || rpcParams.Receive.SenderClientId != OwnerClientId ||
            request == 0 || request != _serverRespawnRequest) return;
        bool success = IsAlive;
        string message = "시작 지점으로 이동하지 못했습니다. 다시 시도해 주세요.";
        try
        {
            if (!success && placed && Time.realtimeSinceStartupAsDouble < _respawnDeadline &&
                _deathView != null && _deathView.PlaceForRespawn(_respawnFootPosition, _respawnFaceRight))
            {
                SetEncounterLockedOnServer(false);
                if (TryGetComponent(out PlayerRuneAttackController attack)) attack.ResetForRespawnOnServer();
                if (TryGetComponent(out PlayerRuneCombatActor actor)) actor.ResetForRespawnOnServer();
                success = ReviveOnServer();
                message = "HP를 복구하지 못했습니다. 다시 시도해 주세요.";
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            // HP 설정 뒤 구독자에서 예외가 났을 때도 실제 생존 상태로 결과를 결정합니다.
            success = IsAlive;
            message = "부활 완료 처리 중 오류가 발생했습니다. 다시 시도해 주세요.";
        }
        FinishServerRespawn(request, success, message);
    }

    private void FinishServerRespawn(uint request, bool success, string message)
    {
        if (request != _serverRespawnRequest) return;
        _serverRespawnRequest = 0;
        TraceRespawn(request, success ? "ServerComplete" : "ServerFailed");
        ReceiveRespawnResultClientRpc(request, success, success ? "" : message, OwnerRpcParams());
    }

    [ClientRpc]
    private void ReceiveRespawnResultClientRpc(uint request, bool success, string message, ClientRpcParams rpcParams = default)
    {
        if (!IsOwner || !IsSpawned || !_localRespawnPending || request != _localRespawnRequest) return;
        TraceRespawn(request, success ? "ClientSuccessResult" : "ClientFailureResult");
        if (IsAlive) FinishLocalRespawn(true, null);
        else if (!success) FinishLocalRespawn(false, message);
        // 성공 RPC가 HP보다 먼저 오면 실제 HP를 기다립니다. 로컬 기한은 계속 유효합니다.
    }

    private void FinishLocalRespawn(bool success, string message)
    {
        if (!_localRespawnPending) return;
        _localRespawnPending = false;
        TraceRespawn(_localRespawnRequest, success ? "LocalComplete" : "LocalFailed");
        if (success) RespawnSucceeded?.Invoke();
        else RespawnFailed?.Invoke(message);
    }

    private void TraceRespawn(uint request, string step)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_logRespawnFlow)
            Debug.Log($"[Respawn] owner={OwnerClientId} request={request} step={step} server={IsServer} hp={CurrentHp}", this);
#endif
    }

    private ClientRpcParams OwnerRpcParams()
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        };
    }
}
