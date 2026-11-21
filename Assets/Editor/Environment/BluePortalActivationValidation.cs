using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>기존 씬을 변경하지 않고 열림 상태와 단계별 Unity 렌더링을 검사합니다.</summary>
public static class BluePortalActivationValidation
{
    private const string Output = ".utmp/blue-portal/activation";

    [InitializeOnLoadMethod]
    private static void Schedule()
    {
        if (File.Exists(Output + "/validate.request"))
            EditorApplication.delayCall += RunRequested;
    }

    private static void RunRequested()
    {
        if (!File.Exists(Output + "/validate.request")) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += RunRequested;
            return;
        }
        File.Delete(Output + "/validate.request");
        try { Validate(); }
        catch (Exception exception)
        {
            File.WriteAllText(Output + "/validation.txt", exception.ToString());
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/Environment/Validate Blue Portal Activation")]
    public static void Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play Mode를 종료한 뒤 실행해 주세요.");
        Directory.CreateDirectory(Output);
        BluePortalActivationSetup.Build();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BluePortalActivationSetup.PrefabPath);
        GameObject original = AssetDatabase.LoadAssetAtPath<GameObject>(BluePortalSetup.PrefabPath);
        foreach (string dependency in AssetDatabase.GetDependencies(BluePortalActivationSetup.PrefabPath))
            Require(!dependency.StartsWith("Assets/AIWork", StringComparison.Ordinal), "No AIWork dependency");
        Scene scene = EditorSceneManager.NewPreviewScene();
        RenderTexture target = new RenderTexture(320, 400, 24);
        RenderTexture previous = RenderTexture.active;
        Texture2D image = new Texture2D(320, 400, TextureFormat.RGB24, false);
        Texture2D strip = new Texture2D(1280, 400, TextureFormat.RGB24, false);
        try
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            PortalActivationEffect effect = instance.GetComponent<PortalActivationEffect>();
            MeshRenderer surface = instance.GetComponentInChildren<MeshRenderer>();
            ParticleSystem particles = instance.GetComponentInChildren<ParticleSystem>();
            MeshRenderer sourceSurface = original.GetComponentInChildren<MeshRenderer>();
            ParticleSystem sourceParticles = original.GetComponentInChildren<ParticleSystem>();
            Vector3 fullScale = sourceSurface.transform.localScale;
            Vector3 fullShape = sourceParticles.shape.scale;
            float fullRate = sourceParticles.emission.rateOverTimeMultiplier;
            Require(surface.sharedMaterial == sourceSurface.sharedMaterial, "Reuse original material");
            Require(!particles.main.playOnAwake && !particles.main.prewarm, "No particles before opening");

            Invoke(effect, "Awake");
            Require(!surface.enabled && particles.particleCount == 0 && !effect.IsOpen, "Closed initially");
            Invoke(effect, "OnEnable");
            Require(effect.IsOpening && surface.enabled, "Auto open on enable");
            Require(Mathf.Approximately(surface.transform.localScale.x, fullScale.x * 0.02f), "Thin vertical slit");
            effect.Open();
            Require(effect.IsOpening, "Duplicate open ignored");

            GameObject cameraObject = new GameObject("ActivationPreviewCamera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.scene = scene;
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 2.6f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.018f, 0.026f, 0.045f, 1f);
            camera.allowHDR = false;
            camera.targetTexture = target;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;

            float previousWidth = surface.transform.localScale.x;
            for (int frame = 0; frame < 4; frame++)
            {
                if (frame > 0)
                {
                    float step = frame == 3 ? 0.4f : 0.2f;
                    Invoke(effect, "AdvanceOpening", step);
                    particles.Simulate(step, false, false, false);
                }
                Vector3 scale = surface.transform.localScale;
                Require(scale.x >= previousWidth && scale.x <= fullScale.x, "Width grows without overshoot");
                Require(scale.y == fullScale.y && scale.z == fullScale.z, "Height and depth unchanged");
                Require(Mathf.Approximately(particles.shape.scale.x / fullShape.x, scale.x / fullScale.x), "Emission follows width");
                Require(particles.transform.localScale == sourceParticles.transform.localScale, "Particle transform unchanged");
                previousWidth = scale.x;
                RenderPipeline.SubmitRenderRequest(camera,
                    new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 320, 400), 0, 0);
                image.Apply();
                strip.SetPixels(frame * 320, 0, 320, 400, image.GetPixels());
            }
            strip.Apply();
            File.WriteAllBytes(Output + "/opening-stages.png", strip.EncodeToPNG());
            Require(effect.IsOpen && !effect.IsOpening, "Open after 0.8 seconds");
            Require(surface.transform.localScale == fullScale && particles.shape.scale == fullShape, "Exact original size");
            Require(particles.emission.rateOverTimeMultiplier == fullRate, "Original emission restored");
            effect.Open();
            Require(effect.IsOpen && !effect.IsOpening, "Already-open call ignored");

            Invoke(effect, "OnDisable");
            Require(!surface.enabled && particles.particleCount == 0 && !effect.IsOpen && !effect.IsOpening, "Disable clears state");
            Invoke(effect, "OnEnable");
            Require(effect.IsOpening && Mathf.Approximately(surface.transform.localScale.x, fullScale.x * 0.02f), "Reenable restarts slit");
            Invoke(effect, "AdvanceOpening", 0.25f);
            float widthBeforePause = surface.transform.localScale.x;
            Invoke(effect, "AdvanceOpening", 0f);
            Require(surface.transform.localScale.x == widthBeforePause, "Zero delta does not advance");
            Invoke(effect, "OnDisable");
            Require(!effect.IsOpening && !surface.enabled && particles.particleCount == 0, "Disable midway clears state");

            SerializedObject settings = new SerializedObject(effect);
            settings.FindProperty("_openOnEnable").boolValue = false;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Invoke(effect, "OnEnable");
            Require(!surface.enabled && !effect.IsOpening, "Manual mode stays closed");
            settings.FindProperty("_openDuration").floatValue = 0f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            effect.Open();
            Require(effect.IsOpen && surface.transform.localScale == fullScale, "Zero duration opens immediately");
            effect.ResetClosed();
            Require(!surface.enabled && !effect.IsOpen, "Manual reset closes");
            Require(!ShaderUtil.ShaderHasError(surface.sharedMaterial.shader), "No shader errors");
            File.WriteAllText(Output + "/validation.txt",
                "PASS: Unity editor compilation and preview-scene state/render checks.\n"
                + "Checked: closed state, auto/manual open, duplicate calls, constant height, expanding emission, "
                + "unchanged particle transform, original final size/rate, completion, disable midway, reenable, zero delta, zero duration.\n"
                + "Preview samples: 0, 0.2, 0.4, 0.8 seconds.\n"
                + "AdvanceOpening was stepped explicitly in Edit Mode; real-time Play Mode not exercised.\n");
            Debug.Log("BluePortalActivation 검증 완료: " + Output);
        }
        finally
        {
            RenderTexture.active = previous;
            EditorSceneManager.ClosePreviewScene(scene);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(strip);
        }
    }

    private static void Invoke(PortalActivationEffect effect, string name, params object[] arguments)
    {
        MethodInfo method = typeof(PortalActivationEffect).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null) throw new MissingMethodException(name);
        method.Invoke(effect, arguments);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + message);
    }
}
