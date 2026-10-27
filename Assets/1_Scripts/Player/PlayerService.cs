public class PlayerService
{
    private PlayerStatViewModel _platerStatViewModel;




    public void AddHp(int value)
    {
            _platerStatViewModel.Hp += value;
    }

    public void AddMoveSpeed(int value)
    {
        _platerStatViewModel.MoveSpeed += value;
    }

    public void AddJumpForce(int value)
    {
        _platerStatViewModel.JumpForce += value;
    }


    public void BindPlayerControllerView(PlayerControllerView playerControllerView)
    {
        GetPlayerControllerView();
        playerControllerView.Bind(_platerStatViewModel);
    }


    private void GetPlayerControllerView()
    {
        if (_platerStatViewModel == null)
        {
            _platerStatViewModel = CreatePlayerControllerView();
        }
       
    }
    private PlayerStatViewModel CreatePlayerControllerView()
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
