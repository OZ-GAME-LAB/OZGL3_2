癤using Cysharp.Threading.Tasks;
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
    
    public event Action InteractionEndRequested; //몄 醫猷 대깽 異媛
    public virtual bool RequiresServerReservation => true; //�媛  몄⑹몄 щ瑜 대 濡쇳

    public event Action Completed;

    protected virtual void Awake()
    {
        _interactionNetwork = GetComponent<InteractionNetwork>();

        if (RequiresServerReservation && _interactionNetwork == null)
        {
            Debug.LogError($"{name}: InteractionNetwork 而댄щ멸 ⑸.", this);
        }
    }

    protected virtual void OnEnable()
    {
        if (RequiresServerReservation == false)
        {
            // 吏湲 �  쇰㈃ 濡而 UI瑜   
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

    public abstract UniTask InitObject(string id);

    public void PlayerInteract(/*장비 타입을 검사할 수 있는 장비 ID(string 타입) 매개변수 추가*/)
    {
        PlayContent(/*λ  寃ы   λ ID(string ) 留ㅺ蹂 異媛*/);
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
            Debug.LogError("由ы뱀 InteractableInfoUI 而댄щ멸 ⑸.");
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
            Debug.LogError(" UI InteractionUIPositioner媛 ⑸.");
        }
    }

    public virtual void HideInteractionInfo()
    {
        if (_infoUI == null)
            return;

        UIManager.Inst.Close(UIType.InteractableInfoUI);
        _infoUI = null;
    }

    protected virtual void PlayContent(/*λ  寃ы   λ ID(string ) 留ㅺ蹂 異媛*/) { }
    public virtual void CloseContent() { }
    public virtual void BeginInteraction() { }
    public virtual void UpdateInteraction(float deltaTime) { }
    public virtual void CancelInteraction() { }
    public virtual void BeginInteraction(Transform[] playerUIAnchor, Camera playerCamera) { }

    // 몄 醫猷 泥 대깽몃� 몄 硫 異媛
    protected void RequestInteractionEnd()
    {
        InteractionEndRequested?.Invoke();
    }
}
