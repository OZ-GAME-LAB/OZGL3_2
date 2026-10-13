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
    protected NetworkVariable<bool> _isInteracting = new NetworkVariable<bool>
    (true,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Owner);
    protected UIBase _openUI;

    public void PlayerInteract(/*장비 타입을 검사할 수 있는 장비 ID(string 타입) 매개변수 추가*/)
    {
        PlayContent(/*장비 타입을 검사할 수 있는 장비 ID(string 타입) 매개변수 추가*/);
    }

    protected virtual void PlayContent(/*장비 타입을 검사할 수 있는 장비 ID(string 타입) 매개변수 추가*/)
    {
        
    }

    public void CloseContent()
    {

    }
}
