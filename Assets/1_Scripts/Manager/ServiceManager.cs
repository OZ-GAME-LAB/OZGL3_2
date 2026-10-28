using System.Collections;
using UnityEngine;

public class ServiceManager : MonoBehaviour
{
    public static ServiceManager Inst { get; private set; }

    //인벤토리 서비스는 프로퍼티라서 대문자로 시작하게 수정했습니다.
    public InventoryService InventoryService { get; private set; }
    public NetworkCraftingService CraftingService { get; private set; }
    public PlayerService PlayerService { get; private set; }

    private void Awake()
    {
        Inst = this;
        InitNetworkService();
    }

    //추후 추가되는 서비스들은 상단에 public NetworkXXXService XXXService { get; private set; } 를 추가하고 아래 메서드에도 추가하면 됩니다.
    private void InitNetworkService()
    {
        CraftingService = new NetworkCraftingService();
        InventoryService = new InventoryService();
        PlayerService = new PlayerService();
    }

    public void InitInventory()
    {
        InventoryService.InitInventoryList();
    }


}
