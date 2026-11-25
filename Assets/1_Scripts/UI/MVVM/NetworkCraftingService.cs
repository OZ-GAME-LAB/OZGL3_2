using UnityEngine;
using System.Collections.Generic;

public class NetworkCraftingService
{
    private CraftingViewModel _craftingViewModel;
    private CraftingObject _workshop;

    public void SetWorkshop(CraftingObject workshop)
    {
        _workshop = workshop;
        LoadRecipeList();
    }

    public void ClearWorkshop(CraftingObject workshop)
    {
        if (_workshop == null || _workshop != workshop) return;
        _workshop = null;
        _craftingViewModel.SelectedRecipe = null;
        _craftingViewModel.RequiredItemList = new List<CraftingRequiredItemSlotViewModel>();
        _craftingViewModel.IsCraftable = false;
        LoadRecipeList();
    }


    private bool IsAvailable(CraftingRecipeData recipe) => recipe != null &&
        (string.IsNullOrEmpty(recipe.RequiredWorkshopProgressId) ||
         (_workshop != null && _workshop.IsWorkshopOpen(recipe.RequiredWorkshopProgressId)));

    public CraftingViewModel GetCraftingViewModel()
    {
        if (_craftingViewModel == null)
        {
            _craftingViewModel = new CraftingViewModel();
            LoadRecipeList();
        }

        return _craftingViewModel;
    }

    private void LoadRecipeList()
    {
        var recipeDataList = DataManager.Inst.ReadData<CraftingRecipeData>();

        var slotList = new Dictionary<string, CraftingSlotViewModel>();

        foreach (var recipe in recipeDataList.Values)
        {
            if (recipe == null)
            {
                Debug.LogWarning("유효하지 않은 제작 레시피가 있습니다.");
                continue;
            }

            if (!IsAvailable(recipe)) continue;
            var slotVm = new CraftingSlotViewModel
            {
                RecipeDataId = recipe.Id,
                ItemDataId = recipe.Item_Id
            };

            slotList.Add(slotVm.RecipeDataId, slotVm);
        }

        _craftingViewModel.RecipeList = slotList;
    }

    public void RequestSelectRecipe(string recipeId)
    {
        if (string.IsNullOrEmpty(recipeId))
        {
            return;
        }

        var craftingVm = GetCraftingViewModel();

        if (craftingVm.RecipeList.TryGetValue(recipeId, out var slotVm) == false)
        {
            return;
        }

        var recipe = DataManager.Inst.GetData<CraftingRecipeData>(recipeId);

        if (!IsAvailable(recipe))
        {
            return;
        }

        if (TryGetMaterialCounts(recipe, out var materials) == false)
        {
            return;
        }

        var requiredItems = new List<CraftingRequiredItemSlotViewModel>();

        foreach (var material in materials)
        {
            var materialVm = new CraftingRequiredItemSlotViewModel
            {
                ItemDataId = material.Key,
                RequiredCount = material.Value,
                OwnedCount = GetOwnedMaterialCount(material.Key)
            };

            requiredItems.Add(materialVm);
        }

        craftingVm.IsCraftable = false;
        craftingVm.SelectedRecipe = slotVm;
        craftingVm.RequiredItemList = requiredItems;

        RefreshInventoryState();
    }

    public void RefreshInventoryState()
    {
        if (_craftingViewModel == null)
        {
            return;
        }

        var selected = _craftingViewModel.SelectedRecipe;
        var recipe = selected == null ? null : DataManager.Inst.GetData<CraftingRecipeData>(selected.RecipeDataId);
        bool canCraft = IsAvailable(recipe);

        foreach (var materialVm in _craftingViewModel.RequiredItemList)
        {
            materialVm.OwnedCount = GetOwnedMaterialCount(materialVm.ItemDataId);

            if (materialVm.IsEnough == false)
            {
                canCraft = false;
            }
        }

        _craftingViewModel.IsCraftable = canCraft;
    }

    public bool RequestCraftSelectedItem()
    {
        var craftingVm = GetCraftingViewModel();

        if (craftingVm.SelectedRecipe == null)
        {
            return false;
        }

        string recipeId = craftingVm.SelectedRecipe.RecipeDataId;

        var recipe = DataManager.Inst.GetData<CraftingRecipeData>(recipeId);

        if (!IsAvailable(recipe))
        {
            return false;
        }

        if (TryGetMaterialCounts(recipe, out var materials) == false)
        {
            craftingVm.IsCraftable = false;
            return false;
        }

        InventoryService inventory = ServiceManager.Inst.InventoryService;
        bool crafted = inventory.TryCraft(materials, DataManager.Inst.GetData<ItemData>(recipe.Item_Id));
        RefreshInventoryState();
        return crafted;
    }

    private bool TryGetMaterialCounts(CraftingRecipeData recipe, out Dictionary<string, int> materials)
    {
        materials = new Dictionary<string, int>();

        if (recipe == null || string.IsNullOrEmpty(recipe.Id) || string.IsNullOrEmpty(recipe.Item_Id))
        {
            return false;
        }

        if (DataManager.Inst.GetData<ItemData>(recipe.Item_Id) == null)
        {
            return false;
        }

        if (recipe.RequiredMaterialId == null || recipe.RequiredMaterialQuantity == null || recipe.RequiredMaterialId.Length == 0 || recipe.RequiredMaterialId.Length != recipe.RequiredMaterialQuantity.Length)
        {
            return false;
        }

        for (int i = 0; i < recipe.RequiredMaterialId.Length; i++)
        {
            string itemId = recipe.RequiredMaterialId[i];
            int count = recipe.RequiredMaterialQuantity[i];

            if (string.IsNullOrEmpty(itemId) || count <= 0)
            {
                return false;
            }

            if (DataManager.Inst.GetData<ItemData>(itemId) == null)
            {
                return false;
            }

            materials.TryGetValue(itemId, out int previousCount);

            if (previousCount > int.MaxValue - count)
            {
                return false;
            }

            materials[itemId] = previousCount + count;
        }

        return true;
    }

    private int GetOwnedMaterialCount(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return 0;
        }

        InventoryService inventory = ServiceManager.Inst != null ? ServiceManager.Inst.InventoryService : null;

        if (inventory == null)
        {
            return 0;
        }

        long total = 0;

        foreach (InventoryItemViewModel item in inventory.GetInventoryItems())
        {
            if (item == null || item.Id != itemId || item.Count <= 0)
            {
                continue;
            }

            // 장착 중인 장비와 사용 결과를 기다리는 아이템은 제작 재료에서 제외.
            if (inventory.IsEquippedItem(item) || inventory.IsItemUsePending(item.Id))
            {
                continue;
            }

            total += item.Count;

            if (total >= int.MaxValue)
            {
                return int.MaxValue;
            }
        }

        return (int)total;
    }
}
