using System.Collections.Generic;
using System.ComponentModel;
using TMPro;
using Unity.Android.Gradle.Manifest;
using UnityEngine;
using UnityEngine.UI;

public class CraftingUI : UIBase
{
    [SerializeField] private CraftingSlotUI Prefab_CraftingSlot;
    [SerializeField] private CraftingRequiredItemSlotUI Prefab_RequiredItemSlot;

    [SerializeField] private Transform Transform_CraftingSlotUIRoot;
    [SerializeField] private Transform Transform_RequiredItemSlotUIRoot;

    [SerializeField] private GameObject Layout_RightSide;
    [SerializeField] private TMP_Text Text_ItemName;
    [SerializeField] private TMP_Text Text_ItemDesc;

    [SerializeField] private Button Button_Craft;
    [SerializeField] private Button Button_Close;
    [SerializeField] private Button Button_BackClose;

    private readonly List<CraftingSlotUI> _recipeSlots = new List<CraftingSlotUI>();
    private readonly List<CraftingRequiredItemSlotUI> _requiredItemSlots = new List<CraftingRequiredItemSlotUI>();

    private CraftingViewModel _craftingVM;

    private void Awake()
    {
        Button_Craft.onClick.AddListener(OnClickCraft);
        Button_Close.onClick.AddListener(OnClickClose);
        Button_BackClose.onClick.AddListener(OnClickClose);

        Layout_RightSide.SetActive(false);
        Button_Craft.interactable = false;
    }

    private void OnEnable()
    {
        UnBindVM();
        ClearRecipeSlots();
        ClearRequiredItemSlots();

        Layout_RightSide.SetActive(false);
        Button_Craft.interactable = false;

        if (isActiveAndEnabled)
        {
            BindVM();
        }
    }

    private void OnDisable()
    {
        UnBindVM();
        ClearRecipeSlots();
        ClearRequiredItemSlots();
    }

    private void OnPropertyChanged_View(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(CraftingViewModel.RecipeList):
                CreateRecipeSlots();
                break;
            case nameof(CraftingViewModel.SelectedRecipe):
                RefreshSelectedItemInfo();
                break;
            case nameof(CraftingViewModel.RequiredItemList):
                CreateRequiredItemSlots();
                break;
            case nameof(CraftingViewModel.IsCraftable):
                Button_Craft.interactable = _craftingVM.IsCraftable;
                break;
        }
    }

    private void BindVM()
    {
        if (_craftingVM != null)
        {
            _craftingVM.PropertyChanged -= OnPropertyChanged_View;
        }

        _craftingVM = NetworkManager.Inst.CraftingService.GetCraftingViewModel();

        NetworkManager.Inst.CraftingService.RefreshInventoryState();

        _craftingVM.PropertyChanged += OnPropertyChanged_View;
        _craftingVM.InvokeOnceOnInit();
    }

    private void UnBindVM()
    {
        if (_craftingVM == null)
        {
            return;
        }

        _craftingVM.PropertyChanged -= OnPropertyChanged_View;
        _craftingVM = null;
    }

    private void RefreshSelectedItemInfo()
    {
        var selectedVM = _craftingVM.SelectedRecipe;

        Layout_RightSide.SetActive(selectedVM != null);

        Text_ItemName.text = "";
        Text_ItemDesc.text = "";

        if (selectedVM == null)
        {
            return;
        }

        var itemData = DataManager.Inst.GetData<ItemData>(selectedVM.ItemDataId);

        if (itemData == null)
        {
            return;
        }

        Text_ItemName.text = itemData.Name;
        Text_ItemDesc.text = itemData.Description;
    }

    private void CreateRecipeSlots()
    {
        ClearRecipeSlots();

        foreach (var slotVm in _craftingVM.RecipeList.Values)
        {
            var slot = Instantiate(
                Prefab_CraftingSlot,
                Transform_CraftingSlotUIRoot);

            slot.BindSlotVM(slotVm, OnSelectRecipe);
            _recipeSlots.Add(slot);
        }
    }

    private void ClearRecipeSlots()
    {
        foreach (var slot in _recipeSlots)
        {
            slot.Unbind();
            slot.gameObject.SetActive(false);
            Destroy(slot.gameObject);
        }

        _recipeSlots.Clear();
    }

    private void CreateRequiredItemSlots()
    {
        ClearRequiredItemSlots();
        foreach (var slotVm in _craftingVM.RequiredItemList)
        {
            var slot = Instantiate(
                Prefab_RequiredItemSlot,
                Transform_RequiredItemSlotUIRoot);
            slot.BindReqSlotVM(slotVm);
            _requiredItemSlots.Add(slot);
        }
    }

    private void ClearRequiredItemSlots()
    {
        foreach (var slot in _requiredItemSlots)
        {
            slot.Unbind();
            slot.gameObject.SetActive(false);
            Destroy(slot.gameObject);
        }

        _requiredItemSlots.Clear();
    }

    private void OnSelectRecipe(string recipeId)
    {
        NetworkManager.Inst.CraftingService.RequestSelectRecipe(recipeId);
    }

    public void OnClickCraft()
    {
        bool success = NetworkManager.Inst.CraftingService.RequestCraftSelectedItem();

        Debug.Log(success ? "제작 완료!" : "제작할 수 없습니다.");
    }

    public void OnClickClose()
    {
        //UIManager.Inst.Close();
    }


}
