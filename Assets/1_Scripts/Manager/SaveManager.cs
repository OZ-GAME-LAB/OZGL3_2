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
    public const int SlotCount = 3;

    private string _saveDirectory;

    public string SaveDirectory => _saveDirectory;

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

    public bool TrySave(int slot, SaveData data, out string error)
    {
        error = null;

        if (IsValidSlot(slot) == false)
        {
            error = "저장 슬롯은 0~2 범위로 지정하세요.";
            return false;
        }

        if (Validate(data, out error) == false)
        {
            return false;
        }

        string path = GetSavePath(slot);
        string tempPath = path + ".tmp";
        string backupPath = path + ".bak";

        try
        {
            Directory.CreateDirectory(_saveDirectory);

            // 호출자가 전달한 객체의 저장 시각은 직접 변경하지 않음.
            string sourceJson = JsonUtility.ToJson(data);
            SaveData snapshot = JsonUtility.FromJson<SaveData>(sourceJson);

            snapshot.SavedAtUtc = DateTime.UtcNow.ToString("O");

            string json = JsonUtility.ToJson(snapshot, true);

            File.WriteAllText(tempPath, json, new UTF8Encoding(false));

            // 작성된 임시 파일도 다시 읽어 검사.
            if (TryReadFile(tempPath, out _, out error) == false)
            {
                return false;
            }

            if (File.Exists(path))
            {
                // 정상적인 기존 파일만 새로운 백업으로 남김.
                // 기존 본 파일이 손상됐다면 이전 백업은 보존.
                bool currentFileValid = TryReadFile(path, out _, out _);

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
            TryDeleteTemporaryFile(tempPath);
        }
    }

    public SaveLoadResult Load(int slot, out SaveData data, out string message)
    {
        data = null;
        message = null;

        if (IsValidSlot(slot) == false)
        {
            message = "저장 슬롯은 0~2 범위로 지정하세요.";
            return SaveLoadResult.Failed;
        }

        string path = GetSavePath(slot);
        string backupPath = path + ".bak";

        if (File.Exists(path) == false && File.Exists(backupPath) == false)
        {
            message = "저장된 데이터가 없습니다.";
            return SaveLoadResult.NotFound;
        }

        if (TryReadFile(path, out data, out string mainError))
        {
            message = "불러오기 완료.";
            return SaveLoadResult.Success;
        }

        if (TryReadFile(backupPath, out data, out string backupError))
        {
            message = "본 파일을 읽지 못해 이전 백업을 불러왔습니다.";
            return SaveLoadResult.RecoveredFromBackup;
        }

        message = $"불러오기 실패.\n본 파일: {mainError}\n백업: {backupError}";

        return SaveLoadResult.Failed;
    }

    private bool TryReadFile(string path, out SaveData data, out string error)
    {
        data = null;
        error = null;

        try
        {
            if (File.Exists(path) == false)
            {
                error = "파일이 없습니다.";
                return false;
            }

            string json = File.ReadAllText(path, Encoding.UTF8);

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "파일 내용이 비어 있습니다.";
                return false;
            }

            SaveData loaded = JsonUtility.FromJson<SaveData>(json);

            if (Validate(loaded, out error) == false)
            {
                return false;
            }

            data = loaded;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static bool Validate(SaveData data, out string error)
    {
        error = null;

        if (data == null)
        {
            error = "저장 데이터가 없습니다.";
            return false;
        }

        if (data.Version != CurrentVersion)
        {
            error = $"지원하지 않는 저장 버전입니다: {data.Version}";
            return false;
        }

        if (data.Items == null || data.EquippedRuneIds == null || data.ProgressIds == null)
        {
            error = "필수 데이터 목록이 없습니다.";
            return false;
        }

        if (data.EquippedRuneIds.Count != 3)
        {
            error = "룬 슬롯 개수가 올바르지 않습니다.";
            return false;
        }

        var instanceIds = new HashSet<string>();
        var equipmentSlots = new HashSet<EquipmentSlot>();

        foreach (SavedItemData item in data.Items)
        {
            if (item == null ||
                string.IsNullOrWhiteSpace(item.InstanceId) ||
                string.IsNullOrWhiteSpace(item.ItemId) ||
                item.Count <= 0)
            {
                error = "아이템 저장 데이터가 올바르지 않습니다.";
                return false;
            }

            if (instanceIds.Add(item.InstanceId) == false)
            {
                error = $"중복된 아이템 식별자입니다: {item.InstanceId}";
                return false;
            }

            if (Enum.IsDefined(typeof(EquipmentSlot), item.EquippedSlot) == false)
            {
                error = "알 수 없는 장비 슬롯입니다.";
                return false;
            }

            if (item.EquippedSlot == EquipmentSlot.None)
            {
                continue;
            }

            if (item.Count != 1 || equipmentSlots.Add(item.EquippedSlot) == false)
            {
                error = "장비 수량 또는 장비 슬롯이 중복되었습니다.";
                return false;
            }
        }

        return true;
    }

    private string GetSavePath(int slot)
    {
        return Path.Combine(_saveDirectory, $"slot_{slot}.json");
    }

    private static bool IsValidSlot(int slot)
    {
        return slot >= 0 && slot < SlotCount;
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
            Debug.LogWarning($"임시 저장 파일 정리 실패: {exception.Message}");
        }
    }
}