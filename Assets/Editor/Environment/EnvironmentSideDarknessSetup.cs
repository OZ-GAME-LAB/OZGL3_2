using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>방향별 암전 테스트 에셋을 최초 생성하고 저장된 연결을 검증합니다.</summary>
[InitializeOnLoad]
public static class EnvironmentSideDarknessSetup
{
    static EnvironmentSideDarknessSetup()
    {
        EditorApplication.delayCall += RunRequestedBuild;
    }

    private static void RunRequestedBuild()
    {
        const string request = "Temp/SideDarkness.Build.request";
        if (!File.Exists(request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += RunRequestedBuild;
            return;
        }
        File.Delete(request);
        try
        {
            Build();
            File.WriteAllText("Library/SideDarkness.Build.result", "SUCCESS");
        }
        catch (Exception error)
        {
            File.WriteAllText("Library/SideDarkness.Build.result", error.ToString());
            Debug.LogException(error);
        }
    }
    private const string MapPath = "Assets/2_Prefabs/Map/Workspaces/SungjunWork.prefab";
    private const string PrefabPath = "Assets/2_Prefabs/World/Effects/CavernSideDarkness.prefab";
    private const string ProfilePath = "Assets/Settings/Environment/Cavern_DustAndDarkness.asset";
    private const string TextureRoot = "Assets/04_Sprites/Environment/ScreenEffects/DirectionalDarkness/";
    private static int _checks;

    [MenuItem("Tools/Environment/Create Side Darkness And Connect Cavern")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode first.");
        AssetDatabase.Refresh();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null ||
            AssetDatabase.LoadAssetAtPath<EnvironmentPresentationProfile>(ProfilePath) != null)
            throw new InvalidOperationException("Assets already exist; use Validate instead of overwriting tuning.");

        Sprite left = ImportScreen("Darkness_Left_960x1080.png");
        Sprite right = ImportScreen("Darkness_Right_960x1080.png");
        GameObject root = new GameObject("CavernSideDarkness");
        EnvironmentSideDarknessEffect effect;
        try
        {
            effect = root.AddComponent<EnvironmentSideDarknessEffect>();
            var serialized = new SerializedObject(effect);
            serialized.FindProperty("_leftScreen").objectReferenceValue = MakeScreen(root.transform, "LeftScreen", left);
            serialized.FindProperty("_rightScreen").objectReferenceValue = MakeScreen(root.transform, "RightScreen", right);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            effect = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath).GetComponent<EnvironmentSideDarknessEffect>();
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }

        AssetDatabase.CopyAsset("Assets/Settings/Environment/GlowDust_Area01_14.asset", ProfilePath);
        var profile = AssetDatabase.LoadAssetAtPath<EnvironmentPresentationProfile>(ProfilePath);
        var profileData = new SerializedObject(profile);
        SerializedProperty entries = profileData.FindProperty("_effects");
        int index = entries.arraySize++;
        SerializedProperty entry = entries.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("_prefab").objectReferenceValue = effect;
        entry.FindPropertyRelative("_intensity").floatValue = 1f;
        entry.FindPropertyRelative("_fadeInDuration").floatValue = 0.4f;
        entry.FindPropertyRelative("_fadeOutDuration").floatValue = 0.4f;
        profileData.ApplyModifiedPropertiesWithoutUndo();

        File.Copy(MapPath, "Library/SungjunWork.BeforeSideDarkness.prefab", true);
        GameObject map = PrefabUtility.LoadPrefabContents(MapPath);
        try
        {
            Transform environment = map.transform.Find("EnvironmentPresentation");
            Transform maskTransform = map.transform.Find("CavernParallax/NearNaturalColumns/AreaMask");
            if (environment == null || maskTransform == null) throw new InvalidOperationException("Missing map anchors.");
            SpriteMask mask = maskTransform.GetComponent<SpriteMask>();
            GameObject region = new GameObject("Cavern_DirectionalDarkness");
            region.layer = 2; // Ignore Raycast; the collider is used only as a trigger scope.
            region.transform.SetParent(environment, false);
            region.transform.SetPositionAndRotation(maskTransform.position, maskTransform.rotation);
            region.transform.localScale = maskTransform.localScale;
            BoxCollider2D scope = region.AddComponent<BoxCollider2D>();
            scope.isTrigger = true;
            scope.size = mask.sprite.bounds.size;
            scope.offset = mask.sprite.bounds.center;
            EnvironmentPresentationArea area = region.AddComponent<EnvironmentPresentationArea>();
            var areaData = new SerializedObject(area);
            areaData.FindProperty("_areaCollider").objectReferenceValue = scope;
            areaData.FindProperty("_priority").intValue = 20;
            areaData.FindProperty("_profile").objectReferenceValue = profile;
            areaData.ApplyModifiedPropertiesWithoutUndo();

            // 마스크의 큰 스케일이 기준점 거리 설정에 영향을 주지 않도록 별도 형제 루트를 사용합니다.
            GameObject anchors = new GameObject("Cavern_DarknessPoints");
            anchors.transform.SetParent(environment, false);
            anchors.transform.position = maskTransform.TransformPoint(mask.sprite.bounds.center);
            MakePoint(anchors.transform, "Darkness_Left", -8f, Vector2.left, 0, scope);
            MakePoint(anchors.transform, "Darkness_Right", 8f, Vector2.right, 1, scope);
            var controllerData = new SerializedObject(environment.GetComponent<EnvironmentPresentationController>());
            SerializedProperty areas = controllerData.FindProperty("_areas");
            int areaIndex = areas.arraySize++;
            areas.GetArrayElementAtIndex(areaIndex).objectReferenceValue = area;
            controllerData.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(map, MapPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(map); }
        AssetDatabase.SaveAssets();
        Validate();
    }

