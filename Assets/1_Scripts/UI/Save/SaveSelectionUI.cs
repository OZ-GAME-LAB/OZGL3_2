using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SaveSelectionUI : UIBase
{
    [Header("안내")]
    [SerializeField] private TMP_Text Text_Title;
    [SerializeField] private TMP_Text Text_Message;

    [Header("저장 목록")]
    [SerializeField] private SaveSelectionSlotUI Prefab_SaveSlot;
    [SerializeField] private Transform Transform_SaveSlotRoot;
    [SerializeField] private ScrollRect Scroll_SaveList;

    [Header("새 데이터 생성")]
    [SerializeField] private TMP_InputField Input_NewName;
    [SerializeField] private Button Button_Create;

    [Header("선택 / 뒤로")]
    [SerializeField] private Button Button_Select;
    [SerializeField] private TMP_Text Text_SelectButton;
    [SerializeField] private Button Button_Back;

    [Header("현재 프로젝트의 고정 맵 식별자")]
    [SerializeField] private string _defaultMapId = "main_map";

    private readonly List<SaveSelectionSlotUI> _saveSlots = new();
    private readonly List<SaveSelectionSlotViewModel> _slotViewModels = new();
    private readonly List<WorldSaveData> _worlds = new();
    private readonly List<CharacterSaveData> _characters = new();

    private bool _configured;
    private bool _isHost;
    private bool _selectingWorld;

    private int _selectedIndex = -1;
    private ushort _port;
    private string _address;

    private WorldSaveData _selectedWorld;
    private Action _onConfirmed;

    private void Awake()
    {
        Button_Create.onClick.AddListener(OnCreate);
        Button_Select.onClick.AddListener(OnSelect);
        Button_Back.onClick.AddListener(OnBack);
    }

    private void OnDisable()
    {
        _configured = false;
        _onConfirmed = null;

        ClearSaveSlots();

        _worlds.Clear();
        _characters.Clear();
        _selectedWorld = null;
    }

    private void OnDestroy()
    {
        if (Button_Create != null)
            Button_Create.onClick.RemoveListener(OnCreate);

        if (Button_Select != null)
            Button_Select.onClick.RemoveListener(OnSelect);

        if (Button_Back != null)
            Button_Back.onClick.RemoveListener(OnBack);
    }

    public void BeginHost(ushort port, Action onConfirmed)
    {
        Begin(true, null, port, onConfirmed);
    }

    public void BeginClient(
        string address,
        ushort port,
        Action onConfirmed)
    {
        Begin(false, address, port, onConfirmed);
    }

    private void Begin(
        bool isHost,
        string address,
        ushort port,
        Action onConfirmed)
    {
        GameStartSelection.Clear();

        _configured = true;
        _isHost = isHost;
        _address = address;
        _port = port;
        _onConfirmed = onConfirmed;

        _selectedWorld = null;
        _selectingWorld = isHost;

        ShowCurrentStep();
    }

    private void ShowCurrentStep(string preferredId = null)
    {
        Text_Title.text = _selectingWorld ? "월드 선택" : "캐릭터 선택";

        Text_SelectButton.text = _selectingWorld ? "다음" : "선택 확정";

        Input_NewName.text = "";

        RefreshList(preferredId);
    }

    private void RefreshList(string preferredId = null)
    {
        ClearSaveSlots();

        _worlds.Clear();
        _characters.Clear();

        SaveManager saves = SaveManager.Inst;

        if (saves == null)
        {
            SetMessage("SaveManager가 없습니다.");
            RefreshButtons();
            return;
        }

        if (Prefab_SaveSlot == null || Transform_SaveSlotRoot == null)
        {
            SetMessage("저장 슬롯 프리팹과 생성 위치를 연결해 주세요.");
            RefreshButtons();
            return;
        }

        string warnings;

        if (_selectingWorld)
        {
            _worlds.AddRange(saves.GetWorldList(out warnings));

            foreach (WorldSaveData world in _worlds)
            {
                AddSaveSlot(world.WorldId, world.WorldName, FormatSavedTime(world.SavedAtUtc));
            }
        }
        else
        {
            _characters.AddRange(saves.GetCharacterList(out warnings));

            foreach (CharacterSaveData character in _characters)
            {
                AddSaveSlot(character.CharacterId, character.CharacterName, FormatSavedTime(character.SavedAtUtc));
            }
        }

        // 생성 직후에는 새 데이터를 선택.
        // 월드 선택으로 돌아왔을 때는 이전 선택을 복원.
        if (!string.IsNullOrEmpty(preferredId))
            OnSelectSaveSlot(preferredId);

        string message = _slotViewModels.Count == 0
            ? "저장된 데이터가 없습니다. 이름을 입력하고 생성해 주세요."
            : "사용할 슬롯을 선택하세요.";

        if (!string.IsNullOrEmpty(warnings))
        {
            Debug.LogWarning(warnings, this);

            message += "\n일부 파일의 복구 또는 읽기 문제가 있습니다. Console을 확인하세요.";
        }

        SetMessage(message);
        RefreshButtons();

        if (Scroll_SaveList != null)
        {
            Canvas.ForceUpdateCanvases();
            Scroll_SaveList.StopMovement();
            Scroll_SaveList.verticalNormalizedPosition = 1f;
        }
    }

    private void RefreshButtons()
    {
        bool ready = _configured && SaveManager.Inst != null;

        bool hasSelection =
            _selectedIndex >= 0 &&
            _selectedIndex < _slotViewModels.Count;

        Button_Select.interactable = ready && hasSelection;
        Button_Create.interactable = ready;
        Input_NewName.interactable = ready;
    }

    private void OnCreate()
    {
        if (!_configured || SaveManager.Inst == null)
            return;

        string newName = Input_NewName.text?.Trim();

        if (string.IsNullOrWhiteSpace(newName))
        {
            SetMessage("이름을 입력해 주세요.");
            return;
        }

        if (newName.Length > 24)
        {
            SetMessage("이름은 24자 이하로 입력해 주세요.");
            return;
        }

        if (_selectingWorld)
        {
            if (string.IsNullOrWhiteSpace(_defaultMapId))
            {
                SetMessage("Inspector의 기본 맵 ID를 설정해 주세요.");
                return;
            }

            WorldSaveData world = WorldSaveData.CreateNew(newName, _defaultMapId);

            if (SaveManager.Inst.SaveWorld(world, out string error) == false)
            {
                SetMessage(error);
                return;
            }

            RefreshList(world.WorldId);
        }
        else
        {
            CharacterSaveData character = CharacterSaveData.CreateNew(newName);

            if (SaveManager.Inst.SaveCharacter(character, out string error) == false)
            {
                SetMessage(error);
                return;
            }

            RefreshList(character.CharacterId);
        }
    }

    private void OnSelect()
    {
        if (!_configured || SaveManager.Inst == null)
            return;

        int index = _selectedIndex;

        if (_selectingWorld)
        {
            if (index < 0 || index >= _worlds.Count)
                return;

            SaveLoadResult result = SaveManager.Inst.LoadWorld(
                _worlds[index].WorldId,
                out WorldSaveData world,
                out string message);

            if (!IsLoadSuccess(result))
            {
                SetMessage(message);
                return;
            }

            if (result == SaveLoadResult.RecoveredFromBackup)
                Debug.LogWarning(message, this);

            _selectedWorld = world;
            _selectingWorld = false;

            ShowCurrentStep();
            return;
        }

        if (index < 0 || index >= _characters.Count)
            return;

        SaveLoadResult characterResult =
            SaveManager.Inst.LoadCharacter(
                _characters[index].CharacterId,
                out CharacterSaveData character,
                out string characterMessage);

        if (!IsLoadSuccess(characterResult))
        {
            SetMessage(characterMessage);
            return;
        }

        if (characterResult == SaveLoadResult.RecoveredFromBackup)
            Debug.LogWarning(characterMessage, this);

        if (_isHost)
        {
            if (_selectedWorld == null)
            {
                SetMessage("먼저 월드를 선택해 주세요.");
                return;
            }

            GameStartSelection.SetHost(
                _port, _selectedWorld, character);
        }
        else
        {
            GameStartSelection.SetClient(
                _address, _port, character);
        }

        Action callback = _onConfirmed;

        _configured = false;
        CloseWindow();

        callback?.Invoke();
    }

    private void OnBack()
    {
        // 호스트의 캐릭터 선택 단계라면 월드 선택으로 복귀.
        if (_configured && _isHost && !_selectingWorld)
        {
            string previousWorldId = _selectedWorld?.WorldId;

            _selectingWorld = true;
            _selectedWorld = null;

            ShowCurrentStep(previousWorldId);
            return;
        }

        GameStartSelection.Clear();
        CloseWindow();
    }

    private void CloseWindow()
    {
        if (UIManager.Inst != null)
            UIManager.Inst.Close(UIType.SaveSelectionUI);
    }

    private void SetMessage(string message)
    {
        if (Text_Message != null)
            Text_Message.text = message;
    }

    private static bool IsLoadSuccess(SaveLoadResult result)
    {
        return result == SaveLoadResult.Success
            || result == SaveLoadResult.RecoveredFromBackup;
    }

    private static string FormatSavedTime(string utcText)
    {
        if (!DateTimeOffset.TryParse(
                utcText, out DateTimeOffset savedTime))
        {
            return "저장 시각 없음";
        }

        return savedTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    }

    private void AddSaveSlot(
    string id,
    string displayName,
    string savedTime)
    {
        var viewModel = new SaveSelectionSlotViewModel(
            id,
            displayName,
            savedTime);

        SaveSelectionSlotUI slot = Instantiate(
            Prefab_SaveSlot,
            Transform_SaveSlotRoot);

        slot.Bind(viewModel, OnSelectSaveSlot);

        _slotViewModels.Add(viewModel);
        _saveSlots.Add(slot);
    }

    private void OnSelectSaveSlot(string saveId)
    {
        if (!_configured)
            return;

        int index = _slotViewModels.FindIndex(
            vm => vm.SaveId == saveId);

        if (index < 0)
            return;

        _selectedIndex = index;

        for (int i = 0; i < _slotViewModels.Count; i++)
        {
            _slotViewModels[i].IsSelected =
                i == _selectedIndex;
        }

        RefreshButtons();
    }

    private void ClearSaveSlots()
    {
        foreach (SaveSelectionSlotUI slot in _saveSlots)
        {
            if (slot == null)
                continue;

            slot.Unbind();

            // Destroy가 처리되기 전에도 레이아웃에서는 즉시 제외.
            slot.gameObject.SetActive(false);
            Destroy(slot.gameObject);
        }

        _saveSlots.Clear();
        _slotViewModels.Clear();
        _selectedIndex = -1;
    }
}