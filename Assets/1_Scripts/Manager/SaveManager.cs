using System;
using System.Collections.Generic;
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

    public bool SaveWorld(WorldSaveData data, out string error)
    {
        return SaveFile(
            WorldDirectory,
            data?.WorldId,
            data,
            ValidateWorld,
            value => value.SavedAtUtc = DateTime.UtcNow.ToString("O"),
            out error);
    }

    public bool SaveCharacter(CharacterSaveData data, out string error)
    {
        return SaveFile(
            CharacterDirectory,
            data?.CharacterId,
            data,
            ValidateCharacter,
            value => value.SavedAtUtc = DateTime.UtcNow.ToString("O"),
            out error);
    }

    public SaveLoadResult LoadWorld(string id, out WorldSaveData data, out string message)
    {
        return LoadFile(WorldDirectory, id, ValidateWorld, out data, out message);
    }

    public SaveLoadResult LoadCharacter(string id, out CharacterSaveData data, out string message)
    {
        return LoadFile(CharacterDirectory, id, ValidateCharacter, out data, out message);
    }

    public List<WorldSaveData> GetWorldList(out string warnings)
    {
        List<WorldSaveData> list = ReadList<WorldSaveData>(WorldDirectory, ValidateWorld, out warnings);

        list.Sort((a, b) => string.CompareOrdinal(b.SavedAtUtc, a.SavedAtUtc));

        return list;
    }

    public List<CharacterSaveData> GetCharacterList(out string warnings)
    {
        List<CharacterSaveData> list = ReadList<CharacterSaveData>(CharacterDirectory, ValidateCharacter, out warnings);

        list.Sort((a, b) => string.CompareOrdinal(b.SavedAtUtc, a.SavedAtUtc));

        return list;
    }

    private bool SaveFile<T>(
        string directory,
        string id,
        T data,
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

            string path = Path.Combine(directory, id + ".json");
            string backupPath = path + ".bak";
            tempPath = path + ".tmp";

            // 현재 본 파일이 다른 버전이면 덮어쓰지 않는다.
            bool currentFileValid = false;

            if (File.Exists(path))
            {
                try
                {
                    ReadFile(path, id, validate);
                    currentFileValid = true;
                }
                catch (NotSupportedException)
                {
                    throw;
                }
                catch
                {
                    // 손상된 본 파일을 백업으로 덮어쓰지 않음.
                    currentFileValid = false;
                }
            }

            T snapshot = JsonUtility.FromJson<T>(JsonUtility.ToJson(data));

            stampTime(snapshot);

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

    private SaveLoadResult LoadFile<T>(
        string directory,
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

            string path = Path.Combine(directory, id + ".json");
            string backupPath = path + ".bak";

            if (File.Exists(path) && !File.Exists(backupPath) == false)
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

    private List<T> ReadList<T>(string directory, Action<T, string> validate, out string warnings) where T : class
    {
        var result = new List<T>();
        var errors = new List<string>();
        var ids = new HashSet<string>();

        try
        {
            if (Directory.Exists(directory))
            {
                foreach (string path in Directory.GetFiles(directory, "*.json"))
                {
                    ids.Add(Path.GetFileNameWithoutExtension(path));
                }

                // 본 파일이 없어도 백업에서 목록을 복구할 수 있음.
                foreach (string path in Directory.GetFiles(directory, "*.json.bak"))
                {
                    string jsonFileName = Path.GetFileNameWithoutExtension(path);
                    ids.Add(Path.GetFileNameWithoutExtension(jsonFileName));
                }

                foreach (string id in ids)
                {
                    SaveLoadResult status = LoadFile(directory, id, validate, out T data, out string message);

                    if (status == SaveLoadResult.Success || status == SaveLoadResult.RecoveredFromBackup)
                    {
                        result.Add(data);
                    }

                    if (status != SaveLoadResult.Success)
                    {
                        errors.Add($"{id}: {message}");
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

    private static void ValidateCharacter(
        CharacterSaveData data,
        string expectedId)
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
}