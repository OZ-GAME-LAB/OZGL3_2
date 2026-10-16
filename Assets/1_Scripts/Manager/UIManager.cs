using Cysharp.Threading.Tasks;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    private readonly HashSet<UIType> _loadingUISet = new HashSet<UIType>();
    private readonly HashSet<UIType> _openedUISet = new HashSet<UIType>();

    private readonly Dictionary<UIRootType, RectTransform> _rootCacheDictionary = new Dictionary<UIRootType, RectTransform>();
    private readonly Dictionary<UIType, UIBase> _createdUICacheDictionary = new Dictionary<UIType, UIBase>();


    [SerializeField] private RectTransform _mainRoot;
    [SerializeField] private RectTransform _contentRoot;
    [SerializeField] private RectTransform _popupRoot;
    [SerializeField] private RectTransform _veryFrontRoot;
    public static UIManager Inst { get; private set; }


    private void Awake()
    {
        Inst = this;
        CacheRootDictionary();

    }

    //메인
    public async UniTask<UIBase> OpenMainRootAsync(UIType uiType, CancellationToken cancellationToken = default)
    {
        UIBase baseUI = await OpenUIAsync(uiType, UIRootType.Main, cancellationToken);
        return baseUI;
    }

    //콘텐츠
    public async UniTask<UIBase> OpenContentRootAsync(UIType uiType, CancellationToken cancellationToken = default)
    {
        return await OpenUIAsync(uiType, UIRootType.Content, cancellationToken);
    }

    // 팝업
    public async UniTask<UIBase> OpenPopupRootAsync(UIType uiType, CancellationToken cancellationToken = default)
    {
        return await OpenUIAsync(uiType, UIRootType.Popup, cancellationToken);
    }

    // 베리프론트
    public async UniTask<UIBase> OpenVeryFrontRootAsync(UIType uiType, CancellationToken cancellationToken = default)
    {
        return await OpenUIAsync(uiType, UIRootType.VeryFront, cancellationToken);
    }


    public void Close(UIType uiType)
    {
        CloseUI(uiType);
    }

    private void CacheRootDictionary()
    {
        _rootCacheDictionary.Clear();
        CacheUIRoot(UIRootType.Main, _mainRoot);
        CacheUIRoot(UIRootType.Content,_contentRoot);
        CacheUIRoot(UIRootType.Popup, _popupRoot);
        CacheUIRoot(UIRootType.VeryFront, _veryFrontRoot);
    }

    public async UniTask<UIBase> OpenUIAsync(UIType uiType, UIRootType uiRoot, CancellationToken cancellationToken)
    {
        if (!TryGetRootRectTransform(uiRoot, out RectTransform rootRectTransform))
        {
            Debug.LogError($"[{nameof(UIManager)}:{nameof(OpenUIAsync)}] '{uiRoot}'에 해당하는 Root RectTransform을 찾을 수 없습니다.");
            return null;
        }


        if (_loadingUISet.Contains(uiType))
        {
            Debug.LogWarning($"[{nameof(UIManager)}:{nameof(OpenUIAsync)}] '{uiType}' UI가 이미 로딩 중입니다.");
            return null;
        }

        if (_openedUISet.Contains(uiType))
        {
            UIBase cachedUI = _createdUICacheDictionary[uiType];
            return cachedUI;
        }

        _loadingUISet.Add(uiType);

        try
        {
            if (_createdUICacheDictionary.TryGetValue(uiType, out UIBase cachedUI))
            {
                SetCreatedUIRoot(cachedUI, rootRectTransform);
                OpenUI(uiType, cachedUI);
                return cachedUI;
            }

            UIBase createdUI = await CreateUIAsync(uiType, rootRectTransform, cancellationToken);

            if (createdUI == null)
            {
                Debug.LogError($"[{nameof(UIManager)}:{nameof(OpenUIAsync)}] '{uiType}' UI 생성에 실패하여 UI를 열 수 없습니다.");
                return null;
            }

            _createdUICacheDictionary[uiType] = createdUI;

            OpenUI(uiType, createdUI);
            return createdUI;
        }
        finally
        {
            _loadingUISet.Remove(uiType);
        }
    }

    private void OpenUI(UIType uiType, UIBase uiObject)
    {
        _openedUISet.Add(uiType);
        uiObject.gameObject.SetActive(true);
    }

    private async UniTask<UIBase> CreateUIAsync(UIType uiType, RectTransform rectTransform, CancellationToken cancellationToken)
    {
        string addressableKey = this.GetUIKey(uiType);
        GameObject uiPrefab = await ResourceManager.Inst.InstantiateAsync(addressableKey, rectTransform);

        if (uiPrefab == null)
        {
            Debug.LogError($"[{nameof(UIManager)}:{nameof(CreateUIAsync)}] '{uiType}' UI 프리팹을 로드하지 못했습니다.");
            return null;
        }

        if (!uiPrefab.TryGetComponent(out UIBase uiBase))
        {
            Debug.LogError($"[{nameof(UIManager)}:{nameof(CreateUIAsync)}] '{uiType}' UI 프리팹에서 UIBase 컴포넌트가 없습니다.");
            return uiBase;
        }

        return uiBase;
    }

    private void CloseUI(UIType uiType)
    {
        if (!_openedUISet.Contains(uiType))
        {
            Debug.LogWarning($"[{nameof(UIManager)}:{nameof(CloseUI)}] '{uiType}' UI가 열려 있지 않습니다.");
            return;
        }

        UIBase uiObject = _createdUICacheDictionary[uiType];

        _openedUISet.Remove(uiType);
        uiObject.gameObject.SetActive(false);
    }

    private bool TryGetRootRectTransform(UIRootType uiRoot, out RectTransform rectTransform)
    {
        bool hasRootRectTransform = _rootCacheDictionary.TryGetValue(uiRoot, out rectTransform);

        return hasRootRectTransform;
    }



    private void CacheUIRoot(UIRootType uiRoot, RectTransform rectTransform)
    {
        if (!_rootCacheDictionary.TryAdd(uiRoot, rectTransform))
        {
            Debug.LogError($"[{nameof(UIManager)}:{nameof(CacheUIRoot)}] UIRoot '{uiRoot}'가 이미 루트 캐시에 등록되어 있습니다.");
        }
    }

    private void SetCreatedUIRoot(UIBase uiObject, RectTransform rectTransform)
    {
        if (!uiObject.transform.IsChildOf(rectTransform))
        {
            uiObject.transform.SetParent(rectTransform);
        }
    }
  
}