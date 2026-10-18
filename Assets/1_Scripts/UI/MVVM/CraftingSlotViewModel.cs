using System.Collections.Generic;
using UnityEngine;

public class CraftingSlotViewModel : ViewModelBase
{
    // 레시피 데이터 하나만 넣어도 되지만 나중에 하나의 아이템을 만드는데 여러 레시피로 만들 수 있을지도 몰라서 일단 레시피, 아이템 아이디를 모두 넣었습니다.
    public void InvokeOnceOnInit()
    {
        OnPropertyChanged(nameof(RecipeDataId));
        OnPropertyChanged(nameof(ItemDataId));
    }

    public IReadOnlyList<CraftingRequiredItemSlotViewModel> RequiredItems
    {
        get;
        private set;
    }

    private string _recipeDataId;
    public string RecipeDataId
    {
        get => _recipeDataId;
        set
        {
            if (_recipeDataId == value)
            {
                return;
            }

            _recipeDataId = value;
            OnPropertyChanged(nameof(RecipeDataId));
        }
    }

    private string _itemDataId;
    public string ItemDataId
    {
        get => _itemDataId;
        set
        {
            if (_itemDataId == value)
            {
                return;
            }

            _itemDataId = value;
            OnPropertyChanged(nameof(ItemDataId));
        }
    }

    private bool _canCraft;
    public bool CanCraft
    {
        get => _canCraft;
        set
        {
            if (_canCraft == value)
            {
                return;
            }

            _canCraft = value;
            OnPropertyChanged(nameof(CanCraft));
        }
    }
}
