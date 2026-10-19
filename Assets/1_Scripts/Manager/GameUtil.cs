using Cysharp.Threading.Tasks;
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
}
