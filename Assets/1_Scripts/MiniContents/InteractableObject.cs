using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;

public abstract class InteractableObject : MonoBehaviour
{
    private InteractableInfoUI _infoUI;
    private UIManager _infoUIManager;
    private CancellationTokenSource _infoCancellation;
    private int _infoRequestVersion;
    protected UIBase _openUI;
    private CancellationTokenSource _contentCancellation;
    private UIManager _contentManager;
    private UIType _contentType;
    public bool IsContentReady { get; private set; }
    public virtual bool RequiresContentReady => false;
    public event Action ContentReady;
    private InteractionNetwork _interactionNetwork;
    public abstract InteractionVMBase ViewModel { get; }

    public event Action<float> Completed;

    public event Action InteractionEndRequested; 

    private bool _destroyAfterUse = true;
    public bool DestroyAfterUse => _destroyAfterUse;
    public event Action RespawnRequested;
    private GameObject _visualObject;
    private bool _isDormant;

    [Header("생활 상호작용 범위 효과")]
    [SerializeField] private ResourceRangeEffect _rangeEffect;
    [Header("자원 외형과 분리된 영역 프리팹")]
    [SerializeField] private ResourceInteractionLayout _loggingLayoutPrefab;
    [SerializeField] private ResourceInteractionLayout _miningLayoutPrefab;
    [SerializeField] private ResourceInteractionLayout _gatheringLayoutPrefab;
    private ResourceInteractionLayout _resourceLayout;
    public LoggingTreeEffect LoggingEffect => InteractionType == "Logging" && _resourceLayout != null
        ? _resourceLayout.LoggingEffect : null;

    public bool IsPlayerInInteractionArea(Vector3 position) =>
        _resourceLayout == null || _resourceLayout.ContainsPlayerPosition(position);

    public bool TryGetLoggingHitPoint(out Vector3 position)
    {
        position = default;
        return InteractionType == "Logging" && _resourceLayout != null &&
            _resourceLayout.TryGetLoggingHitPoint(out position);
    }

    public int GetResourceFacing(Vector3 playerPosition) => _resourceLayout != null && _resourceLayout.RequiresSide
        ? (_resourceLayout.TargetPosition.x >= playerPosition.x ? 1 : -1) : 0;

    public void ConfigureResourceLayout(string type, bool useLayout, Vector2 scale)
    {
        if (_resourceLayout != null) Destroy(_resourceLayout.gameObject);
        _resourceLayout = null;
        if (!useLayout) return;
        ResourceInteractionLayout prefab = type == "Logging" ? _loggingLayoutPrefab :
            type == "Mining" ? _miningLayoutPrefab : type == "Gathering" ? _gatheringLayoutPrefab : null;
        if (prefab == null) throw new InvalidOperationException("자원 외형 프리팹이 없습니다: " + type);
        if (TryGetComponent(out BoxCollider2D legacy)) legacy.enabled = false;
        _rangeEffect?.SetVisible(false);
        _resourceLayout = Instantiate(prefab, transform, false);
        _resourceLayout.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        _resourceLayout.Initialize();
        _resourceLayout.SetAvailablePresentation(false, false);
    }

    public virtual bool UsesMiniGameInput => false;
    public virtual bool KeepInteractionOnRelease => UsesMiniGameInput;

    public virtual bool RequiresServerReservation => true;

    public string RequiredToolAction { get; private set; }
    public string InteractionType { get; private set; }
    public bool UsesPlacedVisual { get; private set; }

    public void ConfigureFieldPresentation(string interactionType, bool usesPlacedVisual)
    {
        InteractionType = interactionType;
        UsesPlacedVisual = usesPlacedVisual;
        if (_rangeEffect != null && _resourceLayout == null)
        {
            bool supported = interactionType == "Gathering" || interactionType == "Fishing"
                || interactionType == "Logging" || interactionType == "Mining";
            _rangeEffect.Configure(supported ? GetComponent<BoxCollider2D>() : null);
        }
    }

    protected string InteractionEquipmentId { get; private set; }

    protected virtual void Awake()
    {
        _interactionNetwork = GetComponent<InteractionNetwork>();

        if (RequiresServerReservation && _interactionNetwork == null)
        {
            Debug.LogError($"{name}: InteractionNetwork 컴포넌트가 필요합니다.", this);
        }
    }

