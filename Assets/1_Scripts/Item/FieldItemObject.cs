using Cysharp.Threading.Tasks;
using TMPro;
using System.Collections;
using System.Collections.Generic;
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
    public bool CanMerge => (IsSpawned == true && Count > 0 && _maxStack > 0 && Count < _maxStack && !_reserved && Time.time >= _mergeAllowedAt && _dropper == null);

    private int _visualVersion;
    private int _maxStack;

    [Header("생성 후 획득 대기 시간")]
    [SerializeField, Min(0f)] private float _pickupDelay = 1f;

    private float _pickupAllowedAt;
    private float _mergeAllowedAt;
    private bool _reserved;
    private PlayerNetworkStats _dropper;
    private readonly List<Collider2D> _pickupContacts = new();
    private readonly HashSet<PlayerNetworkStats> _checkedPlayers = new();
    private Collider2D _pickupArea;
    private Coroutine _contactCheck;
    private int _nextContact;


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
        foreach (var collider in GetComponentsInChildren<Collider2D>())
        {
            if (!collider.isTrigger) continue;
            _pickupArea = collider;
            break;
        }
    }

    public void Init(string itemDataId, int count)
    {
        _itemDataId.Value = new FixedString128Bytes(itemDataId);
        _count.Value = count;
    }

    public override void OnNetworkSpawn()
    {
        _pickupAllowedAt = Time.time + _pickupDelay;

        RefreshStackLimit();

        _itemDataId.OnValueChanged += OnItemDataIdChanged;
        _count.OnValueChanged += OnCountChanged;

        RefreshVisualAsync().Forget();
        RefreshCountText();
    }

    public override void OnNetworkDespawn()
    {
        StopAllCoroutines();
        _contactCheck = null;
        _pickupContacts.Clear();
        _checkedPlayers.Clear();
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

        if (itemData == null)
        {
            return;
        }

        string iconPath = itemData.IconPath;

        if (string.IsNullOrWhiteSpace(iconPath))
        {
            iconPath = "sampleicon";
        }

        Sprite sprite = await ResourceManager.Inst.LoadSprite(iconPath);

        if (this == null || IsSpawned == false || version != _visualVersion || _spriteRenderer == null || Collider_Body == null)
        {
            return;
        }

        if (sprite == null)
        {
            Debug.LogError($"아이템 스프라이트 로드 실패: {itemId}, 경로: {iconPath}", this);

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

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsServer || !IsSpawned || _pickupArea == null || collision.isTrigger ||
            collision.gameObject.layer != LayerMask.NameToLayer("Player") || collision.attachedRigidbody == null ||
            !collision.attachedRigidbody.TryGetComponent<PlayerNetworkStats>(out _) || _pickupContacts.Contains(collision))
            return;
        _pickupContacts.Add(collision);
        if (_contactCheck == null) _contactCheck = StartCoroutine(CheckPickupContacts());
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!IsServer || !_pickupContacts.Remove(collision) || _pickupContacts.Count != 0 || _contactCheck == null)
            return;
        StopCoroutine(_contactCheck);
        _contactCheck = null;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsServer == false || CanMerge == false)
        {
            return;
        }


        if (CanMerge == false)
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

    // 잠든 Rigidbody에는 Stay가 오지 않는다. 접촉 중에만 서버가 실제 겹침을 다시 확인한다.
    private IEnumerator CheckPickupContacts()
    {
        yield return new WaitForFixedUpdate();
        float initialDelay = _pickupAllowedAt - Time.time;
        if (initialDelay > 0f) yield return new WaitForSeconds(initialDelay);
        var retry = new WaitForSeconds(1f);
        while (IsSpawned && _pickupArea.isActiveAndEnabled)
        {
            for (int i = _pickupContacts.Count - 1; i >= 0; --i)
            {
                var body = _pickupContacts[i];
                if (body == null || !body.isActiveAndEnabled || body.isTrigger || body.attachedRigidbody == null ||
                    !body.attachedRigidbody.simulated || !_pickupArea.Distance(body).isOverlapped)
                    _pickupContacts.RemoveAt(i);
            }
            if (_pickupContacts.Count == 0) break;
            _checkedPlayers.Clear();
            for (int i = 0; !_reserved && i < _pickupContacts.Count; ++i)
            {
                _nextContact %= _pickupContacts.Count;
                var body = _pickupContacts[_nextContact++].attachedRigidbody;
                if (body.TryGetComponent(out PlayerNetworkStats player) && player != _dropper && _checkedPlayers.Add(player))
                    player.TryReservePickup(this);
            }
            yield return retry;
        }
        _checkedPlayers.Clear();
        _contactCheck = null;
    }

    internal bool ReserveForTransfer()
    {
        if (!IsServer || !IsSpawned || _reserved || Count < 1) return false;
        _reserved = true;
        return true;
    }

    internal void FinishPickup(int accepted)
    {
        if (!_reserved) return;
        _reserved = false;
        _count.Value -= accepted;
        if (Count == 0) NetworkObject.Despawn();
    }

    internal void FinishDrop(bool committed, PlayerNetworkStats dropper)
    {
        if (!_reserved) return;
        _reserved = false;
        if (!committed)
        {
            NetworkObject.Despawn();
            return;
        }
        _dropper = dropper;
        _mergeAllowedAt = Time.time + _pickupDelay;
        if (dropper != null) StartCoroutine(WaitForDropperToLeave());
    }

    private IEnumerator WaitForDropperToLeave()
    {
        var delay = new WaitForSeconds(0.2f);
        var colliders = GetComponentsInChildren<Collider2D>();
        var bodies = _dropper.GetComponentsInChildren<Collider2D>();
        do
        {
            yield return delay;
            if (_dropper == null || !_dropper.IsSpawned) break;
            bool touching = false;
            foreach (var trigger in colliders)
                foreach (var body in bodies)
                    if (trigger != null && trigger.enabled && trigger.isTrigger && body != null &&
                        body.enabled && !body.isTrigger && trigger.bounds.Intersects(body.bounds))
                        touching = true;
            if (!touching && Time.time >= _mergeAllowedAt) break;
        } while (IsSpawned);
        _dropper = null;
    }
}
