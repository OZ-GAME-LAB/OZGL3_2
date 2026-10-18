public class InventoryUIViewModel : ViewModelBase
{

    public void InvokeOnceOnInit()
    {
        OnPropertyChanged(nameof(Id));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(ItemType));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(MaxStack));
        OnPropertyChanged(nameof(Price));
        OnPropertyChanged(nameof(SellingPrice));
        OnPropertyChanged(nameof(IconPath));
        OnPropertyChanged(nameof(PrefabPath));
    }

    private string _id;
    public string Id
    {
        get => _id;
        set
        {
            if (_id != value)
            {
                _id = value;
                OnPropertyChanged(nameof(Id));
            }
        }
    }

    private string _name;
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
    }

    private string _itemType;
    public string ItemType
    {
        get => _itemType;
        set
        {
            if (_itemType != value)
            {
                _itemType = value;
                OnPropertyChanged(nameof(ItemType));
            }
        }
    }

    private string _description;
    public string Description
    {
        get => _description;
        set
        {
            if (_description != value)
            {
                _description = value;
                OnPropertyChanged(nameof(Description));
            }
        }
    }

    private int _maxStack;
    public int MaxStack
    {
        get => _maxStack;
        set
        {
            if (_maxStack != value)
            {
                _maxStack = value;
                OnPropertyChanged(nameof(MaxStack));
            }
        }
    }

    private int _price;
    public int Price
    {
        get => _price;
        set
        {
            if (_price != value)
            {
                _price = value;
                OnPropertyChanged(nameof(Price));
            }
        }
    }

    private int _sellingPrice;
    public int SellingPrice
    {
        get => _sellingPrice;
        set
        {
            if (_sellingPrice != value)
            {
                _sellingPrice = value;
                OnPropertyChanged(nameof(SellingPrice));
            }
        }
    }

    private string _iconPath;
    public string IconPath
    {
        get => _iconPath;
        set
        {
            if (_iconPath != value)
            {
                _iconPath = value;
                OnPropertyChanged(nameof(IconPath));
            }
        }
    }

    private string _prefabPath;
    public string PrefabPath
    {
        get => _prefabPath;
        set
        {
            if (_prefabPath != value)
            {
                _prefabPath = value;
                OnPropertyChanged(nameof(PrefabPath));
            }
        }
    }
}