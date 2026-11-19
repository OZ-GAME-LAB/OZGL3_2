using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.IO;
using System.Text;
using UnityEngine;

public enum SaveLoadResult
{
    Success,
    RecoveredFromBackup,
    NotFound,
    Failed
}

public class SaveManager : MonoBehaviour
{
    public static SaveManager Inst { get; private set; }

    public const int CurrentVersion = 1;

    private string _saveDirectory;
    public string SaveDirectory => _saveDirectory;
    private CharacterSaveData _confirmedRuneCharacter;
    private List<string> _lastConfirmedRuneIds;

    private string WorldDirectory => Path.Combine(_saveDirectory, "Worlds");
    private string CharacterDirectory => Path.Combine(_saveDirectory, "Characters");

    private void Awake()
    {
        if (Inst != null && Inst != this)
        {
            Destroy(gameObject);
            return;
        }

        Inst = this;
        DontDestroyOnLoad(gameObject);

        _saveDirectory = Path.Combine(Application.persistentDataPath, "Saves");
    }

    private void OnDestroy()
    {
        if (Inst == this)
        {
            Inst = null;
        }
    }

    public bool TrySaveCurrentCharacter(out string error)
    {
        error = null;

        CharacterSaveData character = GameStartSelection.Character;
        ServiceManager services = ServiceManager.Inst;

        if (character == null || services == null)
        {
            error = "저장할 캐릭터 또는 서비스가 없습니다.";
            return false;
        }

        if (services.InventoryService.TryCaptureSaveItems(out var items, out error) == false)
        {
            return false;
        }

        if (services.NetworkRuneService.TryCaptureEquippedRuneIds(out var runeIds, out error) == false)
        {
            return false;
        }

        CharacterSaveData snapshot = JsonUtility.FromJson<CharacterSaveData>(JsonUtility.ToJson(character));
        snapshot.Items = items;
        snapshot.EquippedRuneIds = runeIds;

        return SaveCharacter(snapshot, out error);
    }

    public bool SaveWorld(WorldSaveData data, out string error, bool createOnly = false)
    {
        return SaveFile(
            WorldDirectory,
            data?.WorldId,
            data?.WorldName,
            data,
            createOnly,
            ValidateWorld,
            value => value.SavedAtUtc = DateTime.UtcNow.ToString("O"),
            out error);
    }

    public bool SaveCharacter(CharacterSaveData data, out string error, bool createOnly = false)
    {
        return SaveFile(
            CharacterDirectory,
            data?.CharacterId,
            data?.CharacterName,
            data,
            createOnly,
            ValidateCharacter,
            value => value.SavedAtUtc = DateTime.UtcNow.ToString("O"),
            out error);
    }

    public SaveLoadResult LoadWorld(WorldSaveData slot, out WorldSaveData data, out string message)
    {
        data = null;

        try
        {
            string path = GetSlotPath(WorldDirectory, slot.WorldId, slot.WorldName);
            return LoadFileAtPath(path, slot.WorldId, ValidateWorld, out data, out message);
        }
        catch (Exception exception)
        {
            message = exception.Message;
            return SaveLoadResult.Failed;
        }
    }

    public SaveLoadResult LoadCharacter(CharacterSaveData slot, out CharacterSaveData data, out string message)
    {
        data = null;

        try
        {
            string path = GetSlotPath(CharacterDirectory, slot.CharacterId, slot.CharacterName);
            return LoadFileAtPath(path, slot.CharacterId, ValidateCharacter, out data, out message);
        }
        catch (Exception exception)
        {
            message = exception.Message;
            return SaveLoadResult.Failed;
        }
    }

    public List<WorldSaveData> GetWorldList(out string warnings)
    {
        List<WorldSaveData> list = ReadList<WorldSaveData>(WorldDirectory, world => world.WorldId, world => world.WorldName, ValidateWorld, out warnings);

        list.Sort((a, b) => string.CompareOrdinal(b.SavedAtUtc, a.SavedAtUtc));

        return list;
    }

    public List<CharacterSaveData> GetCharacterList(out string warnings)
    {
        List<CharacterSaveData> list = ReadList<CharacterSaveData>(CharacterDirectory, character => character.CharacterId, character => character.CharacterName, ValidateCharacter, out warnings);

        list.Sort((a, b) => string.CompareOrdinal(b.SavedAtUtc, a.SavedAtUtc));

        return list;
    }

