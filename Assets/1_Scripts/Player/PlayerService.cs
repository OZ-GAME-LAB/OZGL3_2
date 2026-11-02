using UnityEngine;

public class PlayerService
{
    // 서버에서 호출해야 실제로 적용됨.
    public bool AddHp(PlayerNetworkStats target, float value)
    {
        return target != null && target.HealOnServer(value);
    }

    public bool DecreaseHp(PlayerNetworkStats target, float value)
    {
        if (target == null)
        {
            return false;
        }

        return target.TryTakeDamageOnServer(value, out _);
    }

    public bool AddMoveSpeed(PlayerNetworkStats target, float value)
    {
        return target != null && target.AddMoveSpeedOnServer(value);
    }

    public bool AddJumpForce(PlayerNetworkStats target, float value)
    {
        return target != null && target.AddJumpForceOnServer(value);
    }

    public void BindPlayerControllerView(PlayerControllerView view)
    {
        if (view == null)
        {
            return;
        }

        PlayerNetworkStats stats = view.GetComponent<PlayerNetworkStats>();

        if (stats == null)
        {
            Debug.LogError("PlayerNetworkStats가 없습니다.", view);
            return;
        }

        view.Bind(stats.ViewModel);
    }

    /*
     * 서비스가 VM을 직접 수정하면 네트워크 스탯과 표시 값이 달라지므로 VM을 제거했습니다.
    private PlayerStatViewModel _playerStatViewModel;


    public void AddHp(float value)
    {
            _playerStatViewModel.Hp += value;
    }

    public void DecreaseHp(float value)
    {
        _playerStatViewModel.Hp -= value;
    }

    public void AddMoveSpeed(float value)
    {
        _playerStatViewModel.MoveSpeed += value;
    }

    public void AddJumpForce(float value)
    {
        _playerStatViewModel.JumpForce += value;
    }


    public void BindPlayerControllerView(PlayerControllerView playerControllerView)
    {
        GetPlayerControllerView();
        playerControllerView.Bind(_playerStatViewModel);
    }


    private PlayerStatViewModel GetPlayerControllerView()
    {
        if (_playerStatViewModel == null)
        {
            _playerStatViewModel = CreatePlayerControllerView();
        }

        return _playerStatViewModel;
       
    }
    private PlayerStatViewModel CreatePlayerControllerView()
    {
        
        var playerStatViewModel = new PlayerStatViewModel
        {
            Hp = 100,
            MoveSpeed = 12,
            JumpForce = 17.5f
            //임시로 넣은 플레이어 스탯 데이터 나중에 데이터화해야 한다면 수정
        };
        return playerStatViewModel;
    }
    */
}
