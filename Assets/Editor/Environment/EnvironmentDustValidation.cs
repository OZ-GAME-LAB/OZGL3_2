using System;
using System.IO;
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

    [MenuItem("Tools/Environment/Validate Glow Dust")]
    public static void Validate()
    {
        EnvironmentEffectAreaValidation.Validate();
        File.Copy("Temp/EffectArea.Validation.txt", ResultPath, true);
    }
}