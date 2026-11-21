using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>기존 게임 씬이나 공통 매니저를 변경하지 않는 독립 테스트 씬입니다.</summary>
public static class SlidingPuzzleTestSceneBuilder
{
    public const string ScenePath = "Assets/0_Scenes/Tests/SlidingPuzzleLocalTest.unity";
    public const string ImagePath = "Assets/04_Sprites/UI/SlidingPuzzle/TreeSpiritTiles_v01/TreeSpiritTiles_Atlas_1254.png";
    private static readonly Color Ink = new Color32(225, 232, 240, 255);
    private static readonly Color BoardGray = new Color32(128, 128, 128, 255);

    [MenuItem("Tools/Sliding Puzzle/Create Local Test Scene")]
    public static void CreateScene()
    {
        TryCreateScene();
    }

    internal static bool TryCreateScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return false;
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return false;
        StageUtility.GoToMainStage();
        if (File.Exists(ScenePath))
        {
            EditorSceneManager.OpenScene(ScenePath);
            return true;
        }

        ConfigureTexture();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject cameraObject = new GameObject("Preview Camera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(14, 22, 33, 255);
        camera.orthographic = true;
        camera.transform.position = new Vector3(0f, 0f, -10f);

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

        GameObject canvasObject = new GameObject("Local Puzzle Canvas", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 800f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        RectTransform root = canvasObject.GetComponent<RectTransform>();
        CreatePuzzle(root, "Puzzle A", true);
        CreatePuzzle(root, "Puzzle B", false);

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("[SlidingPuzzle] Created " + ScenePath);
        return true;
    }

    private static LocalSlidingPuzzleController CreatePuzzle(RectTransform parent, string title, bool initiallyVisible)
    {
        GameObject controllerObject = new GameObject(title + " State");
        LocalSlidingPuzzleController controller = controllerObject.AddComponent<LocalSlidingPuzzleController>();
        RectTransform panel = Rect(parent, title + " UI", Vector2.zero, new Vector2(600f, 600f));
        Image background = panel.gameObject.AddComponent<Image>();
        background.color = BoardGray;
        SlidingPuzzleUI ui = panel.gameObject.AddComponent<SlidingPuzzleUI>();
        Button close = Button(panel, "Close", "X", new Vector2(278f, 330f), new Vector2(44f, 44f));

        RectTransform board = Rect(panel, "Board", Vector2.zero, new Vector2(540f, 540f));
        board.gameObject.AddComponent<Image>().color = BoardGray;
        SlidingPuzzleTileUI[] tiles = new SlidingPuzzleTileUI[8];
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ImagePath);
        for (int i = 0; i < tiles.Length; i++)
        {
            RectTransform tile = Rect(board, "Tile " + (i + 1), Vector2.zero, new Vector2(174f, 174f));
            RawImage image = tile.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            float part = 1f / 3f;
            image.uvRect = new Rect(i % 3 * part, (2 - i / 3) * part, part, part);
            tile.anchoredPosition = new Vector2(-178f + i % 3 * 178f, 178f - i / 3 * 178f);
            Button button = tile.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.9f, 0.98f, 0.94f);
            colors.pressedColor = new Color(0.75f, 0.9f, 0.84f);
            colors.disabledColor = Color.white;
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            tiles[i] = tile.gameObject.AddComponent<SlidingPuzzleTileUI>();
            SetObject(tiles[i], "_image", image);
            SetObject(tiles[i], "_button", button);
        }

        SetObject(ui, "_puzzleImage", texture);
        SetObject(ui, "_board", board);
        SetObject(ui, "_closeButton", close);
        SerializedObject serialized = new SerializedObject(ui);
        SerializedProperty property = serialized.FindProperty("_tiles");
        property.arraySize = tiles.Length;
        for (int i = 0; i < tiles.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = tiles[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        SetObject(controller, "_ui", ui);
        panel.gameObject.SetActive(initiallyVisible);
        return controller;
    }

    private static void ConfigureTexture()
    {
        AssetDatabase.ImportAsset(ImagePath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(ImagePath);
        importer.textureType = TextureImporterType.Default;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
    }

    private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static Text Label(Transform parent, string name, string text, Vector2 position,
        Vector2 size, int fontSize, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        Text label = Rect(parent, name, position, size).gameObject.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        return label;
    }

    private static Button Button(Transform parent, string name, string caption, Vector2 position, Vector2 size)
    {
        RectTransform rect = Rect(parent, name, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color32(82, 82, 82, 255);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        Label(rect, "Label", caption, Vector2.zero, size, 24, Ink, TextAnchor.MiddleCenter);
        return button;
    }

    private static void SetObject(Object target, string name, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(name).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
