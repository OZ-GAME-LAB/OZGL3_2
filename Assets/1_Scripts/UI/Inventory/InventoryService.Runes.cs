using System;
using System.Collections.Generic;

public partial class InventoryService
{
    // 일반 가방 슬롯과 분리하되 수량과 저장 인스턴스는 같은 서비스가 관리한다.
    private readonly List<InventoryItemViewModel> _runeItems = new();

    public event Action RuneInventoryChanged;

    internal bool TryReceiveStoryRune(string id)
    {
        var player = Unity.Netcode.NetworkManager.Singleton?.LocalClient?.PlayerObject;
        if (player == null || !player.IsOwner || GameStartSelection.Character == null ||
            DataManager.Inst == null || !DataManager.Inst.ReadData<RuneData>().ContainsKey(id))
            return false;
        foreach (var rune in _runeItems)
            if (rune.Id == id && rune.Count > 0) return true;

        if (!AddItem(id))
            return false;
        SoundManager.Inst?.PlaySfx(UnityEngine.Resources.Load<UnityEngine.AudioClip>("Audio/RuneAcquired"), 0.25f);
        return true;
    }

    public IEnumerable<InventoryItemViewModel> GetRuneItems()
    {
        foreach (InventoryItemViewModel item in _runeItems)
            yield return item;
    }

    private bool IsRuneItem(ItemData data)
    {
        return data.ItemType == nameof(ItemType.Rune)
            || DataManager.Inst.ReadData<RuneData>().ContainsKey(data.Id);
    }

    private bool AddRuneItem(ItemData data)
    {
        foreach (InventoryItemViewModel item in _runeItems)
        {
            if (item.Id != data.Id)
                continue;

            if (item.Count >= item.MaxStack)
                return false;

            item.Count++;
            return true;
        }

        _runeItems.Add(CreateInventoryItemViewModel(data));
        RuneInventoryChanged?.Invoke();
        return true;
    }
}
