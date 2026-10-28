using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>열린 씬을 변경하지 않는 Unity 입자/전환 검사입니다.</summary>
public static class EnvironmentDustValidation
{
    private const string RequestPath = "Temp/GlowDust.Build.request";
    private const string ResultPath = "Temp/GlowDust.Validation.txt";

    [InitializeOnLoadMethod]
    private static void ScheduleRequestedBuild()
    {
        if (File.Exists(RequestPath))
            EditorApplication.delayCall += RunRequestedBuild;
    }

    private static void RunRequestedBuild()
    {
        if (!File.Exists(RequestPath))
            return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += RunRequestedBuild;
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            File.WriteAllText(ResultPath, "WAIT: Exit Play Mode before building assets.");
            return;
        }
        File.Delete(RequestPath);
        try
        {
            EnvironmentDustSetup.Build();
            Validate();
        }
        catch (Exception exception)
        {
            File.WriteAllText(ResultPath, exception.ToString());
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/Environment/Validate Glow Dust And Save Preview")]
    public static void Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play Mode 밖에서 검사해 주세요.");
        StringBuilder report = new StringBuilder();
        PreviewRenderUtility preview = new PreviewRenderUtility();
        GameObject controllerObject = null;
        try
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentDustSetup.PrefabPath);
            Check(prefab != null, "Effect prefab exists", report);
            GameObject root = UnityEngine.Object.Instantiate(prefab);
            preview.AddSingleGO(root);
            EnvironmentDustEffect effect = root.GetComponent<EnvironmentDustEffect>();
            Camera camera = preview.camera;
            camera.orthographic = true;
            camera.orthographicSize = 16.875f;
            camera.aspect = 16f / 9f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.012f, 0.025f, 0.035f, 1f);
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 100f;
            Check(effect.Initialize(camera), "Dust Prepare succeeds with real Unity components", report);
            effect.RefreshCamera();
            ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
            Check(systems.Length == 2, "Two particle layers", report);

            controllerObject = new GameObject("DustTransitionValidation");
            preview.AddSingleGO(controllerObject);
            EnvironmentPresentationController controller = controllerObject.AddComponent<EnvironmentPresentationController>();
            Type stateType = typeof(EnvironmentPresentationController).GetNestedType("EffectState", BindingFlags.NonPublic);
            object state = Activator.CreateInstance(stateType);
            stateType.GetField("Instance").SetValue(state, effect);
            SetTarget(controller, state, 1f, 2f);
            // Edit Mode 검사는 런타임 OnEnable 이벤트를 명시적으로 실행합니다.
            typeof(EnvironmentDustEffect).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(effect, null);
            Step(controller, state, 2f);
            for (int i = 0; i < systems.Length; i++)
            {
                systems[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                systems[i].useAutoRandomSeed = false;
                systems[i].randomSeed = (uint)(143 + i);
                systems[i].Simulate(8f, false, true, true);
            }
            int before = systems[0].particleCount;
            Check(before > 0 && before <= 40, "Small particles spawn within cap", report);
            Check(systems[1].particleCount <= 6, "Near particles respect cap", report);
            Transform space = systems[0].main.customSimulationSpace;
            Check(systems[0].main.simulationSpace == ParticleSystemSimulationSpace.Custom, "Custom simulation space assigned", report);
            Vector3 oldSpace = space.position;
            camera.transform.position += new Vector3(5f, 2f, 0f);
            effect.RefreshCamera();
            Check(Mathf.Abs(space.position.x - oldSpace.x + 0.5f) < 0.001f,
                "Camera +5 moves fine simulation anchor -0.5 (screen ratio 1.1)", report);
            Check(Mathf.Abs(space.position.y - oldSpace.y + 0.1f) < 0.001f, "Vertical parallax 1.05", report);
            camera.transform.position -= new Vector3(5f, 2f, 0f);
            effect.RefreshCamera();
            MaterialPropertyBlock diagnostic = new MaterialPropertyBlock();
            systems[0].GetComponent<ParticleSystemRenderer>().GetPropertyBlock(diagnostic);
            File.WriteAllText("Temp/GlowDust.ShaderDiagnostics.txt", "Color=" + diagnostic.GetColor("_BaseColor")
                + " Bounds=" + diagnostic.GetVector("_DustBounds") + " Fade=" + diagnostic.GetVector("_DustFade")
                + " Camera=" + diagnostic.GetVector("_DustCameraPosition") + " Right=" + diagnostic.GetVector("_DustCameraRight")
                + " Up=" + diagnostic.GetVector("_DustCameraUp") + " Shape=" + systems[0].shape.scale.ToString("F5"));
            float fullBrightness = SavePreview(preview, "Temp/GlowDust.Preview.png");
            Check(fullBrightness > 0.01f, "Particles actually render above background: " + fullBrightness, report);

            SetTarget(controller, state, 0f, 2f);
            Check(!systems[0].emission.enabled && !systems[1].emission.enabled, "Exit immediately stops emission", report);
            Check(systems[0].particleCount == before, "Exit preserves living particles", report);
            Step(controller, state, 1f);
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            ParticleSystemRenderer renderer = systems[0].GetComponent<ParticleSystemRenderer>();
            renderer.GetPropertyBlock(block);
            Color half = block.GetColor("_BaseColor");
            Color original = renderer.sharedMaterial.GetColor("_BaseColor");
            Check(Mathf.Abs(half.r - original.r * 0.5f) < 0.001f, "Live particles fade through renderer property block", report);
            Check(original.r > half.r, "Shared material is not modified", report);
            float halfBrightness = SavePreview(preview, null);
            Check(halfBrightness > 0f && halfBrightness < fullBrightness * 0.9f,
                "Rendered particles visibly dim at half intensity", report);
            SetTarget(controller, state, 1f, 2f);
            Check(systems[0].emission.enabled, "Reentry resumes emission", report);
            Check(systems[0].particleCount == before, "Reentry keeps existing particles", report);
            Step(controller, state, 0f);
            renderer.GetPropertyBlock(block);
            Check(Mathf.Abs(block.GetColor("_BaseColor").r - half.r) < 0.001f, "Reentry retains current intensity", report);
            SetTarget(controller, state, 0f, 0f);
            Check(!root.activeSelf && systems[0].particleCount == 0 && systems[1].particleCount == 0,
                "Zero-duration exit clears and disables", report);

            SetTarget(controller, state, 1f, 0f);
            typeof(EnvironmentDustEffect).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(effect, null);
            Check(systems[0].emission.enabled, "Cached effect restarts", report);
            effect.ResetEffect();
            camera.transform.position = new Vector3(500f, 100f, -10f);
            effect.RefreshCamera();
            Check(systems[0].particleCount == 0 && Mathf.Abs(space.position.x - 500f) < 0.001f,
                "Teleport reset clears particles and rebases simulation", report);
            ValidateViewportBounds(preview, effect, systems, report);
            ValidateMap(report);
            File.WriteAllText(ResultPath, report.ToString());
            Debug.Log("GlowDust validation passed. " + ResultPath);
        }
        finally { preview.Cleanup(); }
    }

    private static void ValidateViewportBounds(PreviewRenderUtility preview, EnvironmentDustEffect effect,
        ParticleSystem[] systems, StringBuilder report)
    {
        Camera camera = preview.camera;
        effect.RefreshCamera();
        float height = camera.orthographicSize * 2f;
        Check(Mathf.Abs(systems[0].shape.scale.y - height * 0.74f) < 0.001f,
            "Spawn height is 74 percent of viewport", report);
        float centerY = camera.WorldToViewportPoint(systems[0].transform.position).y;
        Check(Mathf.Abs(centerY - 0.47f) < 0.001f, "Spawn center is at viewport Y 0.47", report);
        effect.ApplyIntensity(1f);
        float center = RenderParticleAt(preview, systems[0], new Vector2(0.5f, 0.47f));
        float edge = RenderParticleAt(preview, systems[0], new Vector2(0.5f, 0.13f));
        float bottom = RenderParticleAt(preview, systems[0], new Vector2(0.5f, 0.04f));
        float top = RenderParticleAt(preview, systems[0], new Vector2(0.5f, 0.94f));
        Check(center > 0.01f && edge > 0f && edge < center * 0.9f,
            "Particle approaching lower boundary fades smoothly in actual render (center=" + center
            + ", edge=" + edge + ", bottom=" + bottom + ", top=" + top + ")", report);
        Check(bottom < center * 0.001f && top < center * 0.001f,
            "Particles outside top/bottom bounds are invisible", report);
    }

    private static float RenderParticleAt(PreviewRenderUtility preview, ParticleSystem particles, Vector2 viewport)
    {
        particles.Clear(false);
        particles.Play(false);
        Vector3 world = preview.camera.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, 5f));
        ParticleSystem.EmitParams emission = new ParticleSystem.EmitParams
        {
            position = particles.main.customSimulationSpace.InverseTransformPoint(world),
            velocity = Vector3.zero,
            startLifetime = 10f,
            startSize = 0.4f,
            startColor = Color.white,
            applyShapeToPosition = false
        };
        particles.Emit(emission, 1);
        // 수명 0 지점은 기존 알파 곡선이 투명하므로 검사 동안 곡선만 끕니다.
        ParticleSystem.ColorOverLifetimeModule colors = particles.colorOverLifetime;
        colors.enabled = false;
        particles.Simulate(0.001f, false, false, false);
        ParticleSystem.Particle[] debugParticles = new ParticleSystem.Particle[2];
        int count = particles.GetParticles(debugParticles);
        if (count != 1)
            throw new InvalidOperationException("경계 검사에 사용할 입자 1개를 생성하지 못했습니다: " + count);
        File.AppendAllText("Temp/GlowDust.ShaderDiagnostics.txt", "\nRequested=" + viewport
            + " count=" + count + " active=" + particles.gameObject.activeInHierarchy
            + " camera=" + preview.camera.transform.position + " space=" + particles.main.customSimulationSpace.position
            + " particle=" + debugParticles[0].position + " bounds=" + particles.GetComponent<ParticleSystemRenderer>().bounds);
        return SavePreview(preview, null);
    }

    private static void SetTarget(EnvironmentPresentationController controller, object state, float target, float duration)
    {
        typeof(EnvironmentPresentationController).GetMethod("SetTarget", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(controller, new[] { state, (object)target, duration });
    }

    private static void Step(EnvironmentPresentationController controller, object state, float delta)
    {
        typeof(EnvironmentPresentationController).GetMethod("UpdateTransition", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(controller, new[] { state, (object)delta });
    }

    private static void ValidateMap(StringBuilder report)
    {
        GameObject map = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/SungjunWork.prefab");
        EnvironmentPresentationController controller = map.GetComponentInChildren<EnvironmentPresentationController>(true);
        Check(controller != null, "Map controller connected", report);
        EnvironmentPresentationArea[] areas = map.GetComponentsInChildren<EnvironmentPresentationArea>(true);
        Check(areas.Length == 2, "Two independent presentation areas connected", report);
        for (int i = 0; i < areas.Length; i++)
            Check(areas[i].Profile != null && areas[i].Profile.GetEffect(0).Prefab != null,
                "Area profile and prefab references valid: " + areas[i].name, report);
        Check(map.GetComponentInChildren<ParallaxBackground>(true) != null, "Existing Parallax retained", report);
    }

    private static float SavePreview(PreviewRenderUtility preview, string path)
    {
        preview.BeginPreview(new Rect(0, 0, 1280, 720), GUIStyle.none);
        bool previousAsync = ShaderUtil.allowAsyncCompilation;
        try
        {
            ShaderUtil.allowAsyncCompilation = false;
            preview.Render(true);
        }
        finally { ShaderUtil.allowAsyncCompilation = previousAsync; }
        Texture result = preview.EndPreview();
        RenderTexture output = RenderTexture.GetTemporary(1280, 720, 0, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        Texture2D image = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
        try
        {
            Graphics.Blit(result, output);
            RenderTexture.active = output;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            if (path != null)
                File.WriteAllBytes(path, image.EncodeToPNG());
            Color[] pixels = image.GetPixels();
            float background = pixels[0].grayscale;
            float brightness = 0f;
            for (int i = 0; i < pixels.Length; i++)
                brightness += Mathf.Max(0f, pixels[i].grayscale - background);
            return brightness;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(output);
            UnityEngine.Object.DestroyImmediate(image);
        }
    }

    private static void Check(bool success, string label, StringBuilder report)
    {
        if (!success)
            throw new InvalidOperationException("FAIL: " + label + "\n" + report);
        report.AppendLine("PASS: " + label);
    }
}
