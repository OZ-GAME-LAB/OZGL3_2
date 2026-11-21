using System;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 이미 배치하거나 조절한 프리팹은 덮어쓰지 않습니다. 씬 저장도 수행하지 않습니다.
public static class RestTentSetup
{
    public const string TentPath = "Assets/2_Prefabs/World/InteractionObjects/RestTent.prefab";
    public const string UIPath = "Assets/2_Prefabs/UI/RestUI.prefab";
    private const string UIAddress = "UI/RestUI";
    private const string SquarePath = "Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/Square.png";

    [MenuItem("Tools/Rest Tent/Create Missing Assets")]
    public static void CreateMissingAssets()
    {
        RequireEditMode();
        CreateTent();
        CreateUI();
        RegisterUI();
        ValidateAssets();
    }

    private static void RequireEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play Mode를 종료한 뒤 실행해 주세요.");
    }

    private static void CreateTent()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(TentPath) != null) return;
        Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
        if (square == null) throw new InvalidOperationException("2D Sprite 패키지의 Square가 없습니다.");
        int layer = LayerMask.NameToLayer("InteractionObject");
        if (layer < 0) throw new InvalidOperationException("InteractionObject 레이어가 없습니다.");
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject root = new GameObject("RestTent");
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, preview);
            root.layer = layer;
            BoxCollider2D area = root.AddComponent<BoxCollider2D>();
            area.isTrigger = true;
            area.size = new Vector2(4f, 3f);
            area.offset = Vector2.up;
            RestTent tent = root.AddComponent<RestTent>();
            SerializedObject data = new SerializedObject(tent);
            data.FindProperty("_area").objectReferenceValue = area;
            data.ApplyModifiedPropertiesWithoutUndo();
            GameObject visual = new GameObject("Visual", typeof(SpriteRenderer));
            SceneManager.MoveGameObjectToScene(visual, preview);
            visual.layer = layer;
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.up;
            visual.transform.localScale = new Vector3(3f, 2f, 1f);
            SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.color = new Color(0.35f, 0.55f, 0.42f, 1f);
            renderer.sortingOrder = 95;
            root.SetActive(true);
            if (PrefabUtility.SaveAsPrefabAsset(root, TentPath) == null)
                throw new InvalidOperationException("텐트 프리팹 저장에 실패했습니다.");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    private static void CreateUI()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(UIPath) != null) return;
        GameObject root = PrefabUtility.LoadPrefabContents("Assets/2_Prefabs/UI/GameOverUI.prefab");
        try
        {
            root.SetActive(false);
            root.name = "RestUI";
            GameOverUI old = root.GetComponent<GameOverUI>();
            if (old == null) throw new InvalidOperationException("기준 UI의 연결이 바뀌었습니다.");
            SerializedObject previous = new SerializedObject(old);
            Button cancel = previous.FindProperty("_respawnButton").objectReferenceValue as Button;
            TMP_Text message = previous.FindProperty("_messageText").objectReferenceValue as TMP_Text;
            if (cancel == null || message == null) throw new InvalidOperationException("기준 UI의 버튼/문구가 없습니다.");
            UnityEngine.Object.DestroyImmediate(old);
            RestUI rest = root.AddComponent<RestUI>();
            SerializedObject data = new SerializedObject(rest);
            data.FindProperty("_messageText").objectReferenceValue = message;
            data.FindProperty("_cancelButton").objectReferenceValue = cancel;
            data.ApplyModifiedPropertiesWithoutUndo();
            cancel.gameObject.name = "Cancel";
            cancel.onClick = new Button.ButtonClickedEvent();
            cancel.navigation = new Navigation { mode = Navigation.Mode.None };
            TMP_Text label = cancel.GetComponentInChildren<TMP_Text>(true);
            if (label == null) throw new InvalidOperationException("버튼 문구가 없습니다.");
            label.text = "휴식 취소";
            message.text = "모든 플레이어가 휴식해야 합니다 0/0";
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                if (text != label && text != message) text.text = "휴식하기";
            if (PrefabUtility.SaveAsPrefabAsset(root, UIPath) == null)
                throw new InvalidOperationException("휴식 UI 저장에 실패했습니다.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static AddressableAssetSettings GetSettings()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null || settings.DefaultGroup == null)
            throw new InvalidOperationException("기존 Addressables 기본 그룹이 없습니다.");
        return settings;
    }

    private static void RegisterUI()
    {
        AddressableAssetSettings settings = GetSettings();
        string guid = AssetDatabase.AssetPathToGUID(UIPath);
        CheckAddress(settings, guid);
        AddressableAssetEntry entry = settings.FindAssetEntry(guid);
        if (entry != null)
        {
            if (entry.address != UIAddress) throw new InvalidOperationException("기존 UI 주소를 확인해 주세요: " + entry.address);
            return;
        }
        entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
        entry.SetAddress(UIAddress);
        // 폰트/씬 등 다른 변경을 저장하지 않고 이번 등록 그룹만 저장합니다.
        EditorUtility.SetDirty(entry.parentGroup);
        AssetDatabase.SaveAssetIfDirty(entry.parentGroup);
    }

    private static void CheckAddress(AddressableAssetSettings settings, string guid)
    {
        if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("RestUI GUID가 없습니다.");
        foreach (AddressableAssetGroup group in settings.groups)
        {
            if (group == null) continue;
            foreach (AddressableAssetEntry entry in group.entries)
                if (entry.address == UIAddress && entry.guid != guid)
                    throw new InvalidOperationException("다른 에셋에 UI/RestUI 주소가 이미 등록되어 있습니다.");
        }
    }

    [MenuItem("Tools/Rest Tent/Validate Assets")]
    public static void ValidateAssets()
    {
        RequireEditMode();
        GameObject tent = AssetDatabase.LoadAssetAtPath<GameObject>(TentPath);
        GameObject ui = AssetDatabase.LoadAssetAtPath<GameObject>(UIPath);
        if (tent == null || ui == null) throw new InvalidOperationException("텐트/휴식 UI 프리팹이 없습니다.");
        RestTent rest = tent.GetComponent<RestTent>();
        BoxCollider2D area = tent.GetComponent<BoxCollider2D>();
        if (rest == null || area == null || !area.enabled || !area.isTrigger || !tent.activeSelf ||
            tent.layer != LayerMask.NameToLayer("InteractionObject") ||
            tent.GetComponentInChildren<NetworkObject>(true) != null ||
            new SerializedObject(rest).FindProperty("_area").objectReferenceValue != area)
            throw new InvalidOperationException("텐트의 컴포넌트/영역 연결을 확인해 주세요.");
        SpriteRenderer visual = tent.GetComponentInChildren<SpriteRenderer>(true);
        if (visual == null || visual.sprite == null) throw new InvalidOperationException("텐트 외형이 없습니다.");
        RestUI view = ui.GetComponent<RestUI>();
        if (view == null || ui.GetComponentInChildren<GameOverUI>(true) != null)
            throw new InvalidOperationException("휴식 UI에 올바른 스크립트가 필요합니다.");
        SerializedObject data = new SerializedObject(view);
        if (data.FindProperty("_messageText").objectReferenceValue == null ||
            data.FindProperty("_cancelButton").objectReferenceValue == null)
            throw new InvalidOperationException("휴식 UI 문구와 버튼을 연결해 주세요.");
        AddressableAssetSettings settings = GetSettings();
        string guid = AssetDatabase.AssetPathToGUID(UIPath);
        CheckAddress(settings, guid);
        AddressableAssetEntry entry = settings.FindAssetEntry(guid);
        if (entry == null || entry.address != UIAddress) throw new InvalidOperationException("UI/RestUI 등록이 없습니다.");
        Debug.Log("[RestTent] PASS: 프리팹, Square, UI 필드, Addressables 연결 검증. 실제 멀티플레이 검증은 별도입니다.");
    }
}
