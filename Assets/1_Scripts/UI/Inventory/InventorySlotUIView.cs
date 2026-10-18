using System;
using UnityEngine;
using UnityEngine.UI;

public class InventorySlotUIView : MonoBehaviour
{
    [SerializeField] private Button _useButton;

    private ItemData _itemData;

    public event Action<InventorySlotUIView, ItemData> ItemUsed;

    private void OnEnable()
    {
        if (_useButton != null)
            _useButton.onClick.AddListener(OnClickUseButton);
    }

    private void OnDisable()
    {
        if (_useButton != null)
            _useButton.onClick.RemoveListener(OnClickUseButton);
    }

    public void Bind(ItemData itemData)
    {
        _itemData = itemData;
    }

    private void OnClickUseButton()
    {
        if (_itemData == null)
            return;

        // 실제 아이템 효과가 있다면 이벤트 발생 전에 처리하거나,
        // 별도의 아이템 사용 로직을 여기서 호출하세요.
        ItemUsed?.Invoke(this, _itemData);
    }
}