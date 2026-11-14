using Cysharp.Threading.Tasks;
using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;

public abstract class InteractableObject : MonoBehaviour
{
    private InteractableInfoUI _infoUI;
    protected UIBase _openUI;
    private InteractionNetwork _interactionNetwork;
    public abstract InteractionVMBase ViewModel { get; }

    public event Action<float> Completed;

    public event Action InteractionEndRequested; 

    private bool _destroyAfterUse = true;
    public bool DestroyAfterUse => _destroyAfterUse;
    public event Action RespawnRequested;
    private GameObject _visualObject;
    private bool _isDormant;

    public virtual bool UsesMiniGameInput => false;

    public virtual bool RequiresServerReservation => true;

    public string RequiredToolAction { get; private set; }

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

    private void HandleAvailabilityChanged(bool isAvailable)
    {
        ViewModel.IsUsedByOtherPlayer = !isAvailable && RequiresServerReservation
            && _interactionNetwork != null && _interactionNetwork.IsUsedByOtherPlayer;
        ViewModel.IsAvailable = isAvailable;
    }

    public virtual void ShowInteractionInfo(Transform[] playerUIAnchors, Camera playerCamera)
    {
        OpenInteractionInfoAsync(playerUIAnchors, playerCamera).Forget();
    }

    private async UniTask OpenInteractionInfoAsync(Transform[] playerUIAnchors, Camera playerCamera)
    {
        UIBase ui = await UIManager.Inst.OpenMainRootAsync(UIType.InteractableInfoUI);

        if (ui == null)
            return;

        if (ui is not InteractableInfoUI infoUI)
        {
            Debug.LogError("프리팹에 InteractableInfoUI 컴포넌트가 필요합니다.");
            return;
        }

        _infoUI = infoUI;
        _infoUI.Bind(ViewModel);

        if (_infoUI.TryGetComponent(out InteractionUIPositioner positioner))
        {
            positioner.SetTargets(playerUIAnchors, playerCamera);
        }
        else
        {
            Debug.LogError("안내 UI에 InteractionUIPositioner가 필요합니다.");
        }
    }

    public virtual void HideInteractionInfo()
    {
        if (_infoUI == null)
            return;

        UIManager.Inst.Close(UIType.InteractableInfoUI);
        _infoUI = null;
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
        InteractionEquipmentId = equipmentId;
    }

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