    protected virtual void OnEnable()
    {
        if (RequiresServerReservation == false)
        {
            // 지금 제작대는 활성 상태라면 로컬 UI를 열 수 있음
            HandleAvailabilityChanged(true);
            return;
        }

        if (_interactionNetwork == null)
        {
            HandleAvailabilityChanged(false);
            return;
        }

        _interactionNetwork.AvailabilityChanged += HandleAvailabilityChanged;

        HandleAvailabilityChanged(_interactionNetwork.IsAvailable);
    }

    protected virtual void OnDisable()
    {
        if (_interactionNetwork != null)
        {
            _interactionNetwork.AvailabilityChanged -= HandleAvailabilityChanged;
        }

        HandleAvailabilityChanged(false);
        HideInteractionInfo();
        CloseResourceUI();
    }

    protected virtual void OnDestroy()
    {
        ReleaseVisual();
    }

    public abstract UniTask InitObject(string id);

    public void PlayerInteract(/*장비 타입을 검사할 수 있는 장비 ID(string 타입) 매개변수 추가*/)
    {
        PlayContent(/*장비 타입을 검사할 수 있는 장비 ID(string 타입) 매개변수 추가*/);
    }

    private void RefreshRangeEffect()
    {
        if (_resourceLayout != null)
        {
            bool ready = isActiveAndEnabled && _interactionNetwork != null && _interactionNetwork.IsReady;
            _resourceLayout.SetDormant(_isDormant);
            _resourceLayout.SetAvailablePresentation(ready, ready && _interactionNetwork.IsClient);
            return;
        }
        if (_rangeEffect == null) return;
        // 도구/점유 여부가 아니라 자원 자체의 초기화·소진 상태로 범위를 표시합니다.
        _rangeEffect.SetVisible(isActiveAndEnabled && !_isDormant
            && _interactionNetwork != null && _interactionNetwork.IsClient && _interactionNetwork.IsReady);
    }

    private void HandleAvailabilityChanged(bool isAvailable)
    {
        ViewModel.IsUsedByOtherPlayer = !isAvailable && RequiresServerReservation
            && _interactionNetwork != null && _interactionNetwork.IsUsedByOtherPlayer;
        ViewModel.IsAvailable = isAvailable;
        RefreshRangeEffect();
    }

    public virtual void ShowInteractionInfo(Transform[] playerUIAnchors, Camera playerCamera)
    {
        HideInteractionInfo();
        _infoCancellation = new CancellationTokenSource();
        OpenInteractionInfoAsync(playerUIAnchors, playerCamera, _infoRequestVersion, _infoCancellation.Token).Forget();
    }

    private async UniTask OpenInteractionInfoAsync(Transform[] playerUIAnchors, Camera playerCamera, int version, CancellationToken token)
    {
        UIManager manager = UIManager.Inst;
        try
        {
            if (manager == null)
                return;
            InteractableInfoUI infoUI = await manager.OpenInteractableInfoUIAsync(token);
            if (this == null || token.IsCancellationRequested || version != _infoRequestVersion || !isActiveAndEnabled)
                return;
            if (infoUI == null)
            {
                if (manager.IsOpen(UIType.InteractableInfoUI))
                    manager.Close(UIType.InteractableInfoUI);
                Debug.LogError("프리팹에 InteractableInfoUI 컴포넌트가 필요합니다.");
                return;
            }
            _infoUIManager = manager;
            _infoUI = infoUI;
            _infoUI.Bind(ViewModel);
            if (_infoUI.TryGetComponent(out InteractionUIPositioner positioner))
                positioner.SetTargets(playerUIAnchors, playerCamera);
            else
            {
                Debug.LogError("안내 UI에 InteractionUIPositioner가 필요합니다.");
                HideInteractionInfo();
            }
        }
        catch (OperationCanceledException)
        {
            // 숨김/대상 전환 이후의 생성 결과는 UIManager의 비활성 캐시에만 남깁니다.
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (version == _infoRequestVersion)
                HideInteractionInfo();
        }
    }

    public virtual void HideInteractionInfo()
    {
        _infoRequestVersion++;
        if (_infoCancellation != null)
        {
            _infoCancellation.Cancel();
            _infoCancellation.Dispose();
            _infoCancellation = null;
        }
        if (_infoUI != null && _infoUIManager != null && _infoUIManager.IsOpen(UIType.InteractableInfoUI))
            _infoUIManager.Close(UIType.InteractableInfoUI);
        _infoUI = null;
        _infoUIManager = null;
    }

    protected void NotifyCompleted(float accuracy = 1f)
    {
        Completed?.Invoke(accuracy);
    }

