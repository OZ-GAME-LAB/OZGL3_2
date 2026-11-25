using System;
using UnityEngine;
using UnityEngine.UI;

public class ControlsHelpUI : UIBase
{
    [SerializeField] private Button Button_Close;

    public event Action Closed;

    private void Awake()
    {
        Button_Close.onClick.AddListener(OnClickClose);
    }

    private void OnEnable()
    {
        Button_Close.Select();
    }

    private void OnClickClose()
    {
        UIManager.Inst.Close(UIType.ControlsHelpUI);
    }

    private void OnDisable()
    {
        Closed?.Invoke();
    }

    private void OnDestroy()
    {
        Button_Close.onClick.RemoveListener(OnClickClose);
    }
}
