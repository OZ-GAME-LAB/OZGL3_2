using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>기존 테스트 UI와 석판을 재사용해 실제 맵의 로컬 퍼즐 연결을 구성합니다.</summary>
public static class SlidingPuzzleInteractionSetup
{
    public const string UIPath = "Assets/2_Prefabs/UI/MiniContents/SlidingPuzzleUI.prefab";
    public const string PlatePath = "Assets/2_Prefabs/World/InteractionObjects/AncientPlate.prefab";

    [MenuItem("Tools/Sliding Puzzle/Connect Existing Plate")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play Mode를 종료한 뒤 실행해 주세요.");
        CreateUIIfMissing();
        ConnectPlate();
        RegisterUI();
        AssetDatabase.SaveAssets();
        Debug.Log("[SlidingPuzzle] Existing plate connected; local completion only.");
    }

    private static void CreateUIIfMissing()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(UIPath) != null)
            return;

        Scene scene = SceneManager.GetSceneByPath(SlidingPuzzleTestSceneBuilder.ScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere)
            scene = EditorSceneManager.OpenScene(SlidingPuzzleTestSceneBuilder.ScenePath, OpenSceneMode.Additive);
        GameObject copy = null;
        try
        {
            SlidingPuzzleUI source = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                source = root.GetComponentInChildren<SlidingPuzzleUI>(true);
                if (source != null)
                    break;
            }
            if (source == null)
                throw new InvalidOperationException("기존 테스트 씬에 SlidingPuzzleUI가 없습니다.");
            copy = UnityEngine.Object.Instantiate(source.gameObject);
            copy.name = "SlidingPuzzleUI";
            copy.SetActive(false);
            RectTransform rect = copy.GetComponent<RectTransform>();
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
            Directory.CreateDirectory(Path.GetDirectoryName(UIPath));
            PrefabUtility.SaveAsPrefabAsset(copy, UIPath);
        }
        finally
        {
            if (copy != null)
                UnityEngine.Object.DestroyImmediate(copy);
            if (openedHere)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void ConnectPlate()
    {
        GameObject plate = PrefabUtility.LoadPrefabContents(PlatePath);
        try
        {
            Collider2D area = plate.GetComponent<Collider2D>();
            if (area == null || !area.isTrigger)
                throw new InvalidOperationException("기존 석판의 Trigger Collider를 확인해 주세요.");
            SlidingPuzzleInteractable interaction = plate.GetComponent<SlidingPuzzleInteractable>();
            if (interaction == null)
                interaction = plate.AddComponent<SlidingPuzzleInteractable>();
            GameObject marker = null;
            foreach (Transform child in plate.GetComponentsInChildren<Transform>(true))
            {
                if (child != plate.transform && child.name == "AncientPlate_Activate")
                {
                    marker = child.gameObject;
                    break;
                }
            }
            if (marker == null)
                throw new InvalidOperationException("기존 완료 표시 자식을 찾지 못했습니다.");
            SerializedObject serialized = new SerializedObject(interaction);
            serialized.FindProperty("_activateOnComplete").objectReferenceValue = marker;
            serialized.FindProperty("_interactionArea").objectReferenceValue = area;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(plate, PlatePath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(plate);
        }
    }

    private static void RegisterUI()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        AddressableAssetGroup group = settings.DefaultGroup;
        if (group == null)
            group = settings.CreateGroup("Local UI", false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
        Register(settings, group, UIPath, "UI/SlidingPuzzleUI");
        Register(settings, group, "Assets/2_Prefabs/UI/MiniContents/InteractableInfoUI.prefab", "UI/InteractableInfoUI");
    }

    private static void Register(AddressableAssetSettings settings, AddressableAssetGroup group, string path, string address)
    {
        string guid = AssetDatabase.AssetPathToGUID(path);
        if (string.IsNullOrEmpty(guid))
            throw new InvalidOperationException("UI 프리팹이 없습니다: " + path);
        AddressableAssetEntry existing = settings.FindAssetEntry(guid);
        AddressableAssetEntry entry = existing ?? settings.CreateOrMoveEntry(guid, group);
        entry.SetAddress(address);
    }
}