    protected void RegisterVisual(GameObject visualObject)
    {
        if (this == null)
        {
            if (visualObject is not null)
                Addressables.Release(visualObject);

            return;
        }

        if (!ReferenceEquals(_visualObject, visualObject))
        {
            ReleaseVisual();
            _visualObject = visualObject;
        }

        if (_visualObject != null)
            _visualObject.SetActive(!_isDormant);
    }

    private void ReleaseVisual()
    {
        GameObject visualObject = _visualObject;
        _visualObject = null;

        if (visualObject is not null)
            Addressables.Release(visualObject);
    }

    public void ApplyDormantState(bool isDormant)
    {
        _isDormant = isDormant;
        RefreshRangeEffect();

        if (_visualObject != null)
            _visualObject.SetActive(!isDormant);
    }

    public void NotifyRespawnRequested()
    {
        RespawnRequested?.Invoke();
    }
    public bool ReactivateOnServer()
    {
        return _interactionNetwork != null &&
               _interactionNetwork.TryReactivateOnServer();
    }

    public void SetDestroyAfterUse(bool destroyAfterUse)
    {
        _destroyAfterUse = destroyAfterUse;
    }

    public void SetRequiredToolAction(string requiredToolAction)
    {
        RequiredToolAction = requiredToolAction;
    }

    public virtual void BeginInteraction(Transform[] playerUIAnchors, Camera playerCamera, string equipmentId)
    {
        CloseResourceUI();
        InteractionEquipmentId = equipmentId;
    }

    // 세 자원 미니게임만 공유하는 UI 수명입니다. 취소한 로딩은 새 UI를 닫지 않습니다.
    protected async UniTask<UIBase> OpenResourceUIAsync(UIType type, Transform[] anchors, Camera camera)
    {
        var cancellation = new CancellationTokenSource();
        _contentCancellation = cancellation;
        CancellationToken token = cancellation.Token;
        UIManager manager = UIManager.Inst;
        try
        {
            if (manager == null) throw new InvalidOperationException("UIManager가 준비되지 않았습니다.");
            while (manager.IsLoading(type))
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            token.ThrowIfCancellationRequested();
            UIBase ui = await manager.OpenMainRootAsync(type, token);
            token.ThrowIfCancellationRequested();
            if (this == null || !isActiveAndEnabled || _contentCancellation != cancellation) return null;
            _contentManager = manager;
            _contentType = type;
            _openUI = ui;
            if (ui == null || !ui.TryGetComponent(out InteractionUIPositioner positioner))
                throw new InvalidOperationException("미니게임 UI와 InteractionUIPositioner를 확인해 주세요.");
            positioner.SetTargets(anchors, camera);
            return ui;
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            if (this != null && _contentCancellation == cancellation)
            {
                CloseResourceUI();
                RequestInteractionEnd();
            }
        }
        return null;
    }

    protected void NotifyContentReady()
    {
        IsContentReady = true;
        ContentReady?.Invoke();
    }

    protected void CloseResourceUI()
    {
        IsContentReady = false;
        if (_contentCancellation != null)
        {
            _contentCancellation.Cancel();
            _contentCancellation.Dispose();
            _contentCancellation = null;
        }
        if (_openUI != null && _contentManager != null && _contentManager.IsOpen(_contentType))
            _contentManager.Close(_contentType);
        _openUI = null;
        _contentManager = null;
    }

    public virtual void StartPreparedInteraction() { }

    protected bool TryGetInteractionEquipmentData(out EquipmentData equipmentData)
    {
        equipmentData = null;

        if (string.IsNullOrWhiteSpace(InteractionEquipmentId))
        {
            Debug.LogWarning("상호작용에 사용할 장비 ID가 없습니다.", this);

            return false;
        }

        equipmentData = DataManager.Inst.GetData<EquipmentData>(InteractionEquipmentId);

        if (equipmentData == null)
        {
            Debug.LogWarning($"장비 데이터를 확인해 주세요: {InteractionEquipmentId}", this);

            return false;
        }

        return true;
    }

    protected virtual void PlayContent(/*장비 타입을 검사할 수 있는 장비 ID(string 타입) 매개변수 추가*/) { }
    public virtual void CloseContent() { }
    public virtual void UpdateInteraction(float deltaTime) { }
    public virtual void CancelInteraction() { }
    public virtual void ConfirmInteraction() { }

    // 상호작용 종료 요청 이벤트를 호출 메서드 추가
    protected void RequestInteractionEnd()
    {
        InteractionEndRequested?.Invoke();
    }
}
