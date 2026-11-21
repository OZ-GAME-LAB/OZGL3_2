using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>열린 상태로 반복 재생하는 포탈을 생성합니다. 기존 에셋은 덮어쓰지 않습니다.</summary>
public static class BluePortalSetup
{
    public const string PrefabPath = "Assets/2_Prefabs/World/Effects/BluePortal.prefab";
    public const string MaterialPath = "Assets/Materials/Environment/BluePortal.mat";
    private const string SparkMaterialPath = "Assets/Materials/Environment/BluePortalSpark.mat";

    [MenuItem("Tools/Environment/Create Blue Portal")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play Mode를 종료한 뒤 실행해 주세요.");

        Material bodyMaterial = GetOrCreateMaterial(MaterialPath, "OZGL/Environment/Blue Portal");
        Material sparkMaterial = GetOrCreateMaterial(SparkMaterialPath, "WanderSoul/Interaction/RangeSparkle");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            Debug.Log("기존 BluePortal 프리팹을 유지합니다: " + PrefabPath);
            return;
        }

        GameObject root = new GameObject("BluePortal");
        try
        {
            CreateSurface(root.transform, bodyMaterial);
            CreateSparks(root.transform, sparkMaterial);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            if (prefab == null)
                throw new InvalidOperationException("포탈 프리팹 저장에 실패했습니다.");
            AssetDatabase.SaveAssetIfDirty(bodyMaterial);
            AssetDatabase.SaveAssetIfDirty(sparkMaterial);
            Debug.Log("BluePortal 생성 완료: " + PrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static Material GetOrCreateMaterial(string path, string shaderName)
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
            return existing;
        Shader shader = Shader.Find(shaderName);
        if (shader == null || ShaderUtil.ShaderHasError(shader))
            throw new InvalidOperationException("셰이더를 확인해 주세요: " + shaderName);
        Material material = new Material(shader);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void CreateSurface(Transform parent, Material material)
    {
        GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
        surface.name = "PortalSurface";
        surface.transform.SetParent(parent, false);
        surface.transform.localScale = new Vector3(3.2f, 4.8f, 1f);
        UnityEngine.Object.DestroyImmediate(surface.GetComponent<Collider>());
        MeshRenderer renderer = surface.GetComponent<MeshRenderer>();
        ConfigureRenderer(renderer, material, 20);
    }

    private static void CreateSparks(Transform parent, Material material)
    {
        GameObject sparks = new GameObject("RimSparkles");
        sparks.transform.SetParent(parent, false);
        ParticleSystem particles = sparks.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.duration = 4f;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.cullingMode = ParticleSystemCullingMode.Pause;
        main.maxParticles = 48;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.075f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.15f, 0.55f, 1f, 0.65f), new Color(0.65f, 0.95f, 1f, 0.9f));
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI);

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 20f;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 1f;
        shape.radiusThickness = 0.015f;
        shape.scale = new Vector3(1.12f, 1.68f, 1f);

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.y = 0.12f;
        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        color.color = fade;
        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));
        ConfigureRenderer(particles.GetComponent<ParticleSystemRenderer>(), material, 21);
    }

    private static void ConfigureRenderer(Renderer renderer, Material material, int order)
    {
        renderer.sharedMaterial = material;
        renderer.sortingOrder = order;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }
}
