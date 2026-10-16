using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerInteractionController : NetworkBehaviour
{
    [SerializeField] private Transform _distanceOrigin;

    // Inspector에서 위 → 아래 → 오른쪽 → 왼쪽 순서로 연결
    [SerializeField] private Transform[] _uiAnchors = new Transform[4];

    private Camera _playerCamera;
    private bool _isLocalPlayer;

    private readonly Dictionary<Collider, InteractableObject>_contacts = new();

    private readonly HashSet<InteractableObject> _candidates = new();
    private readonly List<Collider> _removeBuffer = new();

    private InteractableObject _selectedTarget;
    private InteractableObject _lockedTarget;
    private InteractableObject _infoTarget;

    private ulong _nextRequestId;
    private ulong _activeRequestId;
    private bool _hasLocalRequest;
    private bool _localInteractionStarted;
    private InteractableObject _requestedTarget;

    private InteractionNetwork _serverTarget;
    private ulong _serverRequestId;

    public event Action<bool> InteractionStartResolved;

    public InteractableObject SelectedTarget => _selectedTarget;
    public InteractableObject LockedTarget => _lockedTarget;

    public event Action<InteractableObject> SelectionChanged;

    // 입력 담당자가 이 이벤트를 받아 진행 중인 상호작용을 취소한다.
    public event Action<InteractableObject> LockedTargetLost;

    // 본인 플레이어 여부와 실제 사용할 카메라를 전달한다.
    // 플레이어 네트워크 초기화 후 호출 함수
    public void Initialize(bool isLocalPlayer, Camera playerCamera)
    {
        _isLocalPlayer = false;
        ClearDetection();

        _playerCamera = playerCamera;

        if (!isLocalPlayer)
            return;

        if (!HasValidUIReferences())
        {
            Debug.LogError(
                "플레이어 카메라와 상·하·우·좌 Anchor를 연결해 주세요.", this);
            return;
        }

        _isLocalPlayer = true;
    }

    private bool HasValidUIReferences()
    {
        if (_playerCamera == null || _uiAnchors == null || _uiAnchors.Length != 4)
        {
            return false;
        }

        foreach (Transform anchor in _uiAnchors)
        {
            if (anchor == null)
                return false;
        }

        return true;
    }

    private void OnTriggerEnter(Collider other)
    {
        RegisterContact(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // 초기화 전에 이미 범위 안에 있던 대상도 등록한다.
        RegisterContact(other);
    }

    private void OnTriggerExit(Collider other)
    {
        _contacts.Remove(other);
    }

    private void RegisterContact(Collider other)
    {
        if (!_isLocalPlayer || _contacts.ContainsKey(other))
            return;

        InteractableObject target = other.GetComponentInParent<InteractableObject>();

        if (target != null)
            _contacts.Add(other, target);
    }

    private void Update()
    {
        if (!_isLocalPlayer)
            return;

        RefreshSelection();
    }

    private void RefreshSelection()
    {
        RefreshCandidates();

        if (!ReferenceEquals(_lockedTarget, null))
        {
            if (_lockedTarget != null && _candidates.Contains(_lockedTarget))
            {
                SetSelectedTarget(_lockedTarget);
                return;
            }

            InteractableObject lostTarget = _lockedTarget;
            _lockedTarget = null;

            // 기존 대상의 취소가 새 안내 표시보다 먼저 이뤄지도록 한다.
            LockedTargetLost?.Invoke(lostTarget);
        }

        SelectNearestTarget();
    }

    private void RefreshCandidates()
    {
        _candidates.Clear();
        _removeBuffer.Clear();

        foreach (var pair in _contacts)
        {
            Collider contact = pair.Key;
            InteractableObject target = pair.Value;

            if (contact == null ||
                !contact.enabled ||
                !contact.gameObject.activeInHierarchy ||
                target == null ||
                !target.isActiveAndEnabled)
            {
                _removeBuffer.Add(contact);
                continue;
            }

            _candidates.Add(target);
        }

        foreach (Collider contact in _removeBuffer)
            _contacts.Remove(contact);
    }

    private void SelectNearestTarget()
    {
        Vector3 origin = _distanceOrigin != null
            ? _distanceOrigin.position
            : transform.position;

        InteractableObject nearest =
            _selectedTarget != null &&
            _candidates.Contains(_selectedTarget)
                ? _selectedTarget
                : null;

        float nearestDistance = nearest != null
            ? (nearest.transform.position - origin).sqrMagnitude
            : float.PositiveInfinity;

        foreach (InteractableObject candidate in _candidates)
        {
            float distance =
                (candidate.transform.position - origin).sqrMagnitude;

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = candidate;
            }
        }

        SetSelectedTarget(nearest);
    }

    private void SetSelectedTarget(InteractableObject target)
    {
        bool changed = !ReferenceEquals(_selectedTarget, target);

        _selectedTarget = target;
        RefreshInfoTarget();

        if (changed)
            SelectionChanged?.Invoke(target);
    }

    private void RefreshInfoTarget()
    {
        InteractableObject nextInfoTarget = ReferenceEquals(_lockedTarget, null) ? _selectedTarget : null;

        if (ReferenceEquals(_infoTarget, nextInfoTarget))
            return;

        if (_infoTarget != null)
            _infoTarget.HideInteractionInfo();

        _infoTarget = nextInfoTarget;

        if (_infoTarget != null)
        {
            _infoTarget.ShowInteractionInfo(_uiAnchors, _playerCamera);
        }
    }

    // F를 누른 순간 호출한다.
    // 선택을 고정할 뿐, 서버 선점 승인은 아니다.
    public bool TryLockSelectedTarget(out InteractableObject target)
    {
        target = null;

        if (!_isLocalPlayer ||
            !isActiveAndEnabled ||
            !ReferenceEquals(_lockedTarget, null))
        {
            return false;
        }

        RefreshSelection();

        if (_selectedTarget == null ||
            !_selectedTarget.ViewModel.IsAvailable)
        {
            return false;
        }

        _lockedTarget = _selectedTarget;
        target = _lockedTarget;

        RefreshInfoTarget();
        return true;
    }

    // 서버가 시작을 승인한 뒤, 입력 담당자가 호출한다.
    public bool BeginLockedInteraction()
    {
        if (!_isLocalPlayer ||
            !isActiveAndEnabled ||
            _lockedTarget == null)
        {
            return false;
        }

        RefreshCandidates();

        if (!_candidates.Contains(_lockedTarget))
            return false;

        _lockedTarget.BeginInteraction(_uiAnchors, _playerCamera);
        return true;
    }

    // 취소·완료·서버 거절 처리가 끝난 후 호출한다.
    public void ReleaseTarget()
    {
        _lockedTarget = null;

        if (_isLocalPlayer && isActiveAndEnabled)
            RefreshSelection();
    }

    private void ClearDetection()
    {
        InteractableObject lostTarget = _lockedTarget;
        bool hadLockedTarget =
            !ReferenceEquals(lostTarget, null);

        _lockedTarget = null;
        _contacts.Clear();
        _candidates.Clear();
        _removeBuffer.Clear();

        SetSelectedTarget(null);

        if (hadLockedTarget)
            LockedTargetLost?.Invoke(lostTarget);
    }

    private void OnDisable()
    {
        ClearDetection();
        CancelRequestedInteraction();
    }

    public bool TryRequestInteraction()
    {
        if (!IsSpawned || !IsOwner || _hasLocalRequest)
            return false;

        if (!TryLockSelectedTarget(out InteractableObject target))
            return false;

        if (!target.TryGetComponent(out InteractionNetwork network) || !network.IsSpawned)
        {
            ReleaseTarget();
            return false;
        }

        _activeRequestId = ++_nextRequestId;
        _requestedTarget = target;
        _hasLocalRequest = true;
        _localInteractionStarted = false;

        RequestStartServerRpc(new NetworkObjectReference(network.NetworkObject), _activeRequestId);

        return true;
    }

    public void TickInteraction(float deltaTime)
    {
        if (!IsOwner ||
            !_hasLocalRequest ||
            !_localInteractionStarted ||
            _requestedTarget == null)
        {
            return;
        }

        _requestedTarget.UpdateInteraction(deltaTime);
    }

    public void CancelRequestedInteraction()
    {
        if (!_hasLocalRequest)
            return;

        ulong requestId = _activeRequestId;
        InteractableObject target = _requestedTarget;
        bool wasStarted = _localInteractionStarted;

        // 늦게 오는 승인 응답이 진행을 시작하지 않도록 먼저 정리
        ClearLocalRequest();

        if (wasStarted && target != null)
            target.CancelInteraction();

        if (IsSpawned && IsOwner)
            RequestCancelServerRpc(requestId);

        ReleaseTarget();
    }

    private void ClearLocalRequest()
    {
        _hasLocalRequest = false;
        _localInteractionStarted = false;
        _requestedTarget = null;
        _activeRequestId = 0;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
            LockedTargetLost += OnLockedTargetLost;
    }

    private void OnLockedTargetLost(InteractableObject target)
    {
        CancelRequestedInteraction();
    }

    public override void OnNetworkDespawn()
    {
        LockedTargetLost -= OnLockedTargetLost;

        if (IsServer && _serverTarget != null)
        {
            _serverTarget.TryCancelOnServer(
                OwnerClientId,
                _serverRequestId);
        }

        _serverTarget = null;
        _serverRequestId = 0;

        // 여기서는 RPC를 보내지 않고 로컬 표시만 정리
        InteractableObject target = _requestedTarget;
        bool wasStarted = _localInteractionStarted;

        ClearLocalRequest();

        if (wasStarted && target != null)
            target.CancelInteraction();

        ClearDetection();
        _isLocalPlayer = false;

        base.OnNetworkDespawn();
    }

    [ServerRpc]
    private void RequestStartServerRpc(
    NetworkObjectReference targetReference,
    ulong requestId,
    ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        bool approved = false;

        if (targetReference.TryGet(
                out NetworkObject targetObject, NetworkManager) &&
            targetObject.IsSpawned &&
            targetObject.TryGetComponent(out InteractionNetwork target))
        {
            // 이 플레이어에게 이미 진행 중인 다른 요청이 있는지 확인
            if (_serverTarget == null)
            {
                approved = target.TryBeginOnServer(senderId, requestId);

                if (approved)
                {
                    _serverTarget = target;
                    _serverRequestId = requestId;
                }
            }
            else if (_serverTarget == target &&
                     _serverRequestId == requestId)
            {
                approved = target.TryBeginOnServer(senderId, requestId);
            }
        }

        var clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { senderId }
            }
        };

        ReceiveStartResultClientRpc(
            requestId,
            approved,
            clientRpcParams);
    }

    [ServerRpc]
    private void RequestCancelServerRpc(ulong requestId, ServerRpcParams rpcParams = default)
    {
        if (_serverRequestId != requestId)
            return;

        if (_serverTarget != null)
        {
            _serverTarget.TryCancelOnServer(
                rpcParams.Receive.SenderClientId,
                requestId);
        }

        _serverTarget = null;
        _serverRequestId = 0;
    }

    [ClientRpc]
    private void ReceiveStartResultClientRpc(
        ulong requestId,
        bool approved,
        ClientRpcParams rpcParams = default)
    {
        if (!IsOwner)
            return;

        // 이미 취소했거나 다른 요청으로 넘어간 경우
        if (!_hasLocalRequest || _activeRequestId != requestId)
        {
            if (approved)
                RequestCancelServerRpc(requestId);

            return;
        }

        if (!approved)
        {
            ClearLocalRequest();
            ReleaseTarget();

            InteractionStartResolved?.Invoke(false);
            return;
        }

        // 중복 승인으로 진행률을 다시 초기화하지 않는다.
        if (_localInteractionStarted)
            return;

        if (_requestedTarget == null ||
            LockedTarget != _requestedTarget ||
            !BeginLockedInteraction())
        {
            CancelRequestedInteraction();
            InteractionStartResolved?.Invoke(false);
            return;
        }

        _localInteractionStarted = true;
        InteractionStartResolved?.Invoke(true);
    }
}