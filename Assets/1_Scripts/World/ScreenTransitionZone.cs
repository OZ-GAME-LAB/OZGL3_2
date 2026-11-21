using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class ScreenTransitionZone : MonoBehaviour
{
    [Header("이동 목적지")]
    [Tooltip("도착할 StageEntryPoint의 Entry Point Id를 입력합니다. 비워 두면 작동하지 않습니다.")]
    [SerializeField] private string _destinationEntryPointId;

    private readonly HashSet<Collider2D> _contacts = new HashSet<Collider2D>();
    private readonly List<Collider2D> _lostContacts = new List<Collider2D>();
    private PlayerControllerView _player;

    private void Reset()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void Awake()
    {
        if (GetComponent<BoxCollider2D>().isTrigger) return;
        Debug.LogError("ScreenTransitionZone의 Is Trigger를 켜 주세요.", this);
        enabled = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!TryGetLocalPlayer(other, out PlayerControllerView player)) return;
        RemoveInactiveContacts();
        bool firstContact = _contacts.Count == 0;
        _contacts.Add(other);
        _player = player;
        // 도착 시 미리 등록한 접촉과 추가 신체 콜라이더는 새 진입으로 취급하지 않습니다.
        if (!firstContact) return;
        if (string.IsNullOrWhiteSpace(_destinationEntryPointId))
        {
            Debug.LogWarning("ScreenTransitionZone의 목적지 ID가 비어 있습니다.", this);
            return;
        }
        player.TryStartScreenTransition(this, _destinationEntryPointId);
    }

    public void SuppressArrival(Collider2D playerCollider)
    {
        if (!TryGetLocalPlayer(playerCollider, out PlayerControllerView player)) return;
        RemoveInactiveContacts();
        _contacts.Add(playerCollider);
        _player = player;
    }

    private bool TryGetLocalPlayer(Collider2D other, out PlayerControllerView player)
    {
        player = null;
        if (!isActiveAndEnabled || other == null || other.isTrigger) return false;
        NetworkObject networkPlayer = other.GetComponentInParent<NetworkObject>();
        return networkPlayer != null && networkPlayer.IsSpawned && networkPlayer.IsOwner &&
            networkPlayer.IsPlayerObject && networkPlayer.TryGetComponent(out player);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        _contacts.Remove(other);
        RemoveInactiveContacts();
    }

    private void RemoveInactiveContacts()
    {
        _lostContacts.Clear();
        foreach (Collider2D contact in _contacts)
        {
            if (contact == null || !contact.enabled || !contact.gameObject.activeInHierarchy)
                _lostContacts.Add(contact);
        }
        foreach (Collider2D contact in _lostContacts) _contacts.Remove(contact);
    }

    private void OnDisable()
    {
        if (_player != null) _player.CancelScreenTransition(this);
        _player = null;
        _contacts.Clear();
        _lostContacts.Clear();
    }
}
