using UnityEngine;
using UnityEngine.UI;

public class SettingUI : UIBase
{
    [SerializeField] private Button Button_Close;

    private void Awake()
    {
        Button_Close.onClick.AddListener(OnClose);
    }

    private void OnDestroy()
    {
        if (Button_Close != null)
            Button_Close.onClick.RemoveListener(OnClose);
    }

    private void OnClose()
    {
        if (UIManager.Inst != null)
        {
            UIManager.Inst.CloseSettingUI();
        }
    }
}