    private bool SaveFile<T>(
        string directory,
        string id,
        string name,
        T data,
        bool createOnly,
        Action<T, string> validate,
        Action<T> stampTime,
        out string error) where T : class
    {
        error = null;
        string tempPath = null;

        try
        {
            ValidateId(id);
            validate(data, id);

            Directory.CreateDirectory(directory);

            string path = GetSlotPath(directory, id, name);
            string backupPath = path + ".bak";

            if (createOnly && HasSaveFile(path))
            {
                error = "같은 이름의 저장 데이터가 이미 있습니다.";
                return false;
            }

            bool currentFileValid = false;

            if (HasSaveFile(path))
            {
                // 같은 이름이더라도 기존 파일의 내부 ID가 이번 슬롯과 일치해야 한다.
                // 본 파일이 손상됐다면 기존 백업으로 소유 슬롯을 확인한다.
                SaveLoadResult result = LoadFileAtPath(path, id, validate, out T existing, out string message);

                if (result != SaveLoadResult.Success && result != SaveLoadResult.RecoveredFromBackup)
                {
                    error = $"기존 파일이 같은 슬롯인지 확인할 수 없어 덮어쓰지 않았습니다.\n{message}";
                    return false;
                }

                currentFileValid = result == SaveLoadResult.Success;
            }

            T snapshot = JsonUtility.FromJson<T>(JsonUtility.ToJson(data));

            stampTime(snapshot);

            tempPath = path + ".tmp";

            File.WriteAllText(tempPath, JsonUtility.ToJson(snapshot, true), new UTF8Encoding(false));

            ReadFile(tempPath, id, validate);

            if (File.Exists(path))
            {
                File.Replace(tempPath, path, currentFileValid ? backupPath : null);
            }
            else
            {
                File.Move(tempPath, path);
            }

            return true;
        }
        catch (Exception exception)
        {
            error = $"저장 실패: {exception.Message}";
            Debug.LogException(exception, this);
            return false;
        }
        finally
        {
            if (tempPath != null)
            {
                TryDeleteTemporaryFile(tempPath);
            }
        }
    }

    private SaveLoadResult LoadFileAtPath<T>(
        string path,
        string id,
        Action<T, string> validate,
        out T data,
        out string message) where T : class
    {
        data = null;
        message = null;

        try
        {
            ValidateId(id);

            string backupPath = path + ".bak";

            if (File.Exists(path) == false && File.Exists(backupPath) == false)
            {
                message = "저장 파일이 없습니다.";
                return SaveLoadResult.NotFound;
            }

            string mainError;

            try
            {
                data = ReadFile(path, id, validate);
                message = "불러오기 완료.";
                return SaveLoadResult.Success;
            }
            catch (NotSupportedException)
            {
                throw;
            }
            catch (Exception exception)
            {
                mainError = exception.Message;
            }

            try
            {
                data = ReadFile(backupPath, id, validate);
                message = "본 파일 대신 이전 백업을 불러왔습니다.";
                return SaveLoadResult.RecoveredFromBackup;
            }
            catch (Exception exception)
            {
                message = $"본 파일: {mainError}\n백업: {exception.Message}";

                return SaveLoadResult.Failed;
            }
        }
        catch (Exception exception)
        {
            message = exception.Message;
            return SaveLoadResult.Failed;
        }
    }

