using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SimplePopupUI : UIBase
{
    [SerializeField] TMP_Text Text_Msg;
    private CancellationTokenSource _cancelToken;

    private void OnEnable()
    {
        CloseSelfAsync().Forget();
        UIWaitUntilNextFrame().Forget();
    }

    private void OnDisable()
    {
        if (_cancelToken != null)
        {
            _cancelToken.Cancel();
            _cancelToken.Dispose();
            _cancelToken = null;
        }
    }

    private async UniTask UIWaitUntilNextFrame()
    {
        // 호출 후 1프레임 지난 후 어떤 작업을 해야될 때 이 로직을 사용하세요!
        await UniTask.NextFrame();
    }


    private async UniTaskVoid CloseSelfAsync()
    {
        if (_cancelToken != null)
        {
            _cancelToken.Cancel(); // 취소
            _cancelToken.Dispose(); // 자원해제
        }

        _cancelToken = new CancellationTokenSource();
        Debug.Log("코루틴처럼 대기 상태에서 해야하는 일이 있다면 여기서 먼저 진행");

        // 비활성화가 아니라 파괴형태로 사라진다면 ->>> this.GetCancellationTokenOnDestroy();
        await UniTask.Delay(TimeSpan.FromSeconds(1.5), cancellationToken: _cancelToken.Token);

        UIManager.Inst.Close(UIType.SimplePopup);
    }

    public void SetUI(string msg)
    {
        Text_Msg.text = msg;
        CheckAndChangeColor(msg);
    }

    private void CheckAndChangeColor(string msg)
    {
        if (msg.Contains("출력"))
        {
            Text_Msg.color = Color.red;
        }
    }
}
