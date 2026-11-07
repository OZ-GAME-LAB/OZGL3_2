using Cysharp.Threading.Tasks;
using System;
using UnityEngine;
using UnityEngine.UI;

public static class GameUtil
{
    public static async UniTask<Sprite> LoadAndSetSpriteImage(Image targetImage, string spritePath)
    {
        Sprite sprite = await ResourceManager.Inst.LoadAsset<Sprite>(spritePath);
        if (sprite != null)
        {
            targetImage.sprite = sprite;
        }

        return sprite;
    }

    public static float CalculateInteractionDifficultyMultiplier(int targetTier, int equipmentTier, float increasePerTier = 0.25f)
    {
        int tierDifference = targetTier - equipmentTier;

        float multiplier = 1f + tierDifference * increasePerTier;

        return Math.Max(0.5f, multiplier);
    }
}
