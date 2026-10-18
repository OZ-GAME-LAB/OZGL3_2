using System.Collections;
using UnityEngine;

public class NetworkManager : MonoBehaviour
{
    public InventoryService _inventoryService { get; private set; }

    public static NetworkManager Inst { get; private set; }

    public static NetworkManager Inst { get; private set; }

    public NetworkCraftingService CraftingService { get; private set; }

    private void Awake()
    {
        Inst = this;
        InitNetworkService();
    }

    //추후 추가되는 서비스들은 상단에 public NetworkXXXService XXXService { get; private set; } 를 추가하고 아래 메서드에도 추가하면 됩니다.
    private void InitNetworkService()
    {
        CraftingService = new NetworkCraftingService();
        _inventoryService = new InventoryService();
        _inventoryService.InitInventoryList();
    }


}
