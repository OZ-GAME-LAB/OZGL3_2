using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>발광 먼지 에셋을 최초 생성합니다. 기존 에셋은 덮어쓰지 않습니다.</summary>
public static class EnvironmentDustSetup
{
    public const string PrefabPath = "Assets/2_Prefabs/World/Effects/ForegroundGlowDust.prefab";
    public const string ProfilePath = "Assets/Settings/Environment/GlowDust_Area00.asset";
    public const string SecondaryProfilePath = "Assets/Settings/Environment/GlowDust_Area01_14.asset";
    private const string TexturePath = "Assets/04_Sprites/Environment/Effects/GlowDust.png";
    private const string MapPath = "Assets/Resources/SungjunWork.prefab";

    [MenuItem("Tools/Environment/Create Glow Dust Assets And Connect Test Map")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play Mode를 종료한 뒤 실행해 주세요.");

        AssetDatabase.Refresh();
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        if (importer == null)
            throw new FileNotFoundException(TexturePath);
        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Trilinear;
        importer.maxTextureSize = 256;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        Material fine = CreateMaterial("GlowDust_Fine", texture, new Color(3.5f, 5.2f, 6f, 1f));
        Material near = CreateMaterial("GlowDust_Near", texture, new Color(0.7f, 1.1f, 1.4f, 0.55f));
        EnvironmentDustEffect prefab = CreatePrefab(fine, near);
        EnvironmentPresentationProfile primary = CreateProfile(ProfilePath, prefab, 0.75f);
        EnvironmentPresentationProfile secondary = CreateProfile(SecondaryProfilePath, prefab, 0.5f);
        ConnectMap(primary, secondary);
        AssetDatabase.SaveAssets();
        Debug.Log("발광 먼지 에셋과 SungjunWork 지역 연결 완료.");
    }

