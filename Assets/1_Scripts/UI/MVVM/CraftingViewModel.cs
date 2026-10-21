using System.Collections.Generic;

public class CraftingViewModel : ViewModelBase
{
    public void InvokeOnceOnInit()
    {
        OnPropertyChanged(nameof(RecipeList));
        OnPropertyChanged(nameof(SelectedRecipe));
        OnPropertyChanged(nameof(RequiredItemList));
        OnPropertyChanged(nameof(IsCraftable));
    }

    private Dictionary<string, CraftingSlotViewModel> _recipeList = new Dictionary<string, CraftingSlotViewModel>();
    public Dictionary<string, CraftingSlotViewModel> RecipeList
    {
        get => _recipeList;
        set
        {
            if (_recipeList == value)
            {
                return;
            }
            _recipeList = value;
            OnPropertyChanged(nameof(RecipeList));
        }
    }

    private CraftingSlotViewModel _selectedRecipe;
    public CraftingSlotViewModel SelectedRecipe
    {
        get => _selectedRecipe;
        set
        {
            if (_selectedRecipe == value)
                return;

            _selectedRecipe = value;
            OnPropertyChanged(nameof(SelectedRecipe));
        }
    }

    private List<CraftingRequiredItemSlotViewModel> _requiredItemList = new List<CraftingRequiredItemSlotViewModel>();
    public List<CraftingRequiredItemSlotViewModel> RequiredItemList
    {
        get => _requiredItemList;
        set
        {
            if (_requiredItemList == value)
                return;

            _requiredItemList = value;
            OnPropertyChanged(nameof(RequiredItemList));
        }
    }

    private bool _isCraftable;
    public bool IsCraftable
    {
        get => _isCraftable;
        set
        {
            if (_isCraftable == value)
                return;

            _isCraftable = value;
            OnPropertyChanged(nameof(IsCraftable));
        }
    }
}
