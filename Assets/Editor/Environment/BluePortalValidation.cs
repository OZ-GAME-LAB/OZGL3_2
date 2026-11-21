using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>열린 씬을 저장하거나 변경하지 않고 포탈 연결과 렌더링을 검사합니다.</summary>
public static class BluePortalValidation
{
    private const string OutputDirectory = ".utmp/blue-portal";

    [InitializeOnLoadMethod]
    private static void ScheduleRequestedValidation()
    {
        if (File.Exists(OutputDirectory + "/validate.request"))
            EditorApplication.delayCall += RunRequestedValidation;
    }

    private static void RunRequestedValidation()
    {
        string requestPath = OutputDirectory + "/validate.request";
        if (!File.Exists(requestPath))
            return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += RunRequestedValidation;
            return;
        }
        File.Delete(requestPath);
        try
        {
            Validate();
        }
        catch (Exception exception)
        {
            File.WriteAllText(OutputDirectory + "/validation.txt", exception.ToString());
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/Environment/Validate And Preview Blue Portal")]
    public static void Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play Mode를 종료한 뒤 실행해 주세요.");
        BluePortalSetup.Build();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BluePortalSetup.PrefabPath);
        foreach (string dependency in AssetDatabase.GetDependencies(BluePortalSetup.PrefabPath))
        {
            if (dependency.StartsWith("Assets/AIWork", StringComparison.Ordinal))
                throw new InvalidOperationException("실험용 폴더 참조: " + dependency);
        }
        foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
        {
            Material material = renderer.sharedMaterial;
            if (material == null || material.shader == null || ShaderUtil.ShaderHasError(material.shader))
                throw new InvalidOperationException("머티리얼 또는 셰이더 오류: " + renderer.name);
        }
        if (prefab.GetComponentsInChildren<Collider>(true).Length != 0)
            throw new InvalidOperationException("시각 효과에 불필요한 Collider가 있습니다.");

        Directory.CreateDirectory(OutputDirectory);
        Scene previewScene = EditorSceneManager.NewPreviewScene();
        RenderTexture target = new RenderTexture(640, 800, 24, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        Texture2D image = null;
        try
        {
            GameObject portal = (GameObject)PrefabUtility.InstantiatePrefab(prefab, previewScene);
            ParticleSystem particles = portal.GetComponentInChildren<ParticleSystem>();
            if (particles == null || !particles.main.loop || !particles.main.playOnAwake)
                throw new InvalidOperationException("반복 자동 재생 설정을 확인해 주세요.");
            particles.useAutoRandomSeed = false;
            particles.randomSeed = 12345;
            particles.Simulate(2f, true, true);
            int particleCount = particles.particleCount;
            if (particleCount == 0 || particleCount > 48)
                throw new InvalidOperationException("입자 수 확인 실패: " + particleCount);

            GameObject cameraObject = new GameObject("PortalPreviewCamera");
            SceneManager.MoveGameObjectToScene(cameraObject, previewScene);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.scene = previewScene;
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 2.6f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.018f, 0.026f, 0.045f, 1f);
            camera.allowHDR = false;
            camera.targetTexture = target;
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            RenderPipeline.SubmitRenderRequest(camera, request);

            RenderTexture.active = target;
            image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(OutputDirectory + "/BluePortal-preview.png", image.EncodeToPNG());

            int bluePixels = 0;
            foreach (Color32 pixel in image.GetPixels32())
            {
                if (pixel.b > 100 && pixel.b > pixel.r + 30)
                    bluePixels++;
            }
            if (bluePixels < 1000)
                throw new InvalidOperationException("포탈 렌더링 결과 확인 필요: " + bluePixels);
            foreach (Renderer renderer in portal.GetComponentsInChildren<Renderer>())
            {
                if (ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader))
                    throw new InvalidOperationException("렌더링 이후 셰이더 오류: " + renderer.name);
            }
            File.WriteAllText(OutputDirectory + "/validation.txt",
                "PASS: prefab references, shaders, no colliders, looping particles, URP render without bloom.\n"
                + "Particles at 2 seconds: " + particleCount + "\nBlue pixels: " + bluePixels
                + "\nPreview: " + OutputDirectory + "/BluePortal-preview.png\n");
            Debug.Log("BluePortal 검증 완료. 미리보기: " + OutputDirectory + "/BluePortal-preview.png");
        }
        finally
        {
            RenderTexture.active = previous;
            if (image != null)
                UnityEngine.Object.DestroyImmediate(image);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            EditorSceneManager.ClosePreviewScene(previewScene);
        }
    }
}
