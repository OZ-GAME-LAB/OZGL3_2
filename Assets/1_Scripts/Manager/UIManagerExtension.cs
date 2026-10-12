using Cysharp.Threading.Tasks;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;



public enum UIRootType
{
    None = 0,
    Main,
    Content,
    Popup,
    VeryFront
}

public enum UIType
{
    //DNSimplePopup,
    MainUI,
}


public static class UIManagerExtension
{

    public static async UniTask OpenMainUIAsync(this UIManager uiManager, CancellationToken cancellationToken = default)
    {
        await uiManager.OpenMainRootAsync(UIType.MainUI, cancellationToken);
    }

    public static void CloseBuildUI(this UIManager uiManager)
    {
        uiManager.Close(UIType.MainUI);
    }



    //UI를 생성하고 View를 가져오고 싶을 때 사용
    private static T GetView<T>(UIBase baseUI, UIType uiType) where T : UIBase
    {
        if (baseUI == null)
        {
            return null;
        }

        if (baseUI is not T view)
        {
            return null;
        }

        return view;
    }

    public static string GetUIKey(this UIManager uiManager, UIType uIType)
    {
        string key = $"UI/{uIType}";
        return key;
    }
}

