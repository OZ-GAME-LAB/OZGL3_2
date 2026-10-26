using System.Collections.Generic;
using UnityEngine;

/// <summary>플레이어 진입 시 바라보는 방향에 맞는 장식 애니메이션을 한 번 재생합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class DirectionalAnimationReaction : MonoBehaviour
{
    [Header("애니메이션 연결")]
    [SerializeField] private Animator _animator;
    [SerializeField] private string _leftStateName = "Base Layer.ReactLeft";
    [SerializeField] private string _rightStateName = "Base Layer.ReactRight";

    private readonly Dictionary<Collider2D, PlayerControllerView> _contacts =
        new Dictionary<Collider2D, PlayerControllerView>();
    private readonly List<Collider2D> _expiredContacts = new List<Collider2D>();
    private int _leftStateHash;
    private int _rightStateHash;

    private void Awake()
    {
        if (_animator == null && !TryGetComponent(out _animator))
        {
            Debug.LogWarning("방향 반응에 사용할 Animator를 연결해 주세요.", this);
            enabled = false;
            return;
        }

        if (!TryGetComponent(out Collider2D trigger) || !trigger.isTrigger)
        {
            Debug.LogWarning("방향 반응에는 Is Trigger가 켜진 Collider2D가 필요합니다.", this);
            enabled = false;
            return;
        }

        _leftStateHash = Animator.StringToHash(_leftStateName);
        _rightStateHash = Animator.StringToHash(_rightStateName);
    }

    private void Start()
    {
        if (_animator.runtimeAnimatorController == null ||
            !_animator.HasState(0, _leftStateHash) || !_animator.HasState(0, _rightStateHash))
        {
            Debug.LogWarning("Animator의 Base Layer에 지정된 좌우 반응 상태가 있는지 확인해 주세요.", this);
            enabled = false;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Unity는 비활성화된 MonoBehaviour에도 Trigger 메시지를 보낼 수 있습니다.
        if (!isActiveAndEnabled || _contacts.ContainsKey(other))
            return;

        PlayerControllerView player = other.GetComponentInParent<PlayerControllerView>();
        if (player == null)
            return;

        RemoveExpiredContacts();
        bool alreadyInside = _contacts.ContainsValue(player);
        _contacts.Add(other, player);
        if (alreadyInside)
            return;

        // 현재 프로젝트의 Flip()은 플레이어 루트의 localScale.x 부호를 바꿉니다.
        bool facesLeft = player.transform.localScale.x < 0f;
        int stateHash = facesLeft ? _leftStateHash : _rightStateHash;
        _animator.Play(stateHash, 0, 0f);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        _contacts.Remove(other);
    }

    private void FixedUpdate()
    {
        RemoveExpiredContacts();
    }

    private void RemoveExpiredContacts()
    {
        // 파괴/비활성화로 Exit가 오지 않는 접촉도 정리합니다.
        _expiredContacts.Clear();
        foreach (KeyValuePair<Collider2D, PlayerControllerView> contact in _contacts)
        {
            Collider2D collider = contact.Key;
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy ||
                contact.Value == null ||
                (collider.attachedRigidbody != null && !collider.attachedRigidbody.simulated))
            {
                _expiredContacts.Add(collider);
            }
        }

        foreach (Collider2D collider in _expiredContacts)
            _contacts.Remove(collider);
    }

    private void OnDisable()
    {
        _contacts.Clear();
        _expiredContacts.Clear();
    }
}
