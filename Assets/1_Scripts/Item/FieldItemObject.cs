using Cysharp.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class FieldItemObject : NetworkBehaviour
{
    [SerializeField] private SpriteRenderer _spriteRenderer;

    private NetworkVariable<FixedString128Bytes> _itemDataId = new NetworkVariable<FixedString128Bytes>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> _count = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public string ItemDataId => _itemDataId.Value.ToString();
    public int Count => _count.Value;

    private int _visualVersion;

    private void Awake()
    {
        if (_spriteRenderer == null)
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }
    }

    public void Init(string itemDataId, int count)
    {
        _itemDataId.Value = new FixedString128Bytes(itemDataId);
        _count.Value = count;
    }

    public override void OnNetworkSpawn()
    {
        if (IsClient == false)
        {
            return;
        }

        _itemDataId.OnValueChanged += OnItemDataIdChanged;
        RefreshVisualAsync().Forget();
    }

    public override void OnNetworkDespawn()
    {
        _itemDataId.OnValueChanged -= OnItemDataIdChanged;
        
        _visualVersion++;

        if (_spriteRenderer != null)
        {
            _spriteRenderer.sprite = null;
        }
    }

    private void OnItemDataIdChanged(FixedString128Bytes previous, FixedString128Bytes current)
    {
        RefreshVisualAsync().Forget();
    }

    private async UniTask RefreshVisualAsync()
    {
        int version = ++_visualVersion;

        if (_spriteRenderer == null)
        {
            Debug.LogError("아이템에 SpriteRenderer가 없습니다.", this);
            return;
        }

        _spriteRenderer.sprite = null;

        if (DataManager.Inst == null || ResourceManager.Inst == null)
        {
            Debug.LogError("매니저가 null입니다.", this);
            return;
        }

        string itemId = ItemDataId;

        if (string.IsNullOrEmpty(itemId))
        {
            return;
        }

        var itemData = DataManager.Inst.GetData<ItemData>(itemId);

        if (itemData == null || string.IsNullOrEmpty(itemData.IconPath))
        {
            return;
        }

        Sprite sprite = await ResourceManager.Inst.LoadSprite(itemData.IconPath);

        if (this == null || IsSpawned == false | version != _visualVersion || _spriteRenderer == null)
        {
            return;
        }

        _spriteRenderer.sprite = sprite;
    }

    
}
