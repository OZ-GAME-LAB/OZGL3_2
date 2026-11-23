using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>독안개 프리팹 생성과 독립 미리보기 검사. 갱신 메뉴에서만 기존 프리셋을 수정합니다.</summary>
public static class PoisonFogSetup
{
    public const string PrefabPath = "Assets/2_Prefabs/World/Effects/PoisonFog.prefab";
    private const string MaterialPath = "Assets/Materials/Environment/PoisonFog.mat";
    private const string OutputDirectory = ".utmp/poison-fog";

    [MenuItem("Tools/Environment/Create Poison Fog")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            SaveDiffusePreset(false);
    }

    [MenuItem("Tools/Environment/Update Poison Fog Diffuse Preset")]
    public static void UpdateDiffusePreset()
    {
        SaveDiffusePreset(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null);
    }

    private static void SaveDiffusePreset(bool updateExisting)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play Mode를 종료한 뒤 실행해 주세요.");
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Materials/Environment/PoisonFog.shader");
        if (shader == null || ShaderUtil.ShaderHasError(shader))
            throw new InvalidOperationException("PoisonFog 셰이더 오류를 확인해 주세요.");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        material.SetColor("_FogColor", new Color(0.46f, 0.38f, 0.52f, 1f));
        material.SetVector("_FogMotion", new Vector4(0.3f, 0.4f, 1f, 0f));
        EditorUtility.SetDirty(material);

