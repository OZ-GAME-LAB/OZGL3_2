using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class HostDisconnectedUI : UIBase
{
    [SerializeField] private TMP_Text Text_Message;
    [SerializeField] private Button Button_Confirm;

    private Action _onConfirm;

    private void Awake()
    {
        Button_Confirm.onClick.AddListener(OnConfirm);
    }

    private void OnDestroy()
    {
        Button_Confirm.onClick.RemoveListener(OnConfirm);
        _onConfirm = null;
    }

    public void Show(Action onConfirm)
    {
        _onConfirm = onConfirm;
        Text_Message.text = "호스트와 연결이 끊겼습니다.";
        SetReturning(false);
    }

    public void SetReturning(bool returning)
    {
        Button_Confirm.interactable = !returning;
        if (returning)
            Text_Message.text = "로비로 이동 중입니다.";
        else if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(Button_Confirm.gameObject);
    }

    public void ShowReturnError()
    {
        Text_Message.text = "로비로 돌아가지 못했습니다. 확인을 눌러 다시 시도해 주세요.";
        SetReturning(false);
    }

    private void OnConfirm()
    {
        _onConfirm?.Invoke();
    }
}
