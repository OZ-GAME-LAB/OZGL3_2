public class PlayerService
{
    private PlayerStatViewModel _playerStatViewModel;


    public void AddHp(int value)
    {
            _playerStatViewModel.Hp += value;
    }

    public void AddMoveSpeed(int value)
    {
        _playerStatViewModel.MoveSpeed += value;
    }

    public void AddJumpForce(int value)
    {
        _playerStatViewModel.JumpForce += value;
    }


    public void BindPlayerControllerView(PlayerControllerView playerControllerView)
    {
        if (playerControllerView == null)
        {
            return;
        }

        GetPlayerStatViewModel();
        playerControllerView.Bind(_playerStatViewModel);
    }


    private PlayerStatViewModel GetPlayerStatViewModel()
    {
        if (_playerStatViewModel == null)
        {
            _playerStatViewModel = CreatePlayerStatViewModel();
        }

        return _playerStatViewModel;
       
    }
    private PlayerStatViewModel CreatePlayerStatViewModel()
    {
        
        var playerStatViewModel = new PlayerStatViewModel
        {
            Hp = 100,
            MoveSpeed = 12,
            JumpForce = 12
            //임시로 넣은 플레이어 스탯 데이터 나중에 데이터화해야 한다면 수정
        };
        return playerStatViewModel;
    }

}
