using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class BossEntryUI : UIBase
{
    [SerializeField] private Button _enterButton;
    [SerializeField] private Button _prepareButton;
    public event Action<bool> Chosen;
    private bool _submitted;
    private TMP_Text _enterLabel;
    private string _enterText;
    private GameObject _previousSelection;
    private void Awake()
    {
        _enterLabel = _enterButton.GetComponentInChildren<TMP_Text>(true);
        if (_enterLabel != null) _enterText = _enterLabel.text;
        SetNavigation(_enterButton, _prepareButton);
        SetNavigation(_prepareButton, _enterButton);
        _enterButton.onClick.AddListener(Enter);
        _prepareButton.onClick.AddListener(Prepare);
    }
    private void OnEnable()
    {
        _submitted = false;
        _enterButton.interactable = true;
        _prepareButton.interactable = true;
        if (_enterLabel != null) _enterLabel.text = _enterText;
        if (EventSystem.current != null)
        {
            _previousSelection = EventSystem.current.currentSelectedGameObject;
            EventSystem.current.SetSelectedGameObject(_enterButton.gameObject);
        }
    }
    private static void SetNavigation(Button button, Button other)
    {
        button.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnUp = other, selectOnDown = other,
            selectOnLeft = other, selectOnRight = other
        };
    }
    private void OnDisable()
    {
        EventSystem events = EventSystem.current;
        if (events != null && (events.currentSelectedGameObject == null ||
            events.currentSelectedGameObject == gameObject ||
            events.currentSelectedGameObject == _enterButton.gameObject ||
            events.currentSelectedGameObject == _prepareButton.gameObject))
        {
            GameObject previous = _previousSelection;
            if (previous != null && (!previous.activeInHierarchy ||
                (previous.TryGetComponent(out Selectable selectable) &&
                    (!selectable.isActiveAndEnabled || !selectable.IsInteractable())))) previous = null;
            events.SetSelectedGameObject(previous);
        }
        _previousSelection = null;
    }
    private void Enter() { Submit(true); }
    private void Prepare() { Submit(false); }
    private void Submit(bool enter)
    {
        if (_submitted) return;
        _submitted = true;
        _enterButton.interactable = false;
        _prepareButton.interactable = false;
        if (enter && _enterLabel != null) _enterLabel.text = "응답 대기 중...";
        // 버튼 없는 자체 루트가 입력을 받도록 하여 대기 중 아래 UI로 탐색하지 않습니다.
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
        Chosen?.Invoke(enter);
    }
}
