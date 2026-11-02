using Unity.Netcode;
using UnityEngine;

public class NetCodeCliendtSideService
{
    public ulong CurrentClientId { get; private set; }

    public void InitClientService()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
        }
    }

    public void EndClientService()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    private void OnClientDisconnect(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            string reason = NetworkManager.Singleton.DisconnectReason;

            if (string.IsNullOrEmpty(reason) == false)
            {
                UIManager.Inst.OpenSimplePopup($"서버 연결에 실패했습니다 : 실패 사유: " + reason);
            }
            else
            {
                UIManager.Inst.OpenSimplePopup($"서버 연결에 실패했습니다 : 서버 응답 없음 또는 네트워크 시간 초과(Timeout)");
            }

            // TODO: 타이틀 화면으로 돌아가기, 실패팝업 띄우기 등 처리
        }
        else
        {
            // 2. 다른 플레이어가 나갔을 때
            UIManager.Inst.OpenSimplePopup($"다른 클라이언트가 접속 해제했습니다! {clientId}");
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        // NetworkManager.Singleton.LocalClientId는 내 자신의 클라이언트 ID
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("서버 접속 성공! 내 클라이언트 ID: " + clientId);
            // TODO: 로비 화면 끄기, 게임 플레이 UI 켜기 등 후속 작업 처리
            UIManager.Inst.CloseNetworkUI();
            GameManager.Inst.StartGame();
        }
        else
        {
            UIManager.Inst.OpenSimplePopup($"다른 클라이언트가 접속했습니다! {clientId}");
        }
    }
}
