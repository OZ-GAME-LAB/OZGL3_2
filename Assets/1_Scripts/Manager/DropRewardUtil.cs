using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public static class DropRewardUtil
{
    public static void SpawnMonsterRewardsOnServer(MonsterData data, Vector3 position, int playerCount, Object context = null)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsServer || playerCount <= 0)
            return;

        if (data == null)
        {
            Debug.LogError("사망 보상에 필요한 몬스터 데이터가 없습니다.", context);
            return;
        }

        List<string> selectedItems = new();

        if (data.IsMandatoryDropItem
            && !TryAppendRewards(data.MandatoryDropGroupId, true, 1f, selectedItems, context))
            return;

        if (!string.IsNullOrWhiteSpace(data.ProbabilityDropGroupId)
            && !TryAppendRewards(data.ProbabilityDropGroupId, false, 1f, selectedItems, context))
            return;

        SpawnItemsOnServer(selectedItems, playerCount, position, context);
    }

    public static void SpawnItemsOnServer(IReadOnlyList<string> selectedItems, int playerCount, Vector3 position, Object context = null)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsServer || playerCount <= 0 || selectedItems == null || selectedItems.Count == 0)
            return;

        if (ObjectManager.Inst == null)
        {
            Debug.LogError("보상 아이템 생성에 필요한 ObjectManager가 준비되지 않았습니다.", context);
            return;
        }

        foreach (string itemId in selectedItems)
        {
            for (int i = 0; i < playerCount; i++)
            {
                if (ObjectManager.Inst.SpawnItem(itemId, 1, position) == null)
                    Debug.LogError($"보상 아이템 생성 실패: {itemId}", context);
            }
        }
    }

    public static string SelectRewardItem(DropGroupData dropData, float totalChance)
    {
        float roll = UnityEngine.Random.value * totalChance;
        float accumulatedChance = 0f;
        int lastPositiveIndex = -1;

        for (int i = 0; i < dropData.ItemId.Length; i++)
        {
            float chance = dropData.DropChance[i];

            if (chance <= 0f)
                continue;

            lastPositiveIndex = i;
            accumulatedChance += chance;

            if (roll < accumulatedChance)
                return dropData.ItemId[i];
        }

        return dropData.ItemId[lastPositiveIndex];
    }

    public static bool TryAppendRewards(string groupId, bool isMandatory, float rewardMultiplier, List<string> selectedItems, Object context = null)
    {
        if (!TryGetRewardGroup(groupId, isMandatory, out DropGroupData dropData, context))
        {
            return false;
        }

        if (isMandatory)
        {
            foreach (string itemId in dropData.ItemId)
            {
                int baseQuantity = UnityEngine.Random.Range(dropData.MinQuantity, dropData.MaxQuantity + 1);

                int quantity = Mathf.FloorToInt(baseQuantity * rewardMultiplier);

                for (int i = 0; i < quantity; i++)
                {
                    selectedItems.Add(itemId);
                }
            }
        }
        else
        {
            int baseQuantity = UnityEngine.Random.Range(dropData.MinQuantity, dropData.MaxQuantity + 1);

            int quantity = Mathf.FloorToInt(baseQuantity * rewardMultiplier);

            float totalChance = 0f;

            foreach (float chance in dropData.DropChance)
            {
                totalChance += chance;
            }

            for (int i = 0; i < quantity; i++)
            {
                selectedItems.Add(SelectRewardItem(dropData, totalChance));
            }
        }

        return true;
    }

    public static bool TryGetRewardGroup(
        string groupId,
        bool isMandatory,
        out DropGroupData dropData, Object context = null)
    {
        dropData = null;

        if (string.IsNullOrWhiteSpace(groupId))
        {
            Debug.LogError("필수 드롭 그룹 ID가 비어 있습니다.", context);
            return false;
        }

        if (DataManager.Inst == null)
        {
            Debug.LogError("드롭 데이터 조회에 필요한 DataManager가 준비되지 않았습니다.", context);
            return false;
        }

        dropData = DataManager.Inst.GetData<DropGroupData>(groupId);

        if (dropData == null)
        {
            Debug.LogError(
                $"드롭 그룹을 찾지 못했습니다: {groupId}", context);
            return false;
        }

        if (dropData.ItemId == null || dropData.ItemId.Length == 0)
        {
            Debug.LogError(
                $"드롭 그룹의 아이템 목록이 비어 있습니다: {groupId}", context);
            return false;
        }

        foreach (string itemId in dropData.ItemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                Debug.LogError(
                    $"드롭 그룹에 빈 아이템 ID가 있습니다: {groupId}", context);
                return false;
            }
        }

        if (dropData.MinQuantity < 0 ||
            dropData.MaxQuantity < dropData.MinQuantity ||
            dropData.MaxQuantity == int.MaxValue)
        {
            Debug.LogError(
                $"드롭 수량 범위를 확인해 주세요: {groupId}", context);
            return false;
        }

        if (isMandatory)
            return true;

        if (dropData.DropChance == null ||
            dropData.ItemId.Length != dropData.DropChance.Length)
        {
            Debug.LogError(
                $"아이템 목록과 확률 배열의 길이가 다릅니다: {groupId}", context);
            return false;
        }

        float totalChance = 0f;

        foreach (float chance in dropData.DropChance)
        {
            if (float.IsNaN(chance) || float.IsInfinity(chance) || chance < 0f || chance > 1f)
            {
                Debug.LogError($"드롭 확률은 0~1의 유효한 숫자여야 합니다: {groupId}", context);
                return false;
            }
            totalChance += chance;
        }

        if (!Mathf.Approximately(totalChance, 1f))
        {
            Debug.LogError($"드롭 확률 합계가 1이 아닙니다: {groupId}, 합계: {totalChance}", context);
            return false;
        }

        return true;
    }

}
