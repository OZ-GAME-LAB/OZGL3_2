using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class RestUI : UIBase
{
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
        if (EventSystem.current != null) _previousSelection = EventSystem.current.currentSelectedGameObject;
    }

    public void SetState(int accepted, int total, bool canCancel)
    {
        _messageText.text = $"모든 플레이어가 휴식해야 합니다 {accepted}/{total}";
        _cancelButton.interactable = canCancel;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(canCancel ? _cancelButton.gameObject : gameObject);
    }

    private void OnCancel()
    {
        if (_cancelButton.interactable) CancelRequested?.Invoke();
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