    private static Sprite ImportScreen(string name)
    {
        string path = TextureRoot + name;
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.spritePivot = new Vector2(0.5f, 0.5f);
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings);
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static SpriteRenderer MakeScreen(Transform parent, string name, Sprite sprite)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        var renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            AssetDatabase.GUIDToAssetPath("9dfc825aed78fcd4ba02077103263b40"));
        if (renderer.sharedMaterial == null) throw new InvalidOperationException("Unlit sprite material missing.");
        renderer.sortingOrder = 1000;
        renderer.color = new Color(0f, 0f, 0f, 0f);
        return renderer;
    }

    private static void MakePoint(Transform parent, string name, float x, Vector2 direction, int side, Collider2D scope)
    {
        var point = new GameObject(name);
        point.transform.SetParent(parent, false);
        point.transform.localPosition = new Vector3(x, 0f, 0f);
        var data = new SerializedObject(point.AddComponent<EnvironmentDirectionalDarknessArea>());
        data.FindProperty("_scope").objectReferenceValue = scope;
        data.FindProperty("_screenSide").enumValueIndex = side;
        data.FindProperty("_darkeningDirection").vector2Value = direction;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("Tools/Environment/Validate Side Darkness")]
    public static void Validate()
    {
        _checks = 0;
        GameObject map = PrefabUtility.LoadPrefabContents(MapPath);
        var cameraObject = new GameObject("Darkness Validation Camera");
        var target = new GameObject("Darkness Validation Target");
        GameObject effectObject = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        try
        {
            Physics2D.SyncTransforms();
            var points = map.GetComponentsInChildren<EnvironmentDirectionalDarknessArea>();
            Check(points.Length == 2, "Two directional points");
            foreach (var point in points)
            {
                Vector2 direction = point.Side == EnvironmentDirectionalDarknessArea.ScreenSide.Left ? Vector2.left : Vector2.right;
                Vector2 origin = point.transform.position;
                Check(Mathf.Approximately(point.Evaluate(origin), 0f), "Origin transparent");
                Check(Mathf.Approximately(point.Evaluate(origin - direction * 10f), 0f), "Reverse direction transparent");
                Check(Mathf.Abs(point.Evaluate(origin + direction * 10f) - 0.4f) < 0.001f, "Half distance");
                Check(Mathf.Abs(point.Evaluate(origin + direction * 20f) - 0.8f) < 0.001f, "Full distance");
                Check(point.Evaluate(origin + Vector2.up * 1000f) == 0f, "Outside scope");
            }
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 10f;
            var effect = effectObject.GetComponent<EnvironmentSideDarknessEffect>();
            effect.BindWorldContext(target.transform, map.transform);
            Check(effect.Initialize(camera), "Effect initialized");
            SpriteRenderer left = effectObject.transform.Find("LeftScreen").GetComponent<SpriteRenderer>();
            SpriteRenderer right = effectObject.transform.Find("RightScreen").GetComponent<SpriteRenderer>();
            foreach (float aspect in new[] { 16f / 9f, 4f / 3f, 21f / 9f })
            {
                camera.aspect = aspect;
                effect.RefreshCamera();
                Check(Mathf.Abs(left.bounds.size.x - 10f * aspect) < 0.001f, "Left half width");
                Check(Mathf.Abs(right.bounds.size.y - 20f) < 0.001f, "Full height");
                Check(Mathf.Abs(left.bounds.max.x - right.bounds.min.x) < 0.001f, "Seam aligned");
            }
            var update = typeof(EnvironmentSideDarknessEffect).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var point in points)
            {
                bool isLeft = point.Side == EnvironmentDirectionalDarknessArea.ScreenSide.Left;
                target.transform.position = point.transform.position + (isLeft ? Vector3.left : Vector3.right) * 20f;
                effect.ResetEffect();
                effect.ApplyIntensity(0.5f);
                update.Invoke(effect, null);
                Check(Mathf.Abs((isLeft ? left : right).color.a - 0.4f) < 0.001f, "Region intensity multiplied");
                Check((isLeft ? right : left).color.a == 0f, "Opposite side clear");
            }
            effect.ResetEffect();
            Check(left.color.a == 0f && right.color.a == 0f, "Reset transparent");
            var profile = AssetDatabase.LoadAssetAtPath<EnvironmentPresentationProfile>(ProfilePath);
            Check(profile.EffectCount == 2, "Dust preserved alongside darkness");
            File.WriteAllText("Library/SideDarkness.Validation.txt", $"PASS: {_checks} Unity Editor checks. Play Mode not exercised.\n");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(effectObject);
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            PrefabUtility.UnloadPrefabContents(map);
        }
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("Darkness validation: " + description);
        _checks++;
    }
}
