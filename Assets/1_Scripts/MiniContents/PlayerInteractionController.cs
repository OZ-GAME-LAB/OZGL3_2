癤using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerInteractionController : NetworkBehaviour
{
    [SerializeField] private Transform _distanceOrigin;

    // Inspector     ㅻⅨ履  쇱そ 濡 곌껐
    [SerializeField] private Transform[] _uiAnchors = new Transform[4];

    private Camera _playerCamera;
    private bool _isLocalPlayer;

    //private readonly Dictionary<Collider, InteractableObject>_contacts = new();
    private readonly Dictionary<Collider2D, InteractableObject> _contacts = new();


    private readonly HashSet<InteractableObject> _candidates = new();

    //private readonly List<Collider> _removeBuffer = new();
    private readonly List<Collider2D> _removeBuffer = new();

    private bool _usesServerReservation;

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

    // � 대뱀媛  대깽몃� 諛 吏 以 몄⑹ 痍⑥.
    public event Action<InteractableObject> LockedTargetLost;

    // 蹂몄 �댁 щ ㅼ ъ⑺ 移대쇰� �ы.
    // �댁 ㅽ몄 珥湲고  몄 ⑥
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
                "�댁 移대쇱 쨌쨌걔룹 Anchor瑜 곌껐 二쇱몄.", this);
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

    private void OnTriggerEnter2D(Collider2D other)
    {
        RegisterContact(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // 珥湲고 � 대� 踰    깅.
        RegisterContact(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        _contacts.Remove(other);
    }

    private void RegisterContact(Collider2D other)
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

            // 湲곗〈  痍⑥媛   蹂대 癒쇱 대吏濡 .
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
            Collider2D contact = pair.Key;
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

        foreach (Collider2D contact in _removeBuffer)
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

    // F瑜 瑜 媛 몄.
    //  怨� 肉, 踰 � 뱀몄 .
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

    // 踰媛  뱀명 , � 대뱀媛 몄.
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

        _lockedTarget.Completed -= CompleteRequestedInteraction;
        _lockedTarget.Completed += CompleteRequestedInteraction;

        _lockedTarget.BeginInteraction(_uiAnchors, _playerCamera);
        return true;
    }

    // 痍⑥쨌猷쨌踰 嫄곗 泥由ш   몄.
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

        InteractionNetwork network = null;

        if (target.RequiresServerReservation == true)
        {
            if (!target.TryGetComponent(out network) || !network.IsSpawned)
            {
                ReleaseTarget();
                return false;
            }
        }

        ulong requestId = ++_nextRequestId;

        _activeRequestId = ++_nextRequestId;
        _requestedTarget = target;
        _hasLocalRequest = true;
        _localInteractionStarted = false;

        _usesServerReservation = target.RequiresServerReservation;

        //몄 醫猷 대깽 援щ
        target.InteractionEndRequested += OnInteractionEndRequested;

        if (!_usesServerReservation)
        {
            return StartCurrentInteraction(requestId);
        }

        RequestStartServerRpc(new NetworkObjectReference(network.NetworkObject), _activeRequestId);

        return true;
    }

    //怨듯  ⑥ 異媛
    private bool StartCurrentInteraction(ulong requestId)
    {
        if ((_hasLocalRequest == false) || (_activeRequestId != requestId))
        {
            return false;
        }

        // BeginInteraction 대 利 醫猷 대깽멸 諛쇰
        // CancelInteraction源吏 ㅽ  濡 癒쇱 ㅼ
        _localInteractionStarted = true;

        bool started = BeginLockedInteraction();

        // ㅽ 以 ㅽ㉱룹랬 대깽몃 泥� 醫猷 寃쎌
        if ((_hasLocalRequest == false) || (_activeRequestId != requestId))
        {
            InteractionStartResolved?.Invoke(false);
            return false;
        }

        if (started == false)
        {
            CancelRequestedInteraction();
            InteractionStartResolved?.Invoke(false);
            return false;
        }

        InteractionStartResolved?.Invoke(true);
        return true;
    }

    // 몄 醫猷 대깽멸 諛硫 泥� 痍⑥ 硫 異媛
    private void OnInteractionEndRequested()
    {
        CancelRequestedInteraction();
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
        bool usesServerReservation = _usesServerReservation;

        // � ㅻ 뱀 듭 吏 吏 濡 癒쇱 �由
        ClearLocalRequest();

        if (wasStarted && target != null)
            target.CancelInteraction();

        if (usesServerReservation &&IsSpawned && IsOwner)
            RequestCancelServerRpc(requestId);

        ReleaseTarget();
    }

    private void ClearLocalRequest()
    {
        // 몄 醫猷 대깽 援щ 댁
        if (_requestedTarget != null)
        {
            _requestedTarget.InteractionEndRequested -= OnInteractionEndRequested;
        }

        _usesServerReservation = false;
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

        // ш린 RPC瑜 蹂대댁 怨 濡而 留 �由
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
            //  �댁댁寃 대� 吏 以 ㅻⅨ 泥� 吏 
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

        // 대� 痍⑥嫄곕 ㅻⅨ 泥�쇰 닿 寃쎌
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

        // 以蹂 뱀몄쇰 吏瑜 ㅼ 珥湲고吏 .
        if (_localInteractionStarted)
            return;

        if (_requestedTarget == null ||
            LockedTarget != _requestedTarget)
        {
            CancelRequestedInteraction();
            InteractionStartResolved?.Invoke(false);
            return;
        }

        StartCurrentInteraction(requestId);
    }

    private void CompleteRequestedInteraction()
    {
        if (!IsSpawned || !IsOwner ||
            !_hasLocalRequest || !_localInteractionStarted)
            return;

        ulong requestId = _activeRequestId;

        // 완료 후 입력 해제가 취소 요청을 보내지 않도록 정리
        ClearLocalRequest();

        RequestCompleteServerRpc(requestId);

        ReleaseTarget();
    }

    [ServerRpc]
    private void RequestCompleteServerRpc(
    ulong requestId,
    ServerRpcParams rpcParams = default)
    {
        if (_serverTarget == null || _serverRequestId != requestId)
            return;

        bool completed = _serverTarget.CompleteInteraction(
            rpcParams.Receive.SenderClientId,
            requestId);

        if (!completed)
            return;

        _serverTarget = null;
        _serverRequestId = 0;
    }
}