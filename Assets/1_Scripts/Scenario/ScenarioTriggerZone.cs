using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D), typeof(ScenarioSource))]
public sealed class ScenarioTriggerZone : MonoBehaviour
{
    private readonly HashSet<Collider2D> _contacts = new HashSet<Collider2D>();
    private readonly List<Collider2D> _lostContacts = new List<Collider2D>();
    private ScenarioSource _source;
    private PlayerControllerView _player;
    private bool _attemptedThisVisit;
    private float _retryTime;

    private void Awake()
    {
        _source = GetComponent<ScenarioSource>();
        if (!GetComponent<Collider2D>().isTrigger)
        {
            Debug.LogError("ScenarioTriggerZone의 Collider2D에서 Is Trigger를 켜 주세요.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        ScenarioManager.AvailabilityChanged += OnManagerChanged;
    }

    private void OnManagerChanged()
    {
        if (ScenarioManager.Inst == null)
        {
            _contacts.Clear();
            _attemptedThisVisit = false;
            _retryTime = 0f;
            _player = null;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryEnter(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // 초기화 전에 범위 안에 있던 플레이어와 다른 대화가 끝난 경우도 처리합니다.
        TryEnter(other);
    }

    private void TryEnter(Collider2D other)
    {
        if (!isActiveAndEnabled)
            return;
        if (!_contacts.Contains(other))
        {
            NetworkObject networkPlayer = other.GetComponentInParent<NetworkObject>();
            if (networkPlayer == null || !networkPlayer.IsSpawned || !networkPlayer.IsOwner || !networkPlayer.IsPlayerObject ||
                !networkPlayer.TryGetComponent(out _player))
                return;
            RemoveInactiveContacts();
            if (_contacts.Count == 0)
            {
                _attemptedThisVisit = false;
                _retryTime = 0f;
            }
            _contacts.Add(other);
        }
        if (_attemptedThisVisit || Time.unscaledTime < _retryTime)
            return;
        _retryTime = Time.unscaledTime + 0.25f;
        if (_player != null && _player.TryStartScenario(_source))
            _attemptedThisVisit = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        _contacts.Remove(other);
        RemoveInactiveContacts();
        if (_contacts.Count == 0)
        {
            // 취소/실패한 사건은 다음 방문 때 재시도합니다. 개인 완료 여부는 서비스가 판정합니다.
            _attemptedThisVisit = false;
            _retryTime = 0f;
            _player = null;
        }
    }

    private void OnDisable()
    {
        ScenarioManager.AvailabilityChanged -= OnManagerChanged;
        _contacts.Clear();
        _attemptedThisVisit = false;
        _retryTime = 0f;
        _player = null;
    }

    private void RemoveInactiveContacts()
    {
        _lostContacts.Clear();
        foreach (Collider2D contact in _contacts)
        {
            if (contact == null || !contact.enabled || !contact.gameObject.activeInHierarchy)
                _lostContacts.Add(contact);
        }
        foreach (Collider2D contact in _lostContacts)
            _contacts.Remove(contact);
    }
}