        GameObject root = updateExisting ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("PoisonFog");
        root.SetActive(false);
        try
        {
            PoisonFogEffect effect = root.GetComponent<PoisonFogEffect>();
            if (effect == null)
                effect = root.AddComponent<PoisonFogEffect>();
            SerializedObject settings = new SerializedObject(effect);
            ParticleSystem baseParticles = settings.FindProperty("_particles").objectReferenceValue as ParticleSystem;
            ParticleSystem wisps = settings.FindProperty("_wisps").objectReferenceValue as ParticleSystem;
            if (baseParticles == null)
                baseParticles = CreateLayer(root.transform, "FogParticles");
            if (wisps == null)
                wisps = CreateLayer(root.transform, "DriftingWisps");
            ConfigureParticles(baseParticles, material, false);
            ConfigureParticles(wisps, material, true);
            settings.FindProperty("_particles").objectReferenceValue = baseParticles;
            settings.FindProperty("_particleRenderer").objectReferenceValue = baseParticles.GetComponent<ParticleSystemRenderer>();
            settings.FindProperty("_wisps").objectReferenceValue = wisps;
            settings.FindProperty("_wispRenderer").objectReferenceValue = wisps.GetComponent<ParticleSystemRenderer>();
            settings.FindProperty("_density").floatValue = 0.75f;
            settings.FindProperty("_fogColor").colorValue = new Color(0.46f, 0.38f, 0.52f, 1f);
            settings.FindProperty("_areaSize").vector2Value = new Vector2(24f, 5f);
            settings.FindProperty("_spread").floatValue = 1f;
            settings.FindProperty("_flowSpeed").floatValue = 0.3f;
            settings.FindProperty("_swirlStrength").floatValue = 0.4f;
            settings.FindProperty("_noiseScale").floatValue = 1f;
            settings.FindProperty("_fullDensityEmission").floatValue = 5.6f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(true);
            if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                throw new InvalidOperationException("독안개 프리팹 저장 실패");
            AssetDatabase.SaveAssetIfDirty(material);
            Debug.Log("넓게 퍼지는 2층 독안개 저장 완료: " + PrefabPath);
        }
        finally
        {
            if (updateExisting)
                PrefabUtility.UnloadPrefabContents(root);
            else
                UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static ParticleSystem CreateLayer(Transform parent, string layerName)
    {
        GameObject layer = new GameObject(layerName);
        layer.transform.SetParent(parent, false);
        return layer.AddComponent<ParticleSystem>();
    }

    private static void ConfigureParticles(ParticleSystem particles, Material material, bool isWisp)
    {
        particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        particles.useAutoRandomSeed = false;
        particles.randomSeed = isWisp ? 51673u : 73921u;
        ParticleSystem.MainModule main = particles.main;
        main.duration = 22f;
        main.loop = true;
        main.prewarm = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.cullingMode = ParticleSystemCullingMode.PauseAndCatchup;
        main.maxParticles = 48;
        main.startLifetime = isWisp ? new ParticleSystem.MinMaxCurve(9f, 15f) : new ParticleSystem.MinMaxCurve(15f, 22f);
        main.startSpeed = 0f;
        main.startSize3D = true;
        main.startSizeX = new ParticleSystem.MinMaxCurve(isWisp ? 7f : 9f, isWisp ? 13f : 15f);
        main.startSizeY = new ParticleSystem.MinMaxCurve(isWisp ? 1f : 3.5f, isWisp ? 2.4f : 6f);
        main.startSizeZ = 1f;
        main.startRotation = new ParticleSystem.MinMaxCurve(-0.09f, 0.09f);
        main.startColor = Color.white;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = isWisp ? 3.2f : 2.4f;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 1f;
        shape.radiusThickness = 1f;
        shape.scale = new Vector3(12f, 2.5f, 1f);
        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(0.05f, 0.4f);
        velocity.y = new ParticleSystem.MinMaxCurve(-0.04f, 0.06f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        ParticleSystem.RotationOverLifetimeModule rotation = particles.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.008f, 0.008f);
        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.separateAxes = false;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.65f, 1f, 1.55f));
        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.32f),
                new GradientAlphaKey(0.85f, 0.55f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
        ParticleSystem.CustomDataModule custom = particles.customData;
        custom.enabled = true;
        custom.SetMode(ParticleSystemCustomData.Custom1, ParticleSystemCustomDataMode.Vector);
        custom.SetVectorComponentCount(ParticleSystemCustomData.Custom1, 2);
        custom.SetVector(ParticleSystemCustomData.Custom1, 0, new ParticleSystem.MinMaxCurve(0.01f, 99.99f));
        custom.SetVector(ParticleSystemCustomData.Custom1, 1, new ParticleSystem.MinMaxCurve(0.01f, 99.99f));

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.Local;
        renderer.sortMode = ParticleSystemSortMode.None;
        renderer.sortingOrder = isWisp ? 16 : 15;
        renderer.maxParticleSize = 2f;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
        {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color,
            ParticleSystemVertexStream.UV, ParticleSystemVertexStream.Custom1XY
        });
    }

    [MenuItem("Tools/Environment/Validate And Preview Poison Fog")]
    public static void ValidateAndPreview()
    {
        Build();
        Directory.CreateDirectory(OutputDirectory);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        foreach (string dependency in AssetDatabase.GetDependencies(PrefabPath))
        {
            if (dependency.StartsWith("Assets/AIWork", StringComparison.Ordinal))
                throw new InvalidOperationException("실험 폴더에 대한 참조가 있습니다.");
        }
        Scene preview = EditorSceneManager.NewPreviewScene();
        RenderTexture target = new RenderTexture(960, 400, 24, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        Texture2D image = new Texture2D(960, 400, TextureFormat.RGB24, false);
        try
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
            PoisonFogEffect effect = instance.GetComponent<PoisonFogEffect>();
            ParticleSystem[] layers = instance.GetComponentsInChildren<ParticleSystem>();
            if (layers.Length != 2 || layers[0].main.maxParticles + layers[1].main.maxParticles > 96)
                throw new InvalidOperationException("두 안개층 / 전체 입자 수 제한 확인 실패");
            GameObject cameraObject = new GameObject("FogPreviewCamera");
            SceneManager.MoveGameObjectToScene(cameraObject, preview);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.scene = preview;
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 8f;
            camera.transform.position = new Vector3(1f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.045f, 0.055f, 0.065f, 1f);
            camera.allowHDR = false;
            camera.targetTexture = target;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            float[] densities = { 0f, 0.25f, 0.75f, 1f };
            Color32[] empty = null;
            double previousEnergy = 0;
            string report = "Unity URP diffuse fog validation\n";
            foreach (float density in densities)
            {
                effect.SetDensity(density);
                foreach (ParticleSystem layer in layers)
                    layer.Simulate(layer.main.duration, false, true, true);
                effect.AdvanceEditorPreview(0f);
                Render(camera, target, image);
                Color32[] pixels = image.GetPixels32();
                double energy = 0;
                if (empty == null)
                    empty = pixels;
                else
                {
                    for (int i = 0; i < pixels.Length; i++)
                        energy += Math.Abs(pixels[i].r - empty[i].r) + Math.Abs(pixels[i].b - empty[i].b);
                    if (energy <= previousEnergy + 1000)
                        throw new InvalidOperationException("농도 증가 시 렌더링 변화 확인 실패");
                }
                previousEnergy = energy;
                report += "Density=" + density + ", particles=" + (layers[0].particleCount + layers[1].particleCount)
                    + ", image difference=" + energy + "\n";
                File.WriteAllBytes(OutputDirectory + "/diffuse-" + density.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ".png", image.EncodeToPNG());
            }
            int countBefore = layers[0].particleCount + layers[1].particleCount;
            effect.SetDensity(0.4f);
            GameObject second = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
            second.GetComponent<PoisonFogEffect>().SetDensity(0.2f);
            UnityEngine.Object.DestroyImmediate(second);
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            foreach (ParticleSystem layer in layers)
            {
                ParticleSystemRenderer renderer = layer.GetComponent<ParticleSystemRenderer>();
                renderer.GetPropertyBlock(properties);
                if (Mathf.Abs(properties.GetFloat("_Density") - 0.4f) > 0.001f
                    || Mathf.Abs(renderer.sharedMaterial.GetFloat("_Density") - 0.75f) > 0.001f)
                    throw new InvalidOperationException("입자별 농도 / 공유 머티리얼 독립성 확인 실패");
                List<Vector4> phase = new List<Vector4>();
                layer.GetCustomParticleData(phase, ParticleSystemCustomData.Custom1);
                if (phase.Count < 2 || phase[0] == phase[1])
                    throw new InvalidOperationException("입자별 독립 무늬 데이터 확인 실패");
                if (ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader))
                    throw new InvalidOperationException("셰이더 오류");
            }
            if (layers[0].particleCount + layers[1].particleCount != countBefore)
                throw new InvalidOperationException("농도 변경 시 기존 입자가 재생성되었습니다.");
            effect.SetDensity(0f);
            foreach (ParticleSystem layer in layers)
            {
                if (layer.particleCount != 0 || layer.GetComponent<ParticleSystemRenderer>().enabled)
                    throw new InvalidOperationException("농도 0에서 두 층 숨김 실패");
            }
            effect.SetDensity(0.75f);
            instance.SetActive(false);
            instance.SetActive(true);
            if (layers[0].particleCount == 0 || layers[1].particleCount == 0)
                throw new InvalidOperationException("두 층 재활성화 실패");
            Render(camera, target, image);
            Color32[] before = image.GetPixels32();
            for (int i = 0; i < 120; i++)
                effect.AdvanceEditorPreview(1f / 30f);
            Render(camera, target, image);
            Color32[] after = image.GetPixels32();
            long motion = 0;
            for (int i = 0; i < before.Length; i++)
                motion += Math.Abs(before[i].b - after[i].b);
            if (motion < 1000)
                throw new InvalidOperationException("안개 움직임 확인 실패");
            report += "PASS: two layers, density changes, zero/restart, independent instances, random particle patterns, shared material, moving fog, max 96 particles, references.\n";
            File.WriteAllText(OutputDirectory + "/diffuse-validation.txt", report);
            Debug.Log(report);
        }
        finally
        {
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(image);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    private static void Render(Camera camera, RenderTexture target, Texture2D image)
    {
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        RenderTexture.active = target;
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        image.Apply();
    }
}
