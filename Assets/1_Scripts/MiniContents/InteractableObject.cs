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
    protected UIBase _openUI;
    private InteractionNetwork _interactionNetwork;
    public abstract InteractionVMBase ViewModel { get; }

    protected virtual void Awake()
    {
        _interactionNetwork = GetComponent<InteractionNetwork>();

        if (_interactionNetwork == null)
        {
            Debug.LogError($"{name}: InteractionNetwork 컴포넌트가 필요합니다.", this);
        }
    }

    protected virtual void OnEnable()
    {
        if (_interactionNetwork == null)
            return;

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
    }

    public void PlayerInteract(/*장비 타입을 검사할 수 있는 장비 ID(string 타입) 매개변수 추가*/)
    {
        PlayContent(/*장비 타입을 검사할 수 있는 장비 ID(string 타입) 매개변수 추가*/);
    }

    private void HandleAvailabilityChanged(bool isAvailable)
    {
        ViewModel.IsAvailable = isAvailable;
    }

    protected virtual void PlayContent(/*장비 타입을 검사할 수 있는 장비 ID(string 타입) 매개변수 추가*/) { }
    public virtual void CloseContent() { }
    public virtual void BeginInteraction() { }
    public virtual void UpdateInteraction(float deltaTime) { }
    public virtual void CancelInteraction() { }
    public virtual void BeginInteraction(Transform[] playerUIAnchor, Camera playerCamera) { }
    public virtual void ShowInteractionInfo() { }
    public virtual void HideInteractionInfo() { }
}
