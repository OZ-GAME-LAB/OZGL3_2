using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BossDetector : MonoBehaviour
{
    private BoxCollider2D _areaCollider;

    private readonly HashSet<Collider2D> _contacts = new();

    private bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

    private void Awake()
    {
        _areaCollider = GetComponent<BoxCollider2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        RegisterContact(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        RegisterContact(other);
    }

    private void RegisterContact(Collider2D other)
    {
        if (!isActiveAndEnabled || !IsServer)
            return;

        if (other == null || other.isTrigger)
            return;

        if (!_areaCollider.IsTouching(other))
            return;

        PlayerRuneCombatActor player = other.GetComponentInParent<PlayerRuneCombatActor>();

        if (player == null)
            return;

        _contacts.Add(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsServer)
            return;

        if (other != null && _areaCollider.IsTouching(other))
            return;

        _contacts.Remove(other);
    }

    public void GetAlivePlayers(List<PlayerRuneCombatActor> results)
    {
        results.Clear();

        if (!isActiveAndEnabled || !IsServer)
            return;

        _contacts.RemoveWhere(contact =>
            contact == null || !contact.isActiveAndEnabled || !_areaCollider.IsTouching(contact));

        foreach (Collider2D contact in _contacts)
        {
            PlayerRuneCombatActor player = contact.GetComponentInParent<PlayerRuneCombatActor>();

            if (player == null || !player.isActiveAndEnabled || !player.IsAlive)
                continue;

            if (!results.Contains(player))
            {
                results.Add(player);
            }
        }
    }

    private void OnDisable()
    {
        _contacts.Clear();
    }
}
