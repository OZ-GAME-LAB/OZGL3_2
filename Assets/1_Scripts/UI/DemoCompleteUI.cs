using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class DemoCompleteUI : UIBase
{
    public const string CompletionMessage = "축하합니다! 당신은 숲의 독기를 없애고 다음 지역으로 나아갈 수 있게 되었습니다. 데모 버전은 여기까지입니다. 다음 지역을 기대해주세요.";
    [Header("데모 완료 안내")]
    [SerializeField] private Button _confirmButton;
    [SerializeField] private TMP_Text _messageText;
    public event Action Dismissed;

    private void Awake() { _confirmButton.onClick.AddListener(OnConfirm); }
    private void OnEnable() { _messageText.text = CompletionMessage; }
    private void OnDisable() { Dismissed?.Invoke(); }
    private void OnDestroy() { _confirmButton.onClick.RemoveListener(OnConfirm); }
    private void OnConfirm() { Dismissed?.Invoke(); }

    public void FocusConfirmation()
    {
        transform.SetAsLastSibling();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_confirmButton.gameObject);
    }
}
