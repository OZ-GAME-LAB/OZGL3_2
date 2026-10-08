using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Inst { get; private set; }

    private void Awake()
    {
        {
            Inst = this;
        }
    }

    private Dictionary<string, AsyncOperationHandle> _handles = new Dictionary<string, AsyncOperationHandle>();

    public async UniTask<T> LoadAsset<T>(string address) where T : UnityEngine.Object
    {
        if (_handles.TryGetValue(address, out AsyncOperationHandle handle))
        {
            return handle.Result as T;
        }

        AsyncOperationHandle<T> loadHandle = Addressables.LoadAssetAsync<T>(address);

        try
        {
            T result = await loadHandle.ToUniTask();

            _handles[address] = loadHandle;
            return result;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"에셋 로드 실패: {address} / Error: {e.Message}");

            if (loadHandle.IsValid())
                Addressables.Release(loadHandle);

            return null;
        }
    }

    public async UniTask<GameObject> InstantiateAsync(string address, Transform parent = null, bool instantiateInWorldSpace = false, bool isSetToZeroPos = false)
    {
        AsyncOperationHandle<GameObject> handle = Addressables.InstantiateAsync(address, parent, instantiateInWorldSpace);

        try
        {
            GameObject instance = await handle.ToUniTask();

            if (isSetToZeroPos == true)
            {
                instance.transform.localPosition = Vector3.zero;
            }

            return instance;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"프리팹 생성 실패: {address} / Error: {e.Message}");

            if (handle.IsValid())
                Addressables.Release(handle);

            return null;
        }
    }

    public async UniTask<Sprite> LoadSprite(string address)
    {
        if (_handles.TryGetValue(address, out AsyncOperationHandle handle))
        {
            return handle.Result as Sprite;
        }

        AsyncOperationHandle<Sprite> handleOrigin = Addressables.LoadAssetAsync<Sprite>(address);

        try
        {
            Sprite result = await handleOrigin.ToUniTask();

            _handles[address] = handleOrigin;

            return result;
        }
        catch (System.Exception)
        {
            Debug.LogError($"스프라이트 로드 실패: {address}");

            if (handleOrigin.IsValid())
                Addressables.Release(handleOrigin);

            return null;
        }
    }

    public void Release(string address)
    {
        if (_handles.TryGetValue(address, out AsyncOperationHandle handle))
        {
            Addressables.Release(handle);
            _handles.Remove(address);
            Debug.Log($"에셋 메모리 해제 완료: {address}");
        }
    }
}
