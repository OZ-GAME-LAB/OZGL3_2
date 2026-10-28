public class PlayerService
{
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

}
