using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class DataManager : MonoBehaviour
{
    public static DataManager Inst { get; private set; }

    private readonly Dictionary<Type, object> dataTableList = new();

    private static readonly Action<DataManager>[] FieldTables =
    {
        Prepare<ItemData>, Prepare<EquipmentData>, Prepare<RuneData>, Prepare<EffectData>,
        Prepare<CraftingRecipeData>, Prepare<FieldObjectData>, Prepare<DropGroupData>,
        Prepare<MonsterData>, Prepare<MonsterSkillData>, Prepare<ConsumableData>,
        Prepare<ScenarioData>, Prepare<DialogueLineData>
    };

    public async UniTask PrepareFieldTablesAsync(Action<float> progress, CancellationToken token)
    {
        for (int i = 0; i < FieldTables.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            FieldTables[i](this);
            progress((float)(i + 1) / FieldTables.Length);
            await UniTask.NextFrame(cancellationToken: token);
        }
    }

    private static void Prepare<T>(DataManager manager) where T : GameDataBase
    {
        manager.ReadData<T>();
        if (!manager.dataTableList.ContainsKey(typeof(T)))
            throw new InvalidOperationException($"{typeof(T).Name} 데이터를 불러오지 못했습니다.");
    }

    private void Awake()
    {
        Inst = this;
    }

    [Serializable]
    private class SerializationWrapper<T>
    {
        public List<T> items;
    }

    private Dictionary<string, T> LoadData<T>(string tableName) where T : GameDataBase
    {
        string resourcePath = $"JsonOutput/{tableName}";

        TextAsset textAsset = Resources.Load<TextAsset>(resourcePath);

        if (textAsset == null)
        {
            Debug.LogError($"[Error] 리소스를 찾을 수 없습니다 : Resources/{resourcePath}");
            return null;
        }

        try
        {
            string jsonString = textAsset.text;

            string wrappedJson = "{\"items\":" + jsonString + " }";
            SerializationWrapper<T> wrapper = JsonUtility.FromJson<SerializationWrapper<T>>(wrappedJson);

            if (wrapper != null && wrapper.items != null)
            {
                Debug.Log($"{typeof(T).Name} 데이터를 {wrapper.items.Count}개 로드했습니다.");
                return wrapper.items.ToDictionary(item => item.Id.ToString());
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{typeof(T).Name} JSON 로드 오류] {ex.Message}");
        }

        return null;
    }

    public Dictionary<string, T> ReadData<T>() where T : GameDataBase
    {
        Type type = typeof(T);

        if (!dataTableList.TryGetValue(type, out object cachedTable))
        {
            string tableName = type.Name;
            var table = LoadData<T>(tableName);
            if (table == null)
                return new Dictionary<string, T>();
            dataTableList.Add(type, table);
            return table;
        }

        return (Dictionary<string, T>)cachedTable;
    }

    public T GetData<T>(string id) where T : GameDataBase
    {
        var dataTable = ReadData<T>();
        if (dataTable.TryGetValue(id, out T data))
        {
            return data;
        }

        Debug.LogWarning($"[{typeof(T).Name}] 데이터가 존재하지 않습니다. ID: {id}");
        return null;
    }
}
