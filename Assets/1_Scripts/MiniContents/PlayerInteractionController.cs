using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerInteractionController : NetworkBehaviour
{
    [SerializeField] private Transform _distanceOrigin;

    // Inspector에서 위 → 아래 → 오른쪽 → 왼쪽 순서로 연결
    [SerializeField] private Transform[] _uiAnchors = new Transform[4];

    [Header("자원 상호작용 검증")]
    [SerializeField, Min(1f)] private float _preparationTimeout = 30f;
    [SerializeField, Min(0f)] private float _contactTolerance = 0.15f;
    [SerializeField, Min(0.01f)] private float _fishingCastSeconds = 3.33f;
    [Header("벌목 모션")]
    [SerializeField, Min(0.01f)] private float _loggingSwingSeconds = 2f;
    [Tooltip("Logging 클립 24fps의 7번 프레임(logging_05). 전체 60프레임 중 7/60 지점.")]
    [SerializeField, Range(0f, 1f)] private float _loggingImpactNormalized = 7f / 60f;
    private int _localLoggingSwingSequence;
    private int _localFinishedLoggingSwingSequence;
    private bool _localLoggingSwingPending;
    private bool _serverLoggingImpactNotified;
    private double _serverLoggingImpactTime;
    private int _serverQueuedLoggingSwingSequence;
    private int _serverFinalLoggingSequence;
    private float _serverFinalLoggingAccuracy;
    private bool _serverLoggingCompletionAccepted;
    private bool _localLoggingCompletionAccepted;
    private readonly NetworkVariable<PlayerResourceAnimationState> _resourceAnimation = new(default,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private PlayerControllerView _view;
    public PlayerResourceAnimationState ResourceAnimation
    {
        get
        {
            if (!IsSpawned) return default;
            PlayerResourceAnimationState state = _resourceAnimation.Value;
            if (IsOwner && (!_hasLocalRequest || state.RequestId != _activeRequestId || _awaitingCompletion))
                return default;
            if (IsOwner && state.Kind == ResourceAnimationKind.Logging &&
                state.Phase == ResourceAnimationPhase.Swing &&
                (state.SwingSequence > _localLoggingSwingSequence || state.SwingSequence <= _localFinishedLoggingSwingSequence))
                return default;
            return state;
        }
    }
    private PlayerRuneCombatActor _actor;
    private PlayerRuneAttackController _attack;
    private Collider2D _bodyCollider;
    private Collider2D[] _serverTargetColliders;
    private InteractableObject _serverContent;
    private bool _localProgressStarted;
    private bool _awaitingCompletion;
    private bool _awaitingHarvest;
    public float LoggingImpactNormalized => _loggingImpactNormalized;
    private bool _serverContentReady;
    private bool _serverProgressStarted;
    private bool _serverAlignmentRequired;
    private bool _serverAlignmentReported;
    private bool _serverAlignmentComplete;
    private Vector3 _serverAlignmentTarget;
    private Vector3 _serverAlignmentHitPoint;
    private int _serverAlignmentFacing;
    private bool _localAlignmentPending;
    private double _serverReservedAt;
    public bool HasLocalRequest => _hasLocalRequest;
    public bool HasServerRequest => IsServer && _serverRequestId != 0;

    private void Awake()
    {
        _actor = GetComponent<PlayerRuneCombatActor>();
        _attack = GetComponent<PlayerRuneAttackController>();
        _bodyCollider = GetComponent<Collider2D>();
        _view = GetComponent<PlayerControllerView>();
    }

    private void LateUpdate()
    {
        // 원격 플레이어의 View.Update가 꺼져 있어도 표시를 갱신합니다.
        _view?.UpdateResourcePresentation(ResourceAnimation, IsSpawned ? NetworkManager.ServerTime.Time : 0);
    }

    private Camera _playerCamera;
    private bool _isLocalPlayer;
    private bool _scenarioInputBlocked;

    private readonly Dictionary<Collider2D, InteractableObject> _contacts = new();

    private readonly HashSet<InteractableObject> _candidates = new();

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

    // 입력 담당자가 이 이벤트를 받아 진행 중인 상호작용을 취소한다.
    public event Action<InteractableObject> LockedTargetLost;

    public event Action InteractionEnded;

    private string _requestedEquipmentId;

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

    private void OnTriggerEnter2D(Collider2D other)
    {
        RegisterContact(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // 초기화 전에 이미 범위 안에 있던 대상도 등록한다.
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
        if (IsSpawned && IsServer && HasServerRequest)
        {
            bool requestValid = _serverLoggingCompletionAccepted
                ? _actor != null && _actor.IsAlive && !_actor.IsHitControlLocked
                : IsServerRequestValid();
            if (!requestValid || (_serverAlignmentRequired && !_serverAlignmentComplete &&
                NetworkManager.ServerTime.Time - _serverReservedAt > 2f) || (!_serverProgressStarted &&
                NetworkManager.ServerTime.Time - _serverReservedAt > _preparationTimeout))
                CancelInteractionOnServer();
            else
            {
                TryStartServerProgress();
                UpdateLoggingSwingOnServer();
            }
        }
        if (_hasLocalRequest && !_awaitingCompletion && (_actor == null || !_actor.IsAlive))
            CancelRequestedInteraction();
        if (!_isLocalPlayer)
            return;

        if (_localAlignmentPending && (_requestedTarget == null ||
            !CanInteractWithTool(_requestedTarget, out _, false))) CancelRequestedInteraction();
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

            if (target is ScenarioNpcInteractable && !target.ViewModel.IsAvailable && target != _lockedTarget)
                continue;

            if (target.IsPlayerInInteractionArea(transform.position)) _candidates.Add(target);
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
        InteractableObject nextInfoTarget = !_scenarioInputBlocked && ReferenceEquals(_lockedTarget, null) ? _selectedTarget : null;

        if (nextInfoTarget != null)
            nextInfoTarget.ViewModel.HasRequiredTool = CanInteractWithTool(nextInfoTarget, out _, false);

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

        _lockedTarget.Completed -= CompleteRequestedInteraction;
        _lockedTarget.Completed += CompleteRequestedInteraction;

        _lockedTarget.BeginInteraction(_uiAnchors, _playerCamera, _requestedEquipmentId);
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
        CancelInteractionOnServer();
    }

    public bool TryRequestInteraction()
    {
        if (!IsSpawned || !IsOwner || _hasLocalRequest || _scenarioInputBlocked ||
            _actor == null || !_actor.IsAlive || (_attack != null && _attack.IsAttackActive))
            return false;

        if (!TryLockSelectedTarget(out InteractableObject target))
            return false;

        if (!CanInteractWithTool(target, out string equipmentId))
        {
            ReleaseTarget();
            return false;
        }

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
        _activeRequestId = requestId;
        _requestedTarget = target;
        _requestedEquipmentId = equipmentId;
        _hasLocalRequest = true;
        _localInteractionStarted = false;

        _usesServerReservation = target.RequiresServerReservation;

        target.InteractionEndRequested += OnInteractionEndRequested;
        target.ContentReady += OnContentReady;
        _attack?.CancelAttackInput();

        if (!_usesServerReservation)
        {
            return StartCurrentInteraction(requestId);
        }

        RequestStartServerRpc(new NetworkObjectReference(network.NetworkObject), _activeRequestId);

        return true;
    }

    public void SetScenarioInputBlocked(bool blocked)
    {
        _scenarioInputBlocked = blocked;
        RefreshInfoTarget();
    }

    //공통 시작 함수 추가
    private bool StartCurrentInteraction(ulong requestId)
    {
        if ((_hasLocalRequest == false) || (_activeRequestId != requestId))
        {
            return false;
        }

        // BeginInteraction 내부에서 즉시 종료 이벤트가 발생하더라도
        // CancelInteraction까지 실행될 수 있도록 먼저 설정
        _localInteractionStarted = true;

        bool started;
        try { started = BeginLockedInteraction(); }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            CancelRequestedInteraction();
            InteractionStartResolved?.Invoke(false);
            return false;
        }

        // 실행 도중 실패·취소 이벤트로 요청이 종료된 경우
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

        if (!TargetRequiresPreparation()) OnContentReady();
        InteractionStartResolved?.Invoke(true);
        return true;
    }

    // 상호작용 종료 이벤트가 발생하면 요청을 취소하는 메서드 추가
    private void OnInteractionEndRequested()
    {
        CancelRequestedInteraction();
    }

    public void TickInteraction(float deltaTime)
    {
        if (!IsOwner ||
            !_hasLocalRequest ||
            !_localInteractionStarted || !_localProgressStarted || _awaitingCompletion ||
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

        ClearLocalRequest();

        if (wasStarted && target != null)
            target.CancelInteraction();

        if (usesServerReservation &&IsSpawned && IsOwner)
            RequestCancelServerRpc(requestId);

        ReleaseTarget();
    }

    public void CancelInteractionOnServer()
    {
        if (!IsServer || !IsSpawned || !HasServerRequest) return;
        ulong requestId = _serverRequestId;
        ReleaseServerRequest();
        ReceiveEndResultClientRpc(requestId, false, OwnerRpcParams());
    }

    private void ReleaseServerRequest()
    {
        if (_serverTarget != null)
            _serverTarget.TryCancelOnServer(OwnerClientId, _serverRequestId);
        _serverAlignmentRequired = false;
        _serverAlignmentReported = false;
        _serverAlignmentComplete = false;
        _serverTarget = null;
        _serverRequestId = 0;
        _serverTargetColliders = null;
        _serverContent = null;
        _serverContentReady = false;
        _serverProgressStarted = false;
        _serverLoggingImpactNotified = false;
        _serverQueuedLoggingSwingSequence = 0;
        _serverLoggingImpactTime = 0;
        _serverFinalLoggingSequence = 0;
        _serverFinalLoggingAccuracy = 0f;
        _serverLoggingCompletionAccepted = false;
        if (IsServer && IsSpawned) _resourceAnimation.Value = default;
    }

    private void ClearLocalRequest()
    {
        _localAlignmentPending = false;
        _view?.StopLoggingAlignment();
        bool hadRequest = _hasLocalRequest;
        _requestedEquipmentId = null;

        if (_requestedTarget != null)
        {
            _requestedTarget.InteractionEndRequested -= OnInteractionEndRequested;
            _requestedTarget.Completed -= CompleteRequestedInteraction;
            _requestedTarget.ContentReady -= OnContentReady;
        }

        _usesServerReservation = false;
        _hasLocalRequest = false;
        _localInteractionStarted = false;
        _localProgressStarted = false;
        _awaitingCompletion = false;
        _awaitingHarvest = false;
        _localLoggingSwingPending = false;
        _localLoggingSwingSequence = 0;
        _localLoggingCompletionAccepted = false;
        _localFinishedLoggingSwingSequence = 0;
        _requestedTarget = null;
        _activeRequestId = 0;

        if (hadRequest)
            InteractionEnded?.Invoke();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer) _resourceAnimation.Value = default;

        if (IsOwner)
        {
            LockedTargetLost += OnLockedTargetLost;
            Initialize(true, Camera.main);
        }
    }

    private void OnLockedTargetLost(InteractableObject target)
    {
        // 침강 완료로 나무가 사라져도 이미 확정된 마지막 도끼 모션은 유지합니다.
        if (_localLoggingCompletionAccepted) return;
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

        ReleaseServerRequest();

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
        string fishId = string.Empty;

        if (senderId == OwnerClientId && requestId != 0 && _actor != null && _actor.IsAlive &&
            !_actor.IsHitControlLocked && (_attack == null || !_attack.IsAttackingOnServer) && targetReference.TryGet(
                out NetworkObject targetObject, NetworkManager) &&
            targetObject.IsSpawned &&
            targetObject.TryGetComponent(out InteractionNetwork target) &&
            targetObject.TryGetComponent(out InteractableObject content) && content.isActiveAndEnabled)
        {
            // 이 플레이어에게 이미 진행 중인 다른 요청이 있는지 확인
            if (_serverTarget == null)
            {
                Collider2D[] bounds = target.GetComponentsInChildren<Collider2D>();
                bool align = content.InteractionType == "Logging" && content.UsesPlacedVisual;
                Vector3 destination = default, hitPoint = default;
                int facing = content.GetResourceFacing(transform.position);
                bool geometryValid = !align || (_view != null && content.TryGetLoggingHitPoint(out hitPoint) &&
                    _view.TryCalculateLoggingPosition(hitPoint, facing, out destination) && content.IsPlayerInInteractionArea(destination));
                approved = geometryValid && content.IsPlayerInInteractionArea(transform.position) && HasContact(bounds) && target.TryBeginOnServer(senderId, requestId, out fishId);

                if (approved)
                {
                    _serverAlignmentRequired = align;
                    _serverAlignmentReported = false;
                    _serverAlignmentComplete = !align;
                    _serverAlignmentTarget = destination;
                    _serverAlignmentHitPoint = hitPoint;
                    _serverAlignmentFacing = facing;
                    _serverTarget = target;
                    _serverRequestId = requestId;
                    _serverTargetColliders = bounds;
                    _serverContent = content;
                    _serverContentReady = false;
                    _serverProgressStarted = false;
                    _serverReservedAt = NetworkManager.ServerTime.Time;
                    _attack?.CancelPendingInputOnServer();
                    PublishResourcePreparation(content.InteractionType);
                }
            }
            else if (_serverTarget == target &&
                     _serverRequestId == requestId)
            {
                approved = target.TryBeginOnServer(senderId, requestId, out fishId);
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
            fishId,
            _serverAlignmentRequired, _serverAlignmentTarget,
            clientRpcParams);
    }

    private void CompleteRequestedInteraction(float accuracy)
    {
        if (_localLoggingCompletionAccepted) return;
        if (!IsSpawned || !IsOwner || !_hasLocalRequest || !_localProgressStarted || _awaitingCompletion)
            return;
        _awaitingCompletion = true;
        _awaitingHarvest = _requestedTarget is InteractionGatheringObject;
        if (!_usesServerReservation)
        {
            FinishLocalRequest(_activeRequestId);
            return;
        }
        RequestCompleteServerRpc(_activeRequestId, accuracy);
    }

    [ServerRpc]
    private void RequestCompleteServerRpc(ulong requestId, float accuracy, ServerRpcParams rpcParams = default)
    {
        bool completed = false;
        if (_serverRequestId == requestId && HasServerRequest)
        {
            try
            {
                completed = _serverProgressStarted && IsServerRequestValid() &&
                    (_resourceAnimation.Value.Kind != ResourceAnimationKind.Logging ||
                     _resourceAnimation.Value.Phase == ResourceAnimationPhase.WaitingForInput) &&
                    _serverTarget.CompleteInteraction(OwnerClientId, requestId, accuracy);
            }
            catch (Exception exception) { Debug.LogException(exception, this); }
            finally { ReleaseServerRequest(); }
        }
        ReceiveEndResultClientRpc(requestId, completed, OwnerRpcParams());
    }

    [ServerRpc]
    private void RequestCancelServerRpc(ulong requestId)
    {
        if (_serverRequestId == requestId) ReleaseServerRequest();
        ReceiveEndResultClientRpc(requestId, false, OwnerRpcParams());
    }

    private ClientRpcParams OwnerRpcParams()
    {
        return new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } } };
    }

    [ClientRpc]
    private void ReceiveEndResultClientRpc(ulong requestId, bool completed, ClientRpcParams rpcParams = default)
    {
        if (!IsOwner) return;
        if (completed && _hasLocalRequest && requestId == _activeRequestId && _awaitingHarvest &&
            _view != null && _view.CanPlayActionSound && TryGetComponent(out PlayerActionSound sounds))
            sounds.PlayHarvest();
        if (_hasLocalRequest && _activeRequestId == requestId && _awaitingCompletion && !completed)
            Debug.LogWarning("서버에서 채취 완료가 거절되어 요청을 정리했습니다.", this);
        FinishLocalRequest(requestId);
    }

    private void FinishLocalRequest(ulong requestId)
    {
        if (!_hasLocalRequest || _activeRequestId != requestId) return;
        InteractableObject target = _requestedTarget;
        bool started = _localInteractionStarted;
        ClearLocalRequest();
        if (started && target != null) target.CancelInteraction();
        ReleaseTarget();
    }

    [ClientRpc]
    private void ReceiveStartResultClientRpc(
        ulong requestId,
        bool approved,
        string fishId,
        bool align, Vector3 destination,
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
        if (_localInteractionStarted || _localAlignmentPending)
            return;

        if (_requestedTarget == null ||
            LockedTarget != _requestedTarget)
        {
            CancelRequestedInteraction();
            InteractionStartResolved?.Invoke(false);
            return;
        }

        if (_requestedTarget is InteractionFishingObject fishingObject)
        {
            fishingObject.SetSelectedFishId(fishId);
        }


        if (align)
        {
            _localAlignmentPending = true;
            if (_view == null || !_view.BeginLoggingAlignment(requestId, destination)) CancelRequestedInteraction();
            return;
        }
        StartCurrentInteraction(requestId);
    }

    public void ConfirmCurrentInteraction()
    {
        if (!IsSpawned || !IsOwner || !_hasLocalRequest || !_localInteractionStarted || !_localProgressStarted || _awaitingCompletion || _requestedTarget == null)
            return;

        if (_requestedTarget is InteractionLoggingMiningObject logging && logging.IsLogging)
        {
            if (_localLoggingSwingPending || !logging.TryConfirmLoggingSwing()) return;
            _localLoggingSwingPending = true;
            _localLoggingSwingSequence++;
            RequestLoggingSwingServerRpc(_activeRequestId, _localLoggingSwingSequence,
                logging.IsFinalLoggingJudgment, logging.LoggingFinalAccuracy);
            return;
        }
        _requestedTarget.ConfirmInteraction();
    }

    [ServerRpc]
    private void RequestLoggingSwingServerRpc(ulong requestId, int sequence, bool finalJudgment, float finalAccuracy)
    {
        if (!HasServerRequest || requestId != _serverRequestId || _serverLoggingCompletionAccepted) return;
        PlayerResourceAnimationState state = _resourceAnimation.Value;
        // 이미 받은 타격 요청은 시작 시각을 다시 설정하지 않습니다.
        if (sequence <= state.SwingSequence || sequence == _serverQueuedLoggingSwingSequence) return;
        bool canQueue = state.Phase == ResourceAnimationPhase.Swing && _serverLoggingImpactNotified &&
            _serverQueuedLoggingSwingSequence == 0;
        if (!_serverProgressStarted || !IsServerRequestValid() ||
            state.Kind != ResourceAnimationKind.Logging ||
            (state.Phase != ResourceAnimationPhase.WaitingForInput && !canQueue) ||
            sequence != state.SwingSequence + 1 || sequence > InteractionLoggingMiningVM.TotalRounds ||
            (finalJudgment && (float.IsNaN(finalAccuracy) || float.IsInfinity(finalAccuracy) ||
                finalAccuracy < 0f || finalAccuracy > 1f)))
        {
            CancelInteractionOnServer();
            return;
        }
        if (finalJudgment)
        {
            _serverFinalLoggingSequence = sequence;
            _serverFinalLoggingAccuracy = finalAccuracy;
        }
        if (canQueue)
        {
            _serverQueuedLoggingSwingSequence = sequence;
            return;
        }
        BeginLoggingSwingOnServer(state, sequence);
    }

    private void BeginLoggingSwingOnServer(PlayerResourceAnimationState state, int sequence)
    {
        state.Phase = ResourceAnimationPhase.Swing;
        state.SwingSequence = sequence;
        state.StartTime = NetworkManager.ServerTime.Time;
        state.MotionDuration = float.IsNaN(_loggingSwingSeconds) || float.IsInfinity(_loggingSwingSeconds)
            ? 2f : Mathf.Max(0.01f, _loggingSwingSeconds);
        float impact = float.IsNaN(_loggingImpactNormalized) || float.IsInfinity(_loggingImpactNormalized)
            ? 7f / 60f : Mathf.Clamp01(_loggingImpactNormalized);
        _serverLoggingImpactTime = state.StartTime + state.MotionDuration * impact;
        _serverTarget?.ScheduleLoggingImpactOnServer(OwnerClientId, state.RequestId, sequence, _serverLoggingImpactTime);
        _serverLoggingImpactNotified = false;
        _resourceAnimation.Value = state;
    }

    private void UpdateLoggingSwingOnServer()
    {
        PlayerResourceAnimationState state = _resourceAnimation.Value;
        if (state.Kind != ResourceAnimationKind.Logging || state.Phase != ResourceAnimationPhase.Swing) return;
        double now = NetworkManager.ServerTime.Time;
        if (!_serverLoggingImpactNotified && now >= _serverLoggingImpactTime)
        {
            _serverLoggingImpactNotified = true;
            // 마지막 판정 결과를 타격 요청과 함께 받아, 접촉 시점에 바로 침강을 시작합니다.
            if (state.SwingSequence == _serverFinalLoggingSequence && _serverFinalLoggingAccuracy > 0f &&
                _serverTarget != null && _serverTarget.CompleteInteraction(
                    OwnerClientId, state.RequestId, _serverFinalLoggingAccuracy))
            {
                _serverLoggingCompletionAccepted = true;
                ReceiveLoggingCompletionAcceptedClientRpc(state.RequestId, OwnerRpcParams());
            }
            ReceiveLoggingImpactClientRpc(state.RequestId, state.SwingSequence, OwnerRpcParams());
        }
        if (now < state.StartTime + state.MotionDuration) return;
        if (_serverLoggingCompletionAccepted)
        {
            ReleaseServerRequest();
            ReceiveEndResultClientRpc(state.RequestId, true, OwnerRpcParams());
            return;
        }
        state.Phase = ResourceAnimationPhase.WaitingForInput;
        _resourceAnimation.Value = state;
        ReceiveLoggingSwingFinishedClientRpc(state.RequestId, state.SwingSequence, OwnerRpcParams());
        if (HasServerRequest && _serverRequestId == state.RequestId && _serverQueuedLoggingSwingSequence != 0)
        {
            int queued = _serverQueuedLoggingSwingSequence;
            _serverQueuedLoggingSwingSequence = 0;
            BeginLoggingSwingOnServer(state, queued);
        }
    }

    [ClientRpc]
    private void ReceiveLoggingCompletionAcceptedClientRpc(ulong requestId, ClientRpcParams rpcParams = default)
    {
        if (!IsOwner || !_hasLocalRequest || requestId != _activeRequestId) return;
        _localLoggingCompletionAccepted = true;
    }

    [ClientRpc]
    private void ReceiveLoggingImpactClientRpc(ulong requestId, int sequence, ClientRpcParams rpcParams = default)
    {
        if (!IsOwner || !_hasLocalRequest || requestId != _activeRequestId ||
            !_localLoggingSwingPending || sequence != _localLoggingSwingSequence) return;
        if (_localLoggingCompletionAccepted)
        {
            if (_requestedTarget is InteractionLoggingMiningObject finalLogging)
                finalLogging.ShowLoggingCompletionAtImpact();
            return;
        }
        if (_requestedTarget is InteractionLoggingMiningObject logging && logging.ResumeLoggingJudgmentAtImpact())
            _localLoggingSwingPending = false;
    }

    [ClientRpc]
    private void ReceiveLoggingSwingFinishedClientRpc(ulong requestId, int sequence, ClientRpcParams rpcParams = default)
    {
        if (!IsOwner || !_hasLocalRequest || requestId != _activeRequestId ||
            sequence > _localLoggingSwingSequence || sequence <= _localFinishedLoggingSwingSequence) return;
        _localFinishedLoggingSwingSequence = sequence;
        if (sequence != _localLoggingSwingSequence) return;
        _localLoggingSwingPending = false;
        if (_requestedTarget is InteractionLoggingMiningObject logging && logging.IsLogging)
            logging.CompleteLoggingSwing();
    }

    private bool TargetRequiresPreparation() => _requestedTarget != null && _requestedTarget.RequiresContentReady;

    private void OnContentReady()
    {
        if (!_hasLocalRequest || !_localInteractionStarted || _localProgressStarted) return;
        if (_usesServerReservation) RequestReadyServerRpc(_activeRequestId);
        else StartLocalProgress(_activeRequestId);
    }

    [ServerRpc]
    private void RequestReadyServerRpc(ulong requestId)
    {
        if (!HasServerRequest || _serverRequestId != requestId) return;
        _serverContentReady = true;
        TryStartServerProgress();
    }

    private void TryStartServerProgress()
    {
        if (_serverProgressStarted || !IsServerRequestValid()) return;
        if (_serverAlignmentRequired && !_serverAlignmentComplete)
        {
            if (!_serverAlignmentReported || Mathf.Abs(transform.position.x - _serverAlignmentTarget.x) > _view.LoggingAlignmentTolerance) return;
            _serverAlignmentComplete = true;
        }
        if (!_serverContentReady) return;
        PlayerResourceAnimationState animation = _resourceAnimation.Value;
        if (animation.Kind == ResourceAnimationKind.Fishing &&
            NetworkManager.ServerTime.Time < animation.StartTime + animation.CastDuration) return;
        _serverProgressStarted = true;
        animation.Phase = animation.Kind == ResourceAnimationKind.Logging
            ? ResourceAnimationPhase.WaitingForInput : ResourceAnimationPhase.Active;
        animation.StartTime = NetworkManager.ServerTime.Time;
        _resourceAnimation.Value = animation;
        ReceiveProgressClientRpc(_serverRequestId, OwnerRpcParams());
    }

    private void PublishResourcePreparation(string interactionType)
    {
        ResourceAnimationKind kind = interactionType == "Logging" ? ResourceAnimationKind.Logging :
            interactionType == "Mining" ? ResourceAnimationKind.Mining :
            interactionType == "Fishing" ? ResourceAnimationKind.Fishing : ResourceAnimationKind.None;
        _resourceAnimation.Value = new PlayerResourceAnimationState
        {
            RequestId = _serverRequestId,
            Kind = kind,
            Phase = kind == ResourceAnimationKind.Fishing ? ResourceAnimationPhase.Casting : ResourceAnimationPhase.Preparing,
            StartTime = NetworkManager.ServerTime.Time,
            CastDuration = Mathf.Max(0.01f, _fishingCastSeconds),
            FacingDirection = _serverContent != null ? _serverContent.GetResourceFacing(transform.position) : 0
        };
    }

    [ClientRpc]
    private void ReceiveProgressClientRpc(ulong requestId, ClientRpcParams rpcParams = default)
    {
        if (IsOwner) StartLocalProgress(requestId);
    }

    private void StartLocalProgress(ulong requestId)
    {
        if (!_hasLocalRequest || _activeRequestId != requestId || _localProgressStarted || _requestedTarget == null)
            return;
        if (_requestedTarget.RequiresContentReady && !_requestedTarget.IsContentReady) return;
        _localProgressStarted = true;
        try { _requestedTarget.StartPreparedInteraction(); }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            CancelRequestedInteraction();
        }
    }

    private bool IsServerRequestValid()
    {
        return _actor != null && _actor.IsAlive && !_actor.IsHitControlLocked &&
            _serverContent != null && _serverContent.isActiveAndEnabled &&
            _serverTarget != null && _serverTarget.isActiveAndEnabled && _serverTarget.IsReady &&
            _serverTarget.HasReservationOnServer(OwnerClientId, _serverRequestId) && _serverContent.IsPlayerInInteractionArea(transform.position) && HasContact(_serverTargetColliders) && IsServerLoggingAlignmentValid();
    }

    public void OnLoggingAlignmentFinished(ulong requestId, bool success)
    {
        if (!_hasLocalRequest || requestId != _activeRequestId || !_localAlignmentPending) return;
        _localAlignmentPending = false;
        if (!success || _requestedTarget == null || !CanInteractWithTool(_requestedTarget, out _, false))
        {
            CancelRequestedInteraction();
            return;
        }
        ReportLoggingAlignmentServerRpc(requestId);
        StartCurrentInteraction(requestId);
    }

    [ServerRpc]
    private void ReportLoggingAlignmentServerRpc(ulong requestId)
    {
        if (!HasServerRequest || requestId != _serverRequestId || !_serverAlignmentRequired) return;
        _serverAlignmentReported = true;
        TryStartServerProgress();
    }

    private bool IsServerLoggingAlignmentValid()
    {
        if (!_serverAlignmentRequired) return true;
        if (_view == null || !_serverContent.TryGetLoggingHitPoint(out Vector3 hitPoint) ||
            Vector3.Distance(hitPoint, _serverAlignmentHitPoint) > 0.02f ||
            !_view.TryCalculateLoggingPosition(hitPoint, _serverAlignmentFacing, out Vector3 destination) ||
            Mathf.Abs(destination.x - _serverAlignmentTarget.x) > 0.02f) return false;
        return !_serverAlignmentComplete || Mathf.Abs(transform.position.x - _serverAlignmentTarget.x) <= _view.LoggingAlignmentTolerance;
    }

    private bool HasContact(Collider2D[] bounds)
    {
        if (_bodyCollider == null || !_bodyCollider.enabled || bounds == null) return false;
        foreach (Collider2D area in bounds)
        {
            if (area == null || !area.enabled || !area.gameObject.activeInHierarchy) continue;
            ColliderDistance2D distance = Physics2D.Distance(_bodyCollider, area);
            if (distance.isValid && (distance.isOverlapped || distance.distance <= _contactTolerance)) return true;
        }
        return false;
    }

    private bool CanInteractWithTool(InteractableObject target, out string equipmentId, bool logFailure = true)
    {
        equipmentId = null;

        string requiredAction = target.RequiredToolAction;

        if (string.IsNullOrWhiteSpace(requiredAction))
            return true;

        if (!Enum.TryParse(requiredAction, out ToolAction toolAction) || !Enum.IsDefined(typeof(ToolAction), toolAction))
        {
            if (logFailure)
                Debug.LogWarning($"필요한 도구 설정을 확인해 주세요: {requiredAction}", target);

            return false;
        }

        if (toolAction == ToolAction.None)
            return true;

        return TryGetQuickSlotToolId(toolAction, out equipmentId, logFailure);
    }

    private bool TryGetQuickSlotToolId(ToolAction requiredAction, out string equipmentId, bool logFailure)
    {
        equipmentId = null;

        EquipmentSlot requiredSlot;
        switch (requiredAction)
        {
            case ToolAction.Logging:
                requiredSlot = EquipmentSlot.Axe;
                break;
            case ToolAction.Fishing:
                requiredSlot = EquipmentSlot.FishingRod;
                break;
            case ToolAction.Mining:
                requiredSlot = EquipmentSlot.PickAxe;
                break;
            default:
                return false;
        }

        InventoryService inventoryService = ServiceManager.Inst != null
            ? ServiceManager.Inst.InventoryService
            : null;

        if (inventoryService == null || DataManager.Inst == null)
        {
            if (logFailure)
                Debug.LogWarning("장비 확인에 필요한 인벤토리 또는 데이터 매니저가 준비되지 않았습니다.", this);
            return false;
        }

        string equippedItemId = inventoryService.GetEquipmentItemId(requiredSlot);
        if (string.IsNullOrWhiteSpace(equippedItemId))
        {
            if (logFailure)
                Debug.Log($"{requiredAction}에 필요한 도구를 장비 슬롯에 등록해 주세요.", this);
            return false;
        }

        if (!DataManager.Inst.ReadData<EquipmentData>().TryGetValue(equippedItemId, out EquipmentData equipmentData)
            || equipmentData == null || equipmentData.EquipmentSlot != requiredSlot
            || equipmentData.ToolAction != requiredAction)
        {
            if (logFailure)
                Debug.LogWarning($"슬롯에 등록된 장비의 도구 설정을 확인해 주세요: {equippedItemId}", this);
            return false;
        }

        equipmentId = equippedItemId;
        return true;
    }
}
