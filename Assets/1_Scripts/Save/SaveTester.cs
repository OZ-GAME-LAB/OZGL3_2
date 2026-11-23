using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SaveTester : MonoBehaviour
{
    [SerializeField] private Button _saveButton;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void Awake()
    {
        if (_saveButton == null)
        {
            Debug.LogError("제작 UI 테스트 버튼을 연결해 주세요.", this);
            return;
        }

        _saveButton.onClick.AddListener(OnClickSave);
    }

    private void OnClickSave()
    {
        //SaveManager.Inst.TrySaveCurrentCharacter(out string error);
        CharacterSaveData selected = GameStartSelection.Character;

        if (selected == null || ServiceManager.Inst == null || SaveManager.Inst == null)
        {
            Debug.LogWarning("캐릭터로 입장한 뒤 저장해 주세요.", this);
            return;
        }

        InventoryService inventory = ServiceManager.Inst.InventoryService;

        if (inventory.TryCaptureSaveItems(out List<SavedItemData> items, out string error) == false)
        {
            // 실패 안내 후 종료 보류.
            Debug.LogError(error);
            return;
        }


        // 선택 데이터 자체를 바로 수정하지 않고 저장용 복사본 생성.
        CharacterSaveData snapshot = JsonUtility.FromJson<CharacterSaveData>(JsonUtility.ToJson(selected));

        snapshot.Items = items;

        NetworkRuneService runeService = ServiceManager.Inst.NetworkRuneService;

        if (runeService.TryCaptureEquippedRuneIds(out List<string> runeIds, out error) == false)
        {
            Debug.LogWarning(error, this);
            return;
        }

        snapshot.EquippedRuneIds = runeIds;

        // 장착 룬 목록도 최신 상태로 채운 다음 저장해야 함.
        if (SaveManager.Inst.SaveCharacter(snapshot, out error) == false)
        {
            Debug.LogError(error);
            return;
        }
    }
#else
    private void Awake()
    {
        if (_saveButton != null) _saveButton.gameObject.SetActive(false);
    }
#endif
}
