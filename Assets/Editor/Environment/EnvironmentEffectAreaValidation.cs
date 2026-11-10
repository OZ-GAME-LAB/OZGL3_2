using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>임시 오브젝트로 공통 생성 영역과 먼지의 수명을 검증합니다. 씬과 프리팹은 저장하지 않습니다.</summary>
public static class EnvironmentEffectAreaValidation
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/Environment/Validate Effect Spawn Areas")]
    public static void Validate()
    {
        GameObject map = new GameObject("SpawnAreaValidation");
        GameObject cameraObject = new GameObject("SpawnCameraValidation");
        GameObject instance = null;
        int checks = 0;
        try
        {
            EnvironmentDustEffect prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/2_Prefabs/World/Effects/ForegroundGlowDust.prefab").GetComponent<EnvironmentDustEffect>();
            EnvironmentEffectArea area = map.AddComponent<EnvironmentEffectArea>();
            typeof(EnvironmentEffectArea).GetField("_effectPrefabs", Fields).SetValue(area,
                new EnvironmentScreenEffect[] { prefab });
            BoxCollider2D first = map.AddComponent<BoxCollider2D>();
            first.isTrigger = true;
            first.size = new Vector2(3f, 4f);
            first.offset = new Vector2(-8f, 0f);
            BoxCollider2D second = map.AddComponent<BoxCollider2D>();
            second.isTrigger = true;
            second.size = new Vector2(5f, 2f);
            second.offset = new Vector2(8f, 0f);
            map.transform.position = new Vector3(20f, 5f, 0f);
            map.transform.rotation = Quaternion.Euler(0f, 0f, 25f);
            map.transform.localScale = new Vector3(-1.5f, 0.8f, 1f);
            area.RefreshColliders();
            Physics2D.SyncTransforms();
            Check(area.Supports(prefab) && !area.Supports(null), "Effect matching", ref checks);
            bool seenFirst = false;
            bool seenSecond = false;
            for (int i = 0; i < 500; i++)
            {
                Check(area.TryGetSpawnPosition(out Vector2 point), "Sample succeeds", ref checks);
                bool inFirst = first.OverlapPoint(point);
                bool inSecond = second.OverlapPoint(point);
                Check(inFirst || inSecond, "Sample inside rotated/scaled boxes", ref checks);
                seenFirst |= inFirst;
                seenSecond |= inSecond;
            }
            Check(seenFirst && seenSecond, "Both disconnected boxes used", ref checks);
            first.enabled = false;
            second.isTrigger = false;
            Check(!area.TryGetSpawnPosition(out _), "Disabled/non-trigger ignored", ref checks);
            first.enabled = true;
            second.isTrigger = true;
            area.enabled = false;
            Check(!area.TryGetSpawnPosition(out _), "Disabled area ignored", ref checks);
            area.enabled = true;
            Physics2D.SyncTransforms();

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            instance = UnityEngine.Object.Instantiate(prefab.gameObject);
            instance.SetActive(false);
            EnvironmentDustEffect effect = instance.GetComponent<EnvironmentDustEffect>();
            effect.BindSourcePrefab(prefab);
            effect.BindArea(area);
            Check(effect.Initialize(camera), "Prepare succeeds", ref checks);
            instance.SetActive(true);
            // Edit Mode 검사에서는 런타임 OnEnable에 의존하지 않고 활성화 후 재생을 요청합니다.
            effect.SetTargetIntensity(1f);
            Array layers = (Array)typeof(EnvironmentDustEffect).GetField("_layers", Fields).GetValue(effect);
            MethodInfo advance = typeof(EnvironmentDustEffect).GetMethod("AdvanceEmission", Fields);
            foreach (object layer in layers)
            {
                ParticleSystem particles = (ParticleSystem)layer.GetType().GetField("Particles").GetValue(layer);
                ParticleSystem.Burst[] bursts = (ParticleSystem.Burst[])layer.GetType().GetField("Bursts").GetValue(layer);
                for (int i = 0; i < bursts.Length; i++)
                    bursts[i].probability = 1f;
                advance.Invoke(effect, new object[] { layer, 12f });
                int expected = bursts.Length > 0 ? 4 : 30;
                Check(particles.particleCount == expected,
                    "Rate/Burst counts: " + particles.name + " expected " + expected + ", actual " + particles.particleCount,
                    ref checks);
                ParticleSystem.Particle[] buffer = new ParticleSystem.Particle[particles.main.maxParticles];
                int count = particles.GetParticles(buffer);
                for (int i = 0; i < count; i++)
                {
                    Vector2 world = particles.main.customSimulationSpace.TransformPoint(buffer[i].position);
                    Check(first.OverlapPoint(world) || second.OverlapPoint(world), "Particle starts inside", ref checks);
                }
                Vector3 anchor = particles.main.customSimulationSpace.position;
                camera.transform.position += new Vector3(100f, 100f, 0f);
                effect.RefreshCamera();
                Check(particles.main.customSimulationSpace.position == anchor, "Camera does not move dust", ref checks);
                MaterialPropertyBlock properties = new MaterialPropertyBlock();
                particles.GetComponent<ParticleSystemRenderer>().GetPropertyBlock(properties);
                Check(properties.GetVector("_DustCameraRight") == Vector4.zero
                    && properties.GetVector("_DustCameraUp") == Vector4.zero, "Viewport mask disabled", ref checks);
                buffer[0].position = new Vector3(10000f, 10000f, 0f);
                particles.SetParticles(buffer, count);
                area.enabled = false;
                advance.Invoke(effect, new object[] { layer, 0.1f });
                Check(particles.particleCount == count, "Outside particles survive; disabled area emits none", ref checks);
                particles.Simulate(20f, false, false, true);
                Check(particles.particleCount == 0, "Particles expire by lifetime", ref checks);
                area.enabled = true;
            }
            effect.ResetEffect();
            effect.SetTargetIntensity(1f);
            ValidateEmissionMultiplier(effect, area, layers, advance, ref checks);
            ValidateIndependentAreas(prefab, camera, ref checks);
            ValidateMixedEffects(prefab, camera, ref checks);
            File.WriteAllText("Temp/EffectArea.Validation.txt", "PASS: " + checks + " checks");
            Debug.Log("Effect spawn area validation passed: " + checks);
        }
        catch (Exception exception)
        {
            File.WriteAllText("Temp/EffectArea.Validation.txt", "FAIL: " + exception);
            throw;
        }
        finally
        {
            if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            UnityEngine.Object.DestroyImmediate(map);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    private static void ValidateEmissionMultiplier(EnvironmentDustEffect effect, EnvironmentEffectArea area,
        Array layers, MethodInfo advance, ref int checks)
    {
        object layer = layers.GetValue(0);
        Type layerType = layer.GetType();
        ParticleSystem particles = (ParticleSystem)layerType.GetField("Particles").GetValue(layer);
        effect.SetEmissionMultiplier(0.25f);
        layerType.GetField("Rate").SetValue(layer, new ParticleSystem.MinMaxCurve(10f));
        layerType.GetField("Bursts").SetValue(layer, Array.Empty<ParticleSystem.Burst>());
        advance.Invoke(effect, new object[] { layer, 1f });
        Check(particles.particleCount == 2, "Quarter multiplier emits two particles and saves fraction", ref checks);
        advance.Invoke(effect, new object[] { layer, 1f });
        Check(particles.particleCount == 5, "Rate fraction carries to next emission", ref checks);

        effect.ResetEffect();
        effect.SetTargetIntensity(1f);
        layerType.GetField("Rate").SetValue(layer, new ParticleSystem.MinMaxCurve(0f));
        layerType.GetField("Bursts").SetValue(layer, new[]
        {
            new ParticleSystem.Burst(0f, (short)1, (short)1, 4, 0.1f)
        });
        advance.Invoke(effect, new object[] { layer, 0.4f });
        Check(particles.particleCount == 1, "Four quarter-sized bursts accumulate one particle", ref checks);

        effect.ResetEffect();
        effect.SetTargetIntensity(1f);
        layerType.GetField("Bursts").SetValue(layer, Array.Empty<ParticleSystem.Burst>());
        layerType.GetField("Rate").SetValue(layer, new ParticleSystem.MinMaxCurve(10f));
        Vector3 originalScale = area.transform.localScale;
        area.transform.localScale = originalScale * 2f;
        Physics2D.SyncTransforms();
        advance.Invoke(effect, new object[] { layer, 1f });
        Check(particles.particleCount == 2, "Area size does not change emission amount", ref checks);
        effect.ResetEffect();
        effect.SetTargetIntensity(1f);
        effect.SetEmissionMultiplier(1f);
        advance.Invoke(effect, new object[] { layer, 1f });
        Check(particles.particleCount == 10, "Multiplier one restores original rate", ref checks);

        effect.SetEmissionMultiplier(0f);
        advance.Invoke(effect, new object[] { layer, 1f });
        Check(particles.particleCount == 10, "Zero multiplier stops emission without clearing existing particles", ref checks);
        effect.SetEmissionMultiplier(1f);

        effect.ResetEffect();
        effect.SetTargetIntensity(1f);
        int maxParticles = particles.main.maxParticles;
        layerType.GetField("Rate").SetValue(layer, new ParticleSystem.MinMaxCurve(maxParticles * 2f));
        advance.Invoke(effect, new object[] { layer, 1f });
        Check(particles.particleCount == maxParticles && particles.main.maxParticles == maxParticles,
            "Large emissions preserve and obey Max Particles", ref checks);
        effect.ResetEffect();
        area.transform.localScale = originalScale;
        Physics2D.SyncTransforms();
    }

    private static void ValidateMixedEffects(EnvironmentDustEffect dustPrefab, Camera camera, ref int checks)
    {
        GameObject areaObject = new GameObject("MixedEffectsValidation");
        GameObject fogObject = new GameObject("TemporaryFogPrefab");
        GameObject root = null;
        Sprite sprite = null;
        try
        {
            fogObject.SetActive(false);
            EnvironmentSpriteEffect fogPrefab = fogObject.AddComponent<EnvironmentSpriteEffect>();
            SpriteRenderer renderer = fogObject.AddComponent<SpriteRenderer>();
            sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 2f, 2f), Vector2.one * 0.5f, 1f);
            renderer.sprite = sprite;
            renderer.color = new Color(1f, 1f, 1f, 0.8f);
            typeof(EnvironmentSpriteEffect).GetField("_sprites", Fields).SetValue(fogPrefab, new[] { renderer });
            EnvironmentEffectArea area = areaObject.AddComponent<EnvironmentEffectArea>();
            BoxCollider2D box = areaObject.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(12f, 6f);
            areaObject.transform.position = new Vector3(30f, 40f, 0f);
            Physics2D.SyncTransforms();
            Type entryType = typeof(EnvironmentEffectArea).GetNestedType("EffectEntry", BindingFlags.NonPublic);
            Array entries = Array.CreateInstance(entryType, 2);
            object dustEntry = CreateEntry(entryType, dustPrefab, 0.25f, 0.7f);
            object fogEntry = CreateEntry(entryType, fogPrefab, 3f, 0.25f);
            entries.SetValue(dustEntry, 0);
            entries.SetValue(fogEntry, 1);
            typeof(EnvironmentEffectArea).GetField("_effects", Fields).SetValue(area, entries);
            Check(area.Initialize(camera), "Mixed area initializes", ref checks);
            root = (GameObject)typeof(EnvironmentEffectArea).GetField("_effectRoot", Fields).GetValue(area);
            EnvironmentDustEffect dust = root.GetComponentInChildren<EnvironmentDustEffect>(true);
            EnvironmentSpriteEffect fog = root.GetComponentInChildren<EnvironmentSpriteEffect>(true);
            Check(dust != null && fog != null, "Dust and image coexist in one area", ref checks);
            Check(Mathf.Approximately((float)typeof(EnvironmentDustEffect).GetField("_emissionMultiplier", Fields)
                .GetValue(dust), 0.25f), "Area applies dust emission multiplier", ref checks);
            SpriteRenderer image = fog.GetComponent<SpriteRenderer>();
            Check(Mathf.Approximately(image.color.a, 0.2f), "Image alpha multiplies original alpha", ref checks);
            Check(Vector2.Distance(image.bounds.center, box.bounds.center) < 0.001f
                && Vector2.Distance(image.bounds.size, box.bounds.size) < 0.001f,
                "Image fits area bounds instead of camera viewport", ref checks);
            Vector3 imagePosition = fog.transform.position;
            camera.transform.position += Vector3.right * 50f;
            fog.RefreshCamera();
            Check(Vector2.Distance(imagePosition, fog.transform.position) < 0.001f,
                "Moving camera does not move area image in XY", ref checks);

            entryType.GetField("Intensity").SetValue(fogEntry, 0.5f);
            entryType.GetField("EmissionMultiplier").SetValue(dustEntry, 2f);
            typeof(EnvironmentEffectArea).GetMethod("Update", Fields).Invoke(area, null);
            Check(Mathf.Approximately(image.color.a, 0.4f), "Live image opacity updates", ref checks);
            Check(Mathf.Approximately((float)typeof(EnvironmentDustEffect).GetField("_emissionMultiplier", Fields)
                .GetValue(dust), 2f), "Live dust amount updates independently", ref checks);

            entryType.GetField("Intensity").SetValue(fogEntry, 0f);
            typeof(EnvironmentEffectArea).GetMethod("Update", Fields).Invoke(area, null);
            Check(!fog.gameObject.activeSelf && dust.gameObject.activeSelf, "One entry can be disabled independently", ref checks);
            entryType.GetField("Intensity").SetValue(fogEntry, 0.5f);
            typeof(EnvironmentEffectArea).GetMethod("Update", Fields).Invoke(area, null);
            Check(fog.gameObject.activeSelf && Mathf.Approximately(image.color.a, 0.4f),
                "Image reactivation restores alpha without accumulation", ref checks);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(areaObject);
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(fogObject);
            if (sprite != null) UnityEngine.Object.DestroyImmediate(sprite);
        }
    }

    private static object CreateEntry(Type entryType, EnvironmentScreenEffect prefab, float emission, float intensity)
    {
        object entry = Activator.CreateInstance(entryType, true);
        entryType.GetField("Prefab").SetValue(entry, prefab);
        entryType.GetField("EmissionMultiplier").SetValue(entry, emission);
        entryType.GetField("Intensity").SetValue(entry, intensity);
        return entry;
    }

    private static void ValidateIndependentAreas(EnvironmentDustEffect prefab, Camera camera, ref int checks)
    {
        GameObject firstObject = new GameObject("IndependentAreaA");
        GameObject secondObject = new GameObject("IndependentAreaB");
        GameObject firstRoot = null;
        GameObject secondRoot = null;
        try
        {
            EnvironmentEffectArea first = firstObject.AddComponent<EnvironmentEffectArea>();
            EnvironmentEffectArea second = secondObject.AddComponent<EnvironmentEffectArea>();
            BoxCollider2D firstBox = firstObject.AddComponent<BoxCollider2D>();
            BoxCollider2D secondBox = secondObject.AddComponent<BoxCollider2D>();
            firstBox.isTrigger = secondBox.isTrigger = true;
            firstObject.transform.position = new Vector3(-100f, 0f, 0f);
            secondObject.transform.position = new Vector3(100f, 0f, 0f);
            FieldInfo prefabs = typeof(EnvironmentEffectArea).GetField("_effectPrefabs", Fields);
            prefabs.SetValue(first, new EnvironmentScreenEffect[] { prefab, prefab });
            prefabs.SetValue(second, new EnvironmentScreenEffect[] { prefab });
            Check(first.Initialize(camera) && second.Initialize(camera), "Areas initialize without Presentation", ref checks);
            FieldInfo rootField = typeof(EnvironmentEffectArea).GetField("_effectRoot", Fields);
            firstRoot = (GameObject)rootField.GetValue(first);
            secondRoot = (GameObject)rootField.GetValue(second);
            EnvironmentDustEffect[] firstEffects = firstRoot.GetComponentsInChildren<EnvironmentDustEffect>(true);
            EnvironmentDustEffect[] secondEffects = secondRoot.GetComponentsInChildren<EnvironmentDustEffect>(true);
            Check(firstEffects.Length == 1 && secondEffects.Length == 1,
                "One instance per area; duplicate prefab ignored", ref checks);
            Check(firstEffects[0] != secondEffects[0], "Areas do not share effect state", ref checks);
            Check(first.Initialize(camera) && (GameObject)rootField.GetValue(first) == firstRoot,
                "Repeated initialization does not duplicate effects", ref checks);
            Check(firstRoot.transform.parent == null && secondRoot.transform.parent == null,
                "Effect roots do not inherit area transforms", ref checks);
            Physics2D.SyncTransforms();
            MethodInfo sample = typeof(EnvironmentDustEffect).GetMethod("TryGetSpawnPosition", Fields);
            object[] arguments = { Vector2.zero };
            for (int i = 0; i < 100; i++)
            {
                Check((bool)sample.Invoke(firstEffects[0], arguments)
                    && firstBox.OverlapPoint((Vector2)arguments[0]), "First effect samples only first area", ref checks);
                Check((bool)sample.Invoke(secondEffects[0], arguments)
                    && secondBox.OverlapPoint((Vector2)arguments[0]), "Second effect samples only second area", ref checks);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(firstObject);
            UnityEngine.Object.DestroyImmediate(secondObject);
            if (firstRoot != null) UnityEngine.Object.DestroyImmediate(firstRoot);
            if (secondRoot != null) UnityEngine.Object.DestroyImmediate(secondRoot);
        }
    }

    private static void Check(bool condition, string message, ref int checks)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks++;
    }
}