    private static Material CreateMaterial(string name, Texture2D texture, Color color)
    {
        string path = "Assets/Materials/Environment/" + name + ".mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            Shader boundedShader = Shader.Find("WanderSoul/Environment/GlowDust");
            if (boundedShader == null)
                throw new InvalidOperationException("GlowDust 셰이더를 먼저 임포트해 주세요.");
            if (existing.shader != boundedShader)
            {
                existing.shader = boundedShader;
                existing.shaderKeywords = Array.Empty<string>();
                EditorUtility.SetDirty(existing);
            }
            return existing;
        }
        Shader shader = Shader.Find("WanderSoul/Environment/GlowDust");
        if (shader == null)
            throw new InvalidOperationException("GlowDust 셰이더가 필요합니다.");
        Material material = new Material(shader) { name = name };
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", color);
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent;
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static EnvironmentDustEffect CreatePrefab(Material fine, Material near)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existing != null)
            return existing.GetComponent<EnvironmentDustEffect>();
        GameObject root = new GameObject("ForegroundGlowDust");
        root.SetActive(false);
        try
        {
            EnvironmentDustEffect effect = root.AddComponent<EnvironmentDustEffect>();
            ParticleSystem fineSystem = CreateParticles(root.transform, "FineDustEmitter", fine, false);
            ParticleSystem nearSystem = CreateParticles(root.transform, "NearDustEmitter", near, true);
            Transform fineSpace = new GameObject("FineSimulationSpace").transform;
            fineSpace.SetParent(root.transform, false);
            Transform nearSpace = new GameObject("NearSimulationSpace").transform;
            nearSpace.SetParent(root.transform, false);
            SerializedObject serialized = new SerializedObject(effect);
            SerializedProperty layers = serialized.FindProperty("_layers");
            layers.arraySize = 2;
            SetLayer(layers.GetArrayElementAtIndex(0), fineSystem, fineSpace, new Vector2(1.1f, 1.05f), 5f);
            SetLayer(layers.GetArrayElementAtIndex(1), nearSystem, nearSpace, new Vector2(1.25f, 1.15f), 3f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            GameObject result = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            return result.GetComponent<EnvironmentDustEffect>();
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void SetLayer(SerializedProperty layer, ParticleSystem particles, Transform space,
        Vector2 motion, float distance)
    {
        layer.FindPropertyRelative("Particles").objectReferenceValue = particles;
        layer.FindPropertyRelative("SimulationSpace").objectReferenceValue = space;
        layer.FindPropertyRelative("ScreenMotion").vector2Value = motion;
        layer.FindPropertyRelative("DistanceFromCamera").floatValue = distance;
        ParticleSystem.MainModule main = particles.main;
        main.simulationSpace = ParticleSystemSimulationSpace.Custom;
        main.customSimulationSpace = space;
    }

    private static ParticleSystem CreateParticles(Transform root, string name, Material material, bool near)
    {
        GameObject emitter = new GameObject(name);
        emitter.transform.SetParent(root, false);
        ParticleSystem particles = emitter.AddComponent<ParticleSystem>();
        particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 12f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(near ? 4f : 6f, near ? 8f : 12f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(near ? 0.6f : 0.1f, near ? 1.5f : 0.28f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.9f, 1f, 0.4f), Color.white);
        main.gravityModifier = 0f;
        main.maxParticles = near ? 6 : 40;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.stopAction = ParticleSystemStopAction.None;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = near ? 0f : 2.5f;
        if (near)
        {
            ParticleSystem.Burst burst = new ParticleSystem.Burst(1f, 1, 1, 4, 3f);
            burst.probability = 0.65f;
            emission.SetBursts(new[] { burst });
        }
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(66f, 39.75f, 0.01f);

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(near ? -0.2f : -0.12f, near ? 0.3f : 0.18f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.03f, near ? 0.2f : 0.12f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.separateAxes = true;
        noise.strengthX = near ? 0.3f : 0.18f;
        noise.strengthY = near ? 0.25f : 0.16f;
        noise.strengthZ = 0f;
        noise.frequency = 0.25f;
        noise.scrollSpeed = 0.12f;
        noise.octaveCount = 1;
        noise.quality = ParticleSystemNoiseQuality.Low;
        noise.damping = true;

        Gradient gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.2f),
                new GradientAlphaKey(0.65f, 0.65f), new GradientAlphaKey(0f, 1f) });
        ParticleSystem.ColorOverLifetimeModule colors = particles.colorOverLifetime;
        colors.enabled = true;
        colors.color = gradient;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortMode = ParticleSystemSortMode.OldestInFront;
        renderer.sortingOrder = near ? 310 : 300;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return particles;
    }

    private static EnvironmentPresentationProfile CreateProfile(string path, EnvironmentDustEffect prefab, float intensity)
    {
        EnvironmentPresentationProfile existing = AssetDatabase.LoadAssetAtPath<EnvironmentPresentationProfile>(path);
        if (existing != null)
            return existing;
        EnvironmentPresentationProfile profile = ScriptableObject.CreateInstance<EnvironmentPresentationProfile>();
        SerializedObject serialized = new SerializedObject(profile);
        SerializedProperty effects = serialized.FindProperty("_effects");
        effects.arraySize = 1;
        SerializedProperty entry = effects.GetArrayElementAtIndex(0);
        entry.FindPropertyRelative("_prefab").objectReferenceValue = prefab;
        entry.FindPropertyRelative("_intensity").floatValue = intensity;
        entry.FindPropertyRelative("_fadeInDuration").floatValue = 2.5f;
        entry.FindPropertyRelative("_fadeOutDuration").floatValue = 2f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(profile, path);
        return profile;
    }

    private static void ConnectMap(EnvironmentPresentationProfile primary, EnvironmentPresentationProfile secondary)
    {
        GameObject map = PrefabUtility.LoadPrefabContents(MapPath);
        try
        {
            if (map.GetComponentInChildren<EnvironmentPresentationController>(true) != null)
                return;
            GameObject presentation = new GameObject("EnvironmentPresentation");
            presentation.transform.SetParent(map.transform, false);
            EnvironmentPresentationController controller = presentation.AddComponent<EnvironmentPresentationController>();
            EnvironmentPresentationArea first = CreateArea(presentation.transform, map.transform.Find("Trigger_Area00"), primary);
            EnvironmentPresentationArea second = CreateArea(presentation.transform, map.transform.Find("Trigger_Area01_14"), secondary);
            SerializedObject serialized = new SerializedObject(controller);
            SerializedProperty areas = serialized.FindProperty("_areas");
            areas.arraySize = 2;
            areas.GetArrayElementAtIndex(0).objectReferenceValue = first;
            areas.GetArrayElementAtIndex(1).objectReferenceValue = second;
            // 기본 프로필은 비워 두어 두 연출 영역 밖에서는 먼지가 퇴장합니다.
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(map, MapPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(map); }
    }

    private static EnvironmentPresentationArea CreateArea(Transform parent, Transform source,
        EnvironmentPresentationProfile profile)
    {
        if (source == null || !source.TryGetComponent(out BoxCollider2D sourceCollider))
            throw new InvalidOperationException("테스트 맵의 카메라 영역을 찾을 수 없습니다.");
        GameObject region = new GameObject("GlowDust_" + source.name);
        region.transform.SetParent(parent, false);
        region.transform.position = source.position;
        region.transform.rotation = source.rotation;
        region.transform.localScale = source.localScale;
        BoxCollider2D collider = region.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.offset = sourceCollider.offset;
        collider.size = sourceCollider.size;
        EnvironmentPresentationArea area = region.AddComponent<EnvironmentPresentationArea>();
        SerializedObject serialized = new SerializedObject(area);
        serialized.FindProperty("_areaCollider").objectReferenceValue = collider;
        serialized.FindProperty("_profile").objectReferenceValue = profile;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return area;
    }
}
