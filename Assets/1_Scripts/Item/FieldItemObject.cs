using Cysharp.Threading.Tasks;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class FieldItemObject : NetworkBehaviour
{
    [Header("땅 충돌용 콜라이더")]
    [SerializeField] private BoxCollider2D Collider_Body;
    
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private TMP_Text Text_ItemCount;
    [SerializeField] private bool _showCountWhenOne = true;

    private NetworkVariable<FixedString128Bytes> _itemDataId = new NetworkVariable<FixedString128Bytes>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> _count = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public string ItemDataId => _itemDataId.Value.ToString();
    public int Count => _count.Value;
    public bool CanMerge => (IsSpawned == true && Count > 0 && _maxStack > 0 && Count < _maxStack);

    private int _visualVersion;
    private int _maxStack;

    private void Awake()
    {
        if (_spriteRenderer == null)
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (Collider_Body == null)
        {
            Collider_Body = GetComponent<BoxCollider2D>();
        }
    }

    public void Init(string itemDataId, int count)
    {
        _itemDataId.Value = new FixedString128Bytes(itemDataId);
        _count.Value = count;
    }

    public override void OnNetworkSpawn()
    {
        RefreshStackLimit();

        _itemDataId.OnValueChanged += OnItemDataIdChanged;
        _count.OnValueChanged += OnCountChanged;

        RefreshVisualAsync().Forget();
        RefreshCountText();
    }

    public override void OnNetworkDespawn()
    {
        _itemDataId.OnValueChanged -= OnItemDataIdChanged;
        _count.OnValueChanged -= OnCountChanged;

        _visualVersion++;

        if (_spriteRenderer != null)
        {
            _spriteRenderer.sprite = null;
        }

        if (Text_ItemCount != null)
        {
            Text_ItemCount.text = "";
        }
    }

    private void OnCountChanged(int previousCount, int currentCount)
    {
        RefreshCountText();
    }

    private void RefreshCountText()
    {
        if (Text_ItemCount == null)
        {
            return;
        }

        bool showText =
            Count > 0 &&
            (_showCountWhenOne || Count > 1);

        Text_ItemCount.gameObject.SetActive(showText);

        if (showText)
        {
            Text_ItemCount.text = $"{Count}";
        }
    }

    private void OnItemDataIdChanged(FixedString128Bytes previous, FixedString128Bytes current)
    {
        RefreshStackLimit();
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

        if (Collider_Body == null)
        {
            Debug.LogError("아이템에 땅 충돌용 BoxCollider2D가 없습니다.", this);
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

        if (this == null || IsSpawned == false || version != _visualVersion || _spriteRenderer == null || Collider_Body == null)
        {
            return;
        }

        if (sprite == null)
        {
            Debug.LogError($"아이템 스프라이트 로드 실패: {itemId}, 경로: {itemData.IconPath}", this);

            return;
        }

        _spriteRenderer.sprite = sprite;
        RefreshCollider(sprite);
    }

    private void RefreshCollider(Sprite sprite)
    {
        if (Collider_Body == null || sprite == null)
        {
            return;
        }

        Bounds bounds = sprite.bounds;

        Collider_Body.size = new Vector2(bounds.size.x, bounds.size.y);

        Vector2 offset = new Vector2(bounds.center.x, bounds.center.y);

        if (_spriteRenderer.flipX)
        {
            offset.x = -offset.x;
        }

        if (_spriteRenderer.flipY)
        {
            offset.y = -offset.y;
        }

        Collider_Body.offset = offset;
    }

    private void RefreshStackLimit()
    {
        _maxStack = 0;

        if (DataManager.Inst == null)
        {
            return;
        }

        ItemData itemData =
            DataManager.Inst.GetData<ItemData>(ItemDataId);

        if (itemData != null)
        {
            _maxStack = itemData.MaxStack;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsServer == false || CanMerge == false)
        {
            return;
        }

        FieldItemObject other = collision.gameObject.GetComponent<FieldItemObject>();

        if (other == null || other == this || other.CanMerge == false)
        {
            return;
        }

        if (_itemDataId.Value.Equals(other._itemDataId.Value) == false)
        {
            return;
        }

        FieldItemObject receiver;
        FieldItemObject donor;

        if (NetworkObjectId < other.NetworkObjectId)
        {
            receiver = this;
            donor = other;
        }
        else
        {
            receiver = other;
            donor = this;
        }

        receiver.MergeFormServer(donor);
    }

    private void MergeFormServer(FieldItemObject donor)
    {
        if (IsServer == false || CanMerge == false || donor == null || donor == this || donor.CanMerge == false)
        {
            return;
        }

        if (_itemDataId.Value.Equals(donor._itemDataId.Value) == false)
        {
            return;
        }

        int remainingSpace = _maxStack - Count;
        int moveCount = Mathf.Min(remainingSpace, donor.Count);

        if (moveCount <= 0)
        {
            return;
        }

        _count.Value += moveCount;
        donor._count.Value -= moveCount;

        if (donor.Count == 0)
        {
            donor.NetworkObject.Despawn(destroy: true);
        }
    }
}
