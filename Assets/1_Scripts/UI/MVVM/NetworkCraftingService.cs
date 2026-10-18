using UnityEngine;
using System.Collections.Generic;

public class NetworkCraftingService
{
    private CraftingViewModel _craftingViewModel;

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

        if (recipe == null)
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
                OwnedCount = 0 //InventoryManager.Inst.GetItemCount(material.Key)
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

        bool canCraft = _craftingViewModel.SelectedRecipe != null;

        foreach (var materialVm in _craftingViewModel.RequiredItemList)
        {
            materialVm.OwnedCount = 0; //InventoryManager.Inst.GetItemCount(materialVm.ItemDataId);

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

        if (recipe == null)
        {
            return false;
        }

        if (TryGetMaterialCounts(recipe, out var materials) == false)
        {
            craftingVm.IsCraftable = false;
            return false;
        }

        foreach (var material in materials)
        {
            int ownedCount = 0; // InventoryManager.Inst.GetItemCount(material.Key);

            if (ownedCount < material.Value)
            {
                Debug.LogWarning("제작 실패, 재료 부족");
                RefreshInventoryState();
                return false;
            }
        }

        /*
        foreach (var material in materials)
        {
            InventoryManager.Inst.RemoveItem(
                material.Key,
                material.Value);
        }

        InventoryManager.Inst.AddItem(recipe.Item_Id, 1);
        */
        Debug.LogWarning("제작 성공, 인벤토리가 연결되지 않아 제작을 실행하지 않았습니다.");
        RefreshInventoryState();
        return true;
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

        if (recipe.RequiredMaterialId == null || recipe.RequiredMaterialQuantity == null || recipe.RequiredMaterialId.Length != recipe.RequiredMaterialQuantity.Length)
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
}
