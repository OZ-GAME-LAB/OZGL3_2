using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class BossEntryUI : UIBase
{
    [Header("입장 준비")]
    [SerializeField] private TMP_Text _messageText;
    [SerializeField] private Button _cancelButton;
    private GameObject _previousSelection;
    public event Action CancelRequested;

    private void Awake()
    {
        _cancelButton.onClick.AddListener(OnCancel);
        _cancelButton.navigation = new Navigation { mode = Navigation.Mode.None };
    }

    private void OnEnable()
    {
        _cancelButton.interactable = true;
        if (EventSystem.current != null) _previousSelection = EventSystem.current.currentSelectedGameObject;
    }

    public void SetState(int accepted, int total)
    {
        _messageText.text = $"보스룸 진입 준비 {accepted}/{total}\n모든 플레이어가 입구에서 F를 눌러야 합니다.";

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_cancelButton.gameObject);
    }

    private void OnCancel()
    {
        if (!_cancelButton.interactable) return;
        _cancelButton.interactable = false;
        CancelRequested?.Invoke();
    }

    private void OnDisable()
    {
        EventSystem events = EventSystem.current;
        if (events != null && (events.currentSelectedGameObject == null ||
            events.currentSelectedGameObject == gameObject || events.currentSelectedGameObject == _cancelButton.gameObject))
        {
            GameObject previous = _previousSelection;
            if (previous != null && (!previous.activeInHierarchy ||
                (previous.TryGetComponent(out Selectable selectable) && (!selectable.isActiveAndEnabled || !selectable.IsInteractable())))) previous = null;
            events.SetSelectedGameObject(previous);
        }
        _previousSelection = null;
    }
}
