using NUnit.Framework.Internal.Execution;
using Unity.Netcode;
using UnityEngine;

public class WorldItemSpawnTest : MonoBehaviour
{
    [SerializeField] private string _itemId = "mat_slime_lump";
    [SerializeField] private int _count = 10;

    private NetworkObject _spawnedItem;

    [ContextMenu("테스트/아이템 생성")]
    private void SpawnItem()
    {
        if (!Application.isPlaying || ObjectManager.Inst == null)
        {
            return;
        }

        if (_spawnedItem != null && _spawnedItem.IsSpawned)
        {
            return;
        }

        FieldItemObject item = ObjectManager.Inst.SpawnItem(_itemId, _count, transform.position);

        if (item != null)
        {
            _spawnedItem = item.NetworkObject;
        }
    }

    [ContextMenu("테스트/아이템 제거")]
    private void DespawnItem()
    {
        if (!Application.isPlaying || ObjectManager.Inst == null)
        {
            return;
        }

        ObjectManager.Inst.DespawnObject(_spawnedItem);
    }
}
