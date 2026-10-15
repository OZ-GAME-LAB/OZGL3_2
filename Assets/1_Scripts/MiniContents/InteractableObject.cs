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

public class InteractableObject : MonoBehaviour
{
    protected UIBase _openUI;

    public void PlayerInteract(/*장비 타입을 검사할 수 있는 장비 ID(string 타입) 매개변수 추가*/)
    {
        PlayContent(/*장비 타입을 검사할 수 있는 장비 ID(string 타입) 매개변수 추가*/);
    }

    protected virtual void PlayContent(/*장비 타입을 검사할 수 있는 장비 ID(string 타입) 매개변수 추가*/)
    {
        
    }

    public virtual void CloseContent() { }
    public virtual void BeginInteraction() { }
    public virtual void UpdateInteraction(float deltaTime) { }
    public virtual void CancelInteraction() { }
    public virtual void BeginInteraction(Transform[] playerUIAnchor, Camera playerCamera) { }
}
