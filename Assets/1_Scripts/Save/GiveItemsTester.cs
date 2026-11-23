using UnityEngine;
using UnityEngine.UI;

public class GiveItemsTester : MonoBehaviour
{
    [SerializeField] private Button _giveItemButton;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void Awake()
    {
        if (_giveItemButton == null)
        {
            Debug.LogError("제작 UI 테스트 버튼을 연결해 주세요.", this);
            return;
        }

        _giveItemButton.onClick.AddListener(OnClickGiveItem);
    }

    private void OnClickGiveItem()
    {
        ServiceManager.Inst.GiveInventoryTestItems();
    }
#else
    private void Awake()
    {
        if (_giveItemButton != null) _giveItemButton.gameObject.SetActive(false);
    }
#endif
}
