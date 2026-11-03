using Cysharp.Threading.Tasks;
using UnityEngine;

public class RuneInventoryUITest : MonoBehaviour
{
    private bool _opening;

    [ContextMenu("테스트/내 룬 UI 열기")]
    private void Open()
    {
        OpenAsync().Forget();
    }

    private async UniTask OpenAsync()
    {
        if (!Application.isPlaying || _opening)
            return;

        if (UIManager.Inst == null || ServiceManager.Inst == null)
            return;

        var manager = Unity.Netcode.NetworkManager.Singleton;

        if (manager == null ||
            !manager.IsClient ||
            manager.LocalClient == null ||
            manager.LocalClient.PlayerObject == null)
        {
            Debug.LogWarning("로컬 플레이어가 생성된 뒤 열어 주세요.");
            return;
        }

        _opening = true;

        try
        {
            UIManager uiManager = UIManager.Inst;
            ServiceManager services = ServiceManager.Inst;

            RuneInventoryUI view =
                await uiManager.OpenRuneInventoryUIAsync();

            if (uiManager == null || view == null)
                return;

            // 로딩 중 연결이 종료되거나 플레이어가 사라졌을 수 있음.
            if (services == null || services.NetworkRuneService.BindRuneInventoryView(view) == false)
            {
                uiManager.CloseRuneInventoryUI();
            }
        }
        finally
        {
            _opening = false;
        }
    }
}