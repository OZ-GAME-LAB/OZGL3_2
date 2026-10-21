using Cysharp.Threading.Tasks;
using System;
using Unity.Netcode;
using UnityEngine;

public enum MiniContentsType
{
    None = 0,
    Gathering,
    Fisihing,
    Minining,
    Felling
}

public abstract class InteractableObject : MonoBehaviour
{
    private InteractableInfoUI _infoUI;
    protected UIBase _openUI;
    private InteractionNetwork _interactionNetwork;
    public abstract InteractionVMBase ViewModel { get; }
    
    public event Action InteractionEndRequested; //상호작용 종료 이벤트 추가
    public virtual bool RequiresServerReservation => true; //점유가 필요한 상호작용인지 여부를 나타내는 프로퍼티

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

    public void PlayerInteract(/*장비 타입을 검사할 수 있는 장비 ID(string 타입) 매개변수 추가*/)
    {
        PlayContent(/*장비 타입을 검사할 수 있는 장비 ID(string 타입) 매개변수 추가*/);
    }

    private void HandleAvailabilityChanged(bool isAvailable)
    {
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

    protected virtual void PlayContent(/*장비 타입을 검사할 수 있는 장비 ID(string 타입) 매개변수 추가*/) { }
    public virtual void CloseContent() { }
    public virtual void BeginInteraction() { }
    public virtual void UpdateInteraction(float deltaTime) { }
    public virtual void CancelInteraction() { }
    public virtual void BeginInteraction(Transform[] playerUIAnchor, Camera playerCamera) { }

    // 상호작용 종료 요청 이벤트를 호출 메서드 추가
    protected void RequestInteractionEnd()
    {
        InteractionEndRequested?.Invoke();
    }
}
