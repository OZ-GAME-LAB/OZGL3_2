using System;
using System.Collections.Generic;
using UnityEngine;


// 플레이어에 들어갈 컴포넌트로 초안
// 나중에 좀 더 수정 후 플레이어에 추가 예정
public class PlayerInteractionDetector : MonoBehaviour
{
    [Tooltip("거리 계산 기준. 비어 있으면 이 컴포넌트의 위치 사용")]
    [SerializeField] private Transform _distanceOrigin;

    // Collider별로 대상을 기록해서 중복 Collider를 처리한다.
    private readonly Dictionary<Collider, InteractableObject> _contacts = new();
    private readonly HashSet<InteractableObject> _candidates = new();
    private readonly List<Collider> _removeBuffer = new();

    private bool _isLocalPlayer;
    private InteractableObject _selectedTarget;
    private InteractableObject _lockedTarget;

    public InteractableObject SelectedTarget => _selectedTarget;

    // 새 대상이 없으면 null 전달. UI는 이 이벤트에서 대상을 변경한다.
    public event Action<InteractableObject> SelectionChanged;

    // 진행 중이던 대상이 범위에서 나가거나 사라졌을 때 발생한다.
    public event Action<InteractableObject> LockedTargetLost;

    // 플레이어 담당자가 네트워크 스폰 이후 본인 여부를 전달한다.
    public void SetLocalPlayer(bool isLocalPlayer)
    {
        _isLocalPlayer = isLocalPlayer;

        if (!isLocalPlayer)
            ClearDetection();
    }

    private void OnTriggerEnter(Collider other)
    {
        RegisterContact(other);
    }

    // 활성화 전에 이미 겹쳐 있던 Collider도 등록할 수 있도록 한다.
    private void OnTriggerStay(Collider other)
    {
        RegisterContact(other);
    }

    private void RegisterContact(Collider other)
    {
        if (!_isLocalPlayer || _contacts.ContainsKey(other))
            return;

        InteractableObject target =
            other.GetComponentInParent<InteractableObject>();

        if (target == null)
            return;

        _contacts.Add(other, target);
    }

    private void OnTriggerExit(Collider other)
    {
        _contacts.Remove(other);
    }

    private void Update()
    {
        if (!_isLocalPlayer)
            return;

        RefreshCandidates();

        // 상호작용 중에는 다른 대상이 가까워져도 선택을 유지한다.
        if (!ReferenceEquals(_lockedTarget, null))
        {
            if (_lockedTarget != null &&
                _candidates.Contains(_lockedTarget))
            {
                SetSelectedTarget(_lockedTarget);
                return;
            }

            InteractableObject lostTarget = _lockedTarget;
            _lockedTarget = null;

            LockedTargetLost?.Invoke(lostTarget);
        }

        SelectNearestTarget();
    }

    private void RefreshCandidates()
    {
        _candidates.Clear();
        _removeBuffer.Clear();

        foreach (var pair in _contacts)
        {
            Collider contact = pair.Key;
            InteractableObject target = pair.Value;

            if (contact == null ||
                !contact.enabled ||
                !contact.gameObject.activeInHierarchy ||
                target == null ||
                !target.isActiveAndEnabled)
            {
                _removeBuffer.Add(contact);
                continue;
            }

            _candidates.Add(target);
        }

        foreach (Collider contact in _removeBuffer)
            _contacts.Remove(contact);
    }

    private void SelectNearestTarget()
    {
        Vector3 origin = _distanceOrigin != null
            ? _distanceOrigin.position
            : transform.position;

        // 같은 거리면 기존 선택을 유지한다.
        InteractableObject nearest =
            _selectedTarget != null &&
            _candidates.Contains(_selectedTarget)
                ? _selectedTarget
                : null;

        float nearestDistance = nearest != null
            ? (nearest.transform.position - origin).sqrMagnitude
            : float.PositiveInfinity;

        foreach (InteractableObject candidate in _candidates)
        {
            float distance =
                (candidate.transform.position - origin).sqrMagnitude;

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = candidate;
            }
        }

        SetSelectedTarget(nearest);
    }

    // F를 누르고 시작 요청을 보낼 때 선택 대상을 고정한다.
    public bool TryLockSelectedTarget(out InteractableObject target)
    {
        target = null;

        if (!_isLocalPlayer ||
            !ReferenceEquals(_lockedTarget, null) ||
            _selectedTarget == null ||
            !_selectedTarget.isActiveAndEnabled ||
            !_candidates.Contains(_selectedTarget))
        {
            return false;
        }

        _lockedTarget = _selectedTarget;
        target = _lockedTarget;
        return true;
    }

    // 완료, 취소, 서버 거절 이후 호출한다.
    public void ReleaseTarget()
    {
        _lockedTarget = null;
    }

    private void SetSelectedTarget(InteractableObject target)
    {
        if (ReferenceEquals(_selectedTarget, target))
            return;

        _selectedTarget = target;
        SelectionChanged?.Invoke(target);
    }

    private void ClearDetection()
    {
        InteractableObject lostTarget = _lockedTarget;
        bool hadLockedTarget =
            !ReferenceEquals(lostTarget, null);

        _lockedTarget = null;
        _contacts.Clear();
        _candidates.Clear();
        _removeBuffer.Clear();

        if (hadLockedTarget)
            LockedTargetLost?.Invoke(lostTarget);

        SetSelectedTarget(null);
    }

    private void OnDisable()
    {
        ClearDetection();
    }
}
