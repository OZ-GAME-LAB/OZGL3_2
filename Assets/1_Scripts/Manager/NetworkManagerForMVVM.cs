using UnityEngine;

public class NetworkManager : MonoBehaviour
{
    public InventoryService _inventoryService { get; private set; }

    public static NetworkManager Inst { get; private set; }


    private void Awake()
    {
        InitService();
    }
    private void InitService()
    {
        //서비스를 등록하려면 여기에 추가!
        _inventoryService = new InventoryService();
        _inventoryService.InitInventoryList();
    }

}
