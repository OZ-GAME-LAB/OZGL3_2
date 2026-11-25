using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class PoisonFogWarningZone : MonoBehaviour
{
    [Header("진입 안내")]
    [SerializeField] private string _message = "강렬한 독기가 길을 막고 있다";

    private readonly HashSet<Collider2D> _contacts = new();

    private void OnTriggerEnter2D(Collider2D other) => RegisterContact(other);

    private void OnTriggerStay2D(Collider2D other)
    {
        // 스폰 준비 중에 이미 안개에 들어와 있던 로컬 플레이어도 안내한다.
        if (!_contacts.Contains(other))
            RegisterContact(other);
    }

    private void RegisterContact(Collider2D other)
    {
        if (!isActiveAndEnabled || other.isTrigger || other.gameObject.layer != LayerMask.NameToLayer("Player"))
            return;

        var body = other.attachedRigidbody;
        if (body == null || !body.TryGetComponent(out NetworkObject player) ||
            !player.IsSpawned || !player.IsPlayerObject || !player.IsOwner || UIManager.Inst == null)
            return;

        _contacts.RemoveWhere(IsInactive);
        if (_contacts.Add(other) && _contacts.Count == 1)
            UIManager.Inst.OpenSimplePopup(_message).Forget();
    }

    private static bool IsInactive(Collider2D contact) =>
        contact == null || !contact.enabled || !contact.gameObject.activeInHierarchy;

    private void OnTriggerExit2D(Collider2D other) => _contacts.Remove(other);

    private void OnDisable() => _contacts.Clear();
}
