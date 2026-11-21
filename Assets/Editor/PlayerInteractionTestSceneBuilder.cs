using System.IO;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public static class PlayerInteractionTestSceneBuilder
{
    public const string ScenePath = "Assets/0_Scenes/Tests/PlayerInteractionTest.unity";
    private const string PlatformRoot = "Assets/2_Prefabs/World/Platforms/";

    [MenuItem("Tools/Player Interaction/Create Test Scene")]
    public static void CreateScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        if (File.Exists(ScenePath))
        {
            EditorSceneManager.OpenScene(ScenePath);
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject managers = new GameObject("Test Managers");
        NetworkManager network = Instance("Assets/2_Prefabs/Manager/NetworkManager.prefab", null)
            .GetComponent<NetworkManager>();
        Object.DestroyImmediate(network.GetComponent<NetCodeNetworkManager>());
        Instance("Assets/2_Prefabs/Manager/DataManager.prefab", managers.transform);
        Instance("Assets/2_Prefabs/Manager/ResourceManager.prefab", managers.transform);
        WorldManager world = Instance("Assets/2_Prefabs/Manager/WorldManager.prefab", managers.transform).GetComponent<WorldManager>();
        ObjectManager objects = Instance("Assets/2_Prefabs/Manager/ObjectManager.prefab", managers.transform).GetComponent<ObjectManager>();
        ServiceManager services = new GameObject("ServiceManager").AddComponent<ServiceManager>();
        services.transform.SetParent(managers.transform);
        Set(objects, "_netcodeManager", network);
        Set(objects, "Prefab_Player", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/2_Prefabs/Player/Player.prefab").GetComponent<NetworkObject>());

        GameObject ui = Instance("Assets/2_Prefabs/Manager/UIManager.prefab", managers.transform);
        foreach (CanvasScaler scaler in ui.GetComponentsInChildren<CanvasScaler>(true))
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule))
            .GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

        Camera camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener),
            typeof(CinemachineBrain), typeof(InteractionTestViewport)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.orthographic = true;
        camera.orthographicSize = 16.875f;
        camera.aspect = 16f / 9f;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(20, 28, 38, 255);
        Light2D light = new GameObject("Global Light 2D").AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.intensity = 1f;

        GameObject course = new GameObject("Jump Course");
        CreateSolid("Floor", new Vector2(0, -14), new Vector2(58, 2), course.transform);
        CreateSolid("Left Boundary", new Vector2(-29, 0), new Vector2(1, 32), course.transform);
        CreateSolid("Right Boundary", new Vector2(29, 0), new Vector2(1, 32), course.transform);
        Platform("ThinPlatform/ThinPlatform_S_4x0_5.prefab", "01 Thin", -8, -10, course.transform);
        Platform("Ground/GroundPlatform_S_4x2.prefab", "02 Thick", -1, -7, course.transform);
        Platform("ThinPlatform/ThinPlatform_S_4x0_5.prefab", "03 Thin", 6, -4, course.transform);
        Platform("Ground/GroundPlatform_S_4x2.prefab", "04 Thick", 13, -1, course.transform);
        Platform("ThinPlatform/ThinPlatform_M_8x0_25.prefab", "05 Thin Wide", 20, 2, course.transform);

        GameObject bounds = new GameObject("Camera Bounds 60 x 33.75");
        bounds.layer = LayerMask.NameToLayer("Ignore Raycast");
        BoxCollider2D area = bounds.AddComponent<BoxCollider2D>();
        area.isTrigger = true;
        area.size = new Vector2(60, 33.75f);
        Set(bounds.AddComponent<CameraBoundsArea>(), "_boundsCollider", area);

        StageEntryPoint entry = new GameObject("Player Start").AddComponent<StageEntryPoint>();
        entry.transform.position = new Vector3(-24, -12.98f, 0);
        var entryData = new SerializedObject(entry);
        entryData.FindProperty("_entryPointId").stringValue = "interaction_test_start";
        entryData.FindProperty("_isStartSpawnPoint").boolValue = true;
        entryData.ApplyModifiedPropertiesWithoutUndo();

        PlayerInteractionTestBootstrap bootstrap = new GameObject("Test Bootstrap").AddComponent<PlayerInteractionTestBootstrap>();
        Set(bootstrap, "_networkManager", network);
        Set(bootstrap, "_objectManager", objects);
        Set(bootstrap, "_worldManager", world);
        Set(bootstrap, "_services", services);
        Set(bootstrap, "_entryPoint", entry);
        ConfigureGroundBackground(bootstrap, camera);

        Label("PLAYER INTERACTION TEST", new Vector2(-26, 14.5f), 0.15f);
        Label("THICK + ONE-WAY PLATFORMS", new Vector2(-26, 12.7f), 0.09f);
        Label("START", new Vector2(-26, -15.6f), 0.09f);
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("[InteractionTest] Created " + ScenePath);
    }

    [MenuItem("Tools/Player Interaction/Apply Ground Background")]
    public static void ApplyGroundBackground()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        var scene = EditorSceneManager.OpenScene(ScenePath);
        PlayerInteractionTestBootstrap bootstrap = null;
        Camera camera = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.TryGetComponent(out PlayerInteractionTestBootstrap candidate)) bootstrap = candidate;
            if (root.TryGetComponent(out Camera candidateCamera) && candidateCamera.CompareTag("MainCamera")) camera = candidateCamera;
        }
        if (bootstrap == null || camera == null)
            throw new System.InvalidOperationException("Test Bootstrap and Main Camera are required.");
        ConfigureGroundBackground(bootstrap, camera);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    private static void ConfigureGroundBackground(PlayerInteractionTestBootstrap bootstrap, Camera camera)
    {
        ParallaxBackground background = null;
        foreach (GameObject root in bootstrap.gameObject.scene.GetRootGameObjects())
        {
            if (root.TryGetComponent(out ParallaxBackground existing))
            {
                background = existing;
                break;
            }
        }
        if (background == null)
        {
            GameObject source = PrefabUtility.LoadPrefabContents("Assets/2_Prefabs/Map/Workspaces/SungjunWork.prefab");
            try
            {
                foreach (ParallaxBackground candidate in source.GetComponentsInChildren<ParallaxBackground>(true))
                {
                    if (candidate.name != "ForestBackground_ParallaxTest") continue;
                    GameObject copy = Object.Instantiate(candidate.gameObject);
                    copy.name = "ForestBackground_ParallaxTest";
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(copy, bootstrap.gameObject.scene);
                    copy.transform.position = Vector3.zero;
                    copy.SetActive(true);
                    background = copy.GetComponent<ParallaxBackground>();
                    break;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(source);
            }
        }
        if (background == null)
            throw new System.InvalidOperationException("SungjunWork ground background was not found.");
        var settings = new SerializedObject(background);
        settings.FindProperty("_referenceCameraPosition").vector2Value = Vector2.zero;
        settings.FindProperty("_outputCamera").objectReferenceValue = camera;
        settings.FindProperty("_cameraBrain").objectReferenceValue = camera.GetComponent<CinemachineBrain>();
        settings.ApplyModifiedPropertiesWithoutUndo();
        // 저장된 씬에서도 기본 구도를 볼 수 있게 위치를 계산합니다.
        background.RefreshBackground();
        Set(bootstrap, "_background", background);
        Set(bootstrap, "_outputCamera", camera);
    }

    private static void Platform(string path, string name, float x, float top, Transform parent)
    {
        GameObject platform = Instance(PlatformRoot + path, parent);
        platform.name = name;
        BoxCollider2D box = platform.GetComponent<BoxCollider2D>();
        float offset = box != null ? box.size.y * 0.5f + box.offset.y : 0;
        platform.transform.position = new Vector3(x, top - offset, 0);
    }

    private static void CreateSolid(string name, Vector2 position, Vector2 size, Transform parent)
    {
        GameObject solid = new GameObject(name);
        solid.transform.SetParent(parent);
        solid.transform.position = position;
        solid.layer = LayerMask.NameToLayer("Ground");
        solid.AddComponent<BoxCollider2D>().size = size;
        SpriteRenderer sprite = solid.AddComponent<SpriteRenderer>();
        sprite.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        sprite.drawMode = SpriteDrawMode.Sliced;
        sprite.size = size;
        sprite.color = new Color32(65, 89, 105, 255);
    }

    private static void Label(string text, Vector2 position, float scale)
    {
        TextMesh label = new GameObject(text).AddComponent<TextMesh>();
        label.text = text;
        label.fontSize = 80;
        label.characterSize = scale;
        label.color = new Color32(205, 222, 235, 255);
        label.transform.position = position;
    }

    private static GameObject Instance(string path, Transform parent)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            throw new System.InvalidOperationException("Missing prefab: " + path);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.transform.localPosition = Vector3.zero;
        return instance;
    }

    private static void Set(Object target, string field, Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