    private List<T> ReadList<T>(string directory, Func<T, string> getId, Func<T, string> getName, Action<T, string> validate, out string warnings) where T : class
    {
        var result = new List<T>();
        var errors = new List<string>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ids = new HashSet<string>(StringComparer.Ordinal);

        try
        {
            if (Directory.Exists(directory))
            {
                foreach (string path in Directory.GetFiles(directory, "*.json"))
                {
                    paths.Add(path);
                }

                foreach (string backup in Directory.GetFiles(directory, "*.json.bak"))
                {
                    paths.Add(Path.ChangeExtension(backup, null));
                }

                foreach (string path in paths)
                {
                    string stem = Path.GetFileNameWithoutExtension(path);
                    string lastError = null;
                    bool loaded = false;

                    foreach (string candidate in new[] { path, path + ".bak" })
                    {
                        if (File.Exists(candidate) == false)
                        {
                            continue;
                        }

                        try
                        {
                            T item = JsonUtility.FromJson<T>(File.ReadAllText(candidate, Encoding.UTF8));

                            if (item == null)
                            {
                                throw new InvalidDataException("저장 데이터가 비어 있습니다.");
                            }

                            string id = getId(item);
                            ValidateId(id);
                            validate(item, id);

                            bool isOldFile = string.Equals(stem, id, StringComparison.Ordinal);
                            bool isNamedFile = string.Equals(stem, getName(item), StringComparison.OrdinalIgnoreCase);

                            if (isOldFile == false && isNamedFile == false)
                            {
                                throw new InvalidDataException("파일명과 저장 데이터 이름이 일치하지 않습니다.");
                            }

                            if (ids.Add(id))
                            {
                                result.Add(item);
                            }
                            else
                            {
                                errors.Add($"{stem}: 같은 ID의 파일이 또 있습니다.");
                            }

                            if (candidate != path)
                            {
                                errors.Add($"{stem}: 백업에서 불러왔습니다.");
                            }

                            loaded = true;
                            break;
                        }
                        catch (NotSupportedException exception)
                        {
                            // 최신 버전의 본 파일을 오래된 백업으로 대체하지 않는다.
                            lastError = exception.Message;
                            break;
                        }
                        catch (Exception exception)
                        {
                            lastError = exception.Message;
                        }
                    }

                    if (loaded == false)
                    {
                        errors.Add($"{stem}: {lastError ?? "읽을 수 있는 파일이 없습니다."}");
                    }
                }
            }
        }
        catch (Exception exception)
        {
            errors.Add(exception.Message);
        }

        warnings = string.Join("\n", errors);
        return result;
    }

    private static T ReadFile<T>(string path, string expectedId, Action<T, string> validate) where T : class
    {
        string json = File.ReadAllText(path, Encoding.UTF8);

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException("파일이 비어 있습니다.");
        }

        T data = JsonUtility.FromJson<T>(json);
        validate(data, expectedId);

