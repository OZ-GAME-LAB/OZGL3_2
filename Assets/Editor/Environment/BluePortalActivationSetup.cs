using System;
using UnityEditor;
using UnityEngine;

/// <summary>기존 포탈 외형을 재사용하여 좌우 열림 전용 프리팹을 생성합니다.</summary>
public static class BluePortalActivationSetup
{
    public const string PrefabPath = "Assets/2_Prefabs/World/Effects/BluePortalActivation.prefab";

    [MenuItem("Tools/Environment/Create Blue Portal Activation")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play Mode를 종료한 뒤 실행해 주세요.");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BluePortalSetup.PrefabPath) == null)
            throw new InvalidOperationException("BluePortal 프리팹을 먼저 생성해 주세요.");

        GameObject root = PrefabUtility.LoadPrefabContents(BluePortalSetup.PrefabPath);
        try
        {
            root.name = "BluePortalActivation";
            MeshRenderer surface = root.GetComponentInChildren<MeshRenderer>(true);
            ParticleSystem particles = root.GetComponentInChildren<ParticleSystem>(true);
            if (surface == null || particles == null)
                throw new InvalidOperationException("포탈 본체와 입자 연결을 확인해 주세요.");
            particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.prewarm = false;

            PortalActivationEffect effect = root.AddComponent<PortalActivationEffect>();
            SerializedObject serialized = new SerializedObject(effect);
            serialized.FindProperty("_surface").objectReferenceValue = surface;
            serialized.FindProperty("_rimSparkles").objectReferenceValue = particles;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                throw new InvalidOperationException("포탈 활성화 프리팹 저장에 실패했습니다.");
            Debug.Log("BluePortalActivation 생성 완료: " + PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
