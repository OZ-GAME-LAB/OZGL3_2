using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(ScenarioSource))]
public sealed class ScenarioNpcInteractable : InteractableObject
{
    private sealed class NpcViewModel : InteractionVMBase
    {
        public string InfoText;
        public override string InteractionInfoText => InfoText;
    }

    [Header("상호작용 안내")]
    [SerializeField] private string _interactionText = "대화하기 (F)";
    private readonly NpcViewModel _viewModel = new NpcViewModel();
    private ScenarioService _service;
    private bool _interactionActive;
    public ScenarioSource Source { get; private set; }
    public override InteractionVMBase ViewModel => _viewModel;
    public override bool RequiresServerReservation => false;
    public override bool KeepInteractionOnRelease => true;

    protected override void Awake()
    {
        base.Awake();
        Source = GetComponent<ScenarioSource>();
        _viewModel.InfoText = _interactionText;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        ScenarioManager.AvailabilityChanged += BindService;
        BindService();
    }

    private void BindService()
    {
        ScenarioService service = ServiceManager.Inst != null ? ServiceManager.Inst.ScenarioService : null;
        if (_service != service)
        {
            UnbindService();
            _service = service;
            if (_service != null)
            {
                _service.StateChanged += RefreshAvailability;
                _service.Ended += OnScenarioEnded;
            }
        }
        RefreshAvailability();
    }

    private void RefreshAvailability()
    {
        _viewModel.IsAvailable = _service != null && _service.CanStart(Source);
    }

    public override UniTask InitObject(string id)
    {
        // 정적 NPC의 사건 ID는 ScenarioSource에 연결합니다. 채집/드롭 데이터와 분리합니다.
        BindService();
        return UniTask.CompletedTask;
    }

    public override void BeginInteraction(Transform[] playerUIAnchors, Camera playerCamera, string equipmentId)
    {
        base.BeginInteraction(playerUIAnchors, playerCamera, equipmentId);
        BindService();
        NetworkManager network = NetworkManager.Singleton;
        NetworkObject localPlayer = network != null && network.LocalClient != null ? network.LocalClient.PlayerObject : null;
        _interactionActive = true;
        if (localPlayer == null || !localPlayer.TryGetComponent(out PlayerControllerView player) ||
            !player.TryStartScenario(Source))
        {
            _interactionActive = false;
            RequestInteractionEnd();
        }
    }

    private void OnScenarioEnded(ScenarioSource source, ScenarioEndReason reason)
    {
        if (!_interactionActive || source != Source)
            return;
        _interactionActive = false;
        RequestInteractionEnd();
    }

    public override void CancelInteraction()
    {
        if (!_interactionActive)
            return;
        _interactionActive = false;
        _service?.CancelFromSource(Source);
    }

    private void UnbindService()
    {
        if (_service == null)
            return;
        _service.StateChanged -= RefreshAvailability;
        _service.Ended -= OnScenarioEnded;
        _service = null;
    }

    protected override void OnDisable()
    {
        ScenarioManager.AvailabilityChanged -= BindService;
        CancelInteraction();
        RequestInteractionEnd();
        UnbindService();
        base.OnDisable();
    }
}