        return data;
    }

    private static void ValidateId(string id)
    {
        if (Guid.TryParseExact(id, "N", out _) == false)
        {
            throw new InvalidDataException("저장 ID 형식이 올바르지 않습니다.");
        }
    }

    private static void ValidateWorld(WorldSaveData data, string expectedId)
    {
        if (data == null)
        {
            throw new InvalidDataException("월드 데이터가 없습니다.");
        }

        if (data.Version != CurrentVersion)
        {
            throw new NotSupportedException($"지원하지 않는 월드 버전입니다: {data.Version}");
        }

        if (data.WorldId != expectedId ||
            string.IsNullOrWhiteSpace(data.WorldName) ||
            string.IsNullOrWhiteSpace(data.MapId) ||
            data.ProgressIds == null)
        {
            throw new InvalidDataException("월드 데이터가 올바르지 않습니다.");
        }
    }

    private static void ValidateCharacter(CharacterSaveData data, string expectedId)
    {
        if (data == null)
        {
            throw new InvalidDataException("캐릭터 데이터가 없습니다.");
        }

        if (data.Version != CurrentVersion)
        {
            throw new NotSupportedException($"지원하지 않는 캐릭터 버전입니다: {data.Version}");
        }

        if (data.CharacterId != expectedId ||
            string.IsNullOrWhiteSpace(data.CharacterName) ||
            data.Items == null ||
            data.EquippedRuneIds == null ||
            data.EquippedRuneIds.Count != 3 ||
            data.ProgressIds == null)
        {
            throw new InvalidDataException("캐릭터 데이터가 올바르지 않습니다.");
        }

        var instanceIds = new HashSet<string>();
        var equipmentSlots = new HashSet<EquipmentSlot>();

        foreach (SavedItemData item in data.Items)
        {
            if (item == null ||
                string.IsNullOrWhiteSpace(item.InstanceId) ||
                string.IsNullOrWhiteSpace(item.ItemId) ||
                item.Count <= 0 ||
                !instanceIds.Add(item.InstanceId))
            {
                throw new InvalidDataException("아이템 데이터가 올바르지 않습니다.");
            }

            if (Enum.IsDefined(typeof(EquipmentSlot), item.EquippedSlot) == false)
            {
                throw new InvalidDataException("알 수 없는 장비 슬롯입니다.");
            }

            if (item.EquippedSlot != EquipmentSlot.None &&
                (item.Count != 1 || !equipmentSlots.Add(item.EquippedSlot)))
            {
                throw new InvalidDataException("장비 수량 또는 슬롯이 올바르지 않습니다.");
            }
        }
    }

    public static bool TryValidateSaveName(string name, out string error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(name) || name.Length > 24)
        {
            error = "이름은 1~24자로 입력해 주세요.";
            return false;
        }

        if (name != name.Trim() || name.EndsWith(".", StringComparison.Ordinal))
        {
            error = "이름의 앞뒤에 공백을 넣거나 마지막에 마침표를 넣을 수 없습니다.";
            return false;
        }

        foreach (char c in name)
        {
            if (char.IsControl(c) || "<>:\"/\\|?*".IndexOf(c) >= 0)
            {
                error = "이름에는 다음 문자를 사용할 수 없습니다: < > : \" / \\ | ? *";
                return false;
            }
        }

        string firstPart = name.Split('.')[0].TrimEnd().ToUpperInvariant();

        if (firstPart == "CON" || firstPart == "PRN" || firstPart == "AUX" || firstPart == "NUL" ||
            System.Text.RegularExpressions.Regex.IsMatch(firstPart, @"^(COM|LPT)[1-9¹²³]$"))
        {
            error = "파일 이름으로 사용할 수 없는 이름입니다.";
            return false;
        }

        return true;
    }

    private static void TryDeleteTemporaryFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"임시 파일 정리 실패: {exception.Message}");
        }
    }

    public void ClearConfirmedRunes()
    {
        _confirmedRuneCharacter = null;
        _lastConfirmedRuneIds = null;
    }

    public void RememberConfirmedRunes(string first, string second, string third)
    {
        _confirmedRuneCharacter = GameStartSelection.Character;
        _lastConfirmedRuneIds = new List<string> { first ?? "", second ?? "", third ?? "" };
    }

    public bool TrySaveDisconnectedCharacter(out string error)
    {
        CharacterSaveData character = GameStartSelection.Character;
        InventoryService inventory = ServiceManager.Inst?.InventoryService;

        if (character == null || inventory == null ||
            ReferenceEquals(character, _confirmedRuneCharacter) == false || _lastConfirmedRuneIds == null)
        {
            error = "이번 입장에서 확인된 캐릭터와 룬 정보가 없어 기존 저장 파일을 유지합니다.";
            return false;
        }

        if (inventory.TryCaptureSaveItems(out var items, out error) == false)
        {
            return false;
        }

        CharacterSaveData snapshot = JsonUtility.FromJson<CharacterSaveData>(JsonUtility.ToJson(character));
        snapshot.Items = items;
        snapshot.EquippedRuneIds = new List<string>(_lastConfirmedRuneIds);

        return SaveCharacter(snapshot, out error);
    }

    private static bool HasSaveFile(string path)
    {
        return File.Exists(path) || File.Exists(path + ".bak");
    }

    private static string GetSlotPath(string directory, string id, string name)
    {
        ValidateId(id);

        string oldPath = Path.Combine(directory, id + ".json");

        if (HasSaveFile(oldPath))
        {
            return oldPath;
        }

        if (TryValidateSaveName(name, out string error) == false)
        {
            throw new InvalidDataException(error);
        }

        return Path.Combine(directory, name + ".json");
    }

    public bool DeleteWorld(WorldSaveData slot, out string error)
    {
        return DeleteSlot<WorldSaveData>(WorldDirectory, slot.WorldId, slot.WorldName, ValidateWorld, out error);
    }

    public bool DeleteCharacter(CharacterSaveData slot, out string error)
    {
        return DeleteSlot<CharacterSaveData>(CharacterDirectory, slot.CharacterId, slot.CharacterName, ValidateCharacter, out error);
    }

    private bool DeleteSlot<T>(string directory, string id, string name, Action<T, string> validate, out string error) where T : class
    {
        error = null;

        try
        {
            string path = GetSlotPath(directory, id, name);
            SaveLoadResult status = LoadFileAtPath(path, id, validate, out T data, out string message);

            if (status != SaveLoadResult.Success && status != SaveLoadResult.RecoveredFromBackup)
            {
                error = $"선택한 슬롯을 확인할 수 없어 삭제하지 않았습니다.\n{message}";
                return false;
            }

            File.Delete(path + ".tmp");
            File.Delete(path + ".bak");
            File.Delete(path);
            return true;
        }
        catch (Exception exception)
        {
            error = $"삭제 실패: {exception.Message}";
            return false;
        }
    }
}