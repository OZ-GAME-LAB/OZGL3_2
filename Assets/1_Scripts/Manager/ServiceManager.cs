using System.Collections;
using UnityEngine;

public class ServiceManager : MonoBehaviour
{
    public static ServiceManager Inst { get; private set; }

    //인벤토리 서비스는 프로퍼티라서 대문자로 시작하게 수정했습니다.
    public InventoryService InventoryService { get; private set; }
    public NetworkCraftingService CraftingService { get; private set; }
    public PlayerService PlayerService { get; private set; }
    public NetworkRuneService NetworkRuneService { get; private set; }
    public ScenarioService ScenarioService { get; private set; }
    private DialoguePresenter _dialoguePresenter;

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
        NetworkRuneService = new NetworkRuneService(InventoryService);
        ScenarioService = new ScenarioService();
        _dialoguePresenter = new DialoguePresenter(ScenarioService);
    }

    private void Update()
    {
        ScenarioService?.Tick(Time.unscaledDeltaTime);
    }

    private void OnDestroy()
    {
        NetworkRuneService?.Dispose();
        _dialoguePresenter?.Dispose();
        ScenarioService?.Dispose();
        if (Inst == this)
            Inst = null;
    }

    public void InitInventory()
    {
        //ServiceManager.Inst.InventoryService.InitTestInventoryItem();
        InventoryService.InitializeEmptyInventory();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void GiveInventoryTestItems()
    {
        ServiceManager.Inst.InventoryService.InitTestInventoryItem();
        //InventoryService.InitializeEmptyInventory();
    }


#endif
}
