using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 명시적인 요청 파일이 있을 때만 연결 복구와 현재 씬의 짧은 실행 검사를 수행합니다.
[InitializeOnLoad]
public static class EnvironmentDustConnectionRecovery
{
    private const string Request = "Temp/GlowDust.Restore.request";
    private const string Result = "Temp/GlowDust.Restore.txt";
    private const string Stage = "GlowDustRecovery.Stage";
    private const string WasPlaying = "GlowDustRecovery.WasPlaying";
    private static double _started;

    static EnvironmentDustConnectionRecovery()
    {
        EditorApplication.playModeStateChanged += OnModeChanged;
        EditorApplication.delayCall += Resume;
    }

    private static void Resume()
    {
        if (SessionState.GetString(Stage, "") == "checking" && EditorApplication.isPlaying)
        {
            BeginCheck();
            return;
        }
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += Resume;
            return;
        }
        File.Delete(Request);
        SessionState.SetBool(WasPlaying, EditorApplication.isPlaying);
        if (EditorApplication.isPlaying)
        {
            SessionState.SetString(Stage, "restore");
            EditorApplication.isPlaying = false;
        }
        else Restore();
    }

    private static void OnModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetString(Stage, "") == "restore")
            EditorApplication.delayCall += Restore;
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetString(Stage, "") == "checking")
            BeginCheck();
    }

    private static void Restore()
    {
        try
        {
            var primary = AssetDatabase.LoadAssetAtPath<EnvironmentPresentationProfile>(EnvironmentDustSetup.ProfilePath);
            var secondary = AssetDatabase.LoadAssetAtPath<EnvironmentPresentationProfile>(EnvironmentDustSetup.SecondaryProfilePath);
            if (primary == null || secondary == null) throw new InvalidOperationException("Existing profiles missing.");
            typeof(EnvironmentDustSetup).GetMethod("ConnectMap", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { primary, secondary });
            EnvironmentDustValidation.Validate();
            File.WriteAllText(Result, "Map connections restored; native render validation passed. Starting Play Mode check.\n");
            SessionState.SetString(Stage, "checking");
            EditorApplication.isPlaying = true;
        }
        catch (Exception error)
        {
            File.WriteAllText(Result, error.ToString());
            SessionState.EraseString(Stage);
        }
    }

    private static void BeginCheck()
    {
        _started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Check;
        EditorApplication.update += Check;
    }

    private static void Check()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup - _started < 10) return;
        EditorApplication.update -= Check;
        try
        {
            StringBuilder report = new StringBuilder();
            var controllers = UnityEngine.Object.FindObjectsByType<EnvironmentPresentationController>(FindObjectsSortMode.None);
            report.AppendLine("Runtime controllers: " + controllers.Length);
            foreach (var controller in controllers)
                report.AppendLine("Area: " + (controller.CurrentArea != null ? controller.CurrentArea.name : "none")
                    + "; Profile: " + (controller.CurrentProfile != null ? controller.CurrentProfile.name : "none"));
            var effects = UnityEngine.Object.FindObjectsByType<EnvironmentDustEffect>(FindObjectsSortMode.None);
            report.AppendLine("Active dust effects: " + effects.Length);
            int total = 0;
            foreach (var effect in effects)
            {
                foreach (var ps in effect.GetComponentsInChildren<ParticleSystem>())
                {
                    var renderer = ps.GetComponent<ParticleSystemRenderer>();
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    report.AppendLine(ps.name + ": particles=" + ps.particleCount + ", playing=" + ps.isPlaying
                        + ", emitting=" + ps.emission.enabled + ", rendererVisible=" + renderer.isVisible
                        + ", color=" + block.GetColor("_BaseColor") + ", viewport=" + block.GetVector("_DustBounds"));
                    total += ps.particleCount;
                }
            }
            report.AppendLine(total > 0 ? "PASS: Actual Play Mode particles generated." : "FAIL: No live particles; inspect current area/player initialization.");
            File.AppendAllText(Result, report.ToString());
        }
        catch (Exception error) { File.AppendAllText(Result, error.ToString()); }
        finally
        {
            SessionState.EraseString(Stage);
            if (!SessionState.GetBool(WasPlaying, false)) EditorApplication.isPlaying = false;
        }
    }
}
