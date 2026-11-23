using UnityEditor;
using UnityEngine;

/// <summary>씬에 배치한 독안개를 선택했을 때만 편집용 미리보기를 재생합니다.</summary>
[CustomEditor(typeof(PoisonFogEffect))]
[CanEditMultipleObjects]
public sealed class PoisonFogEffectEditor : Editor
{
    private double _lastPreviewTime;

    private void OnEnable()
    {
        _lastPreviewTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += UpdatePreview;
    }

    private void OnDisable()
    {
        EditorApplication.update -= UpdatePreview;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Density: 두 안개층의 농도. 0 = 숨김, 1 = 가장 짙음.\nArea Size: 타원형 발생 영역. 입자는 영역 밖까지 퍼집니다.\nSpread: 입자 크기와 확산 이동 배율.\n씬 또는 Prefab Mode에서 선택하면 미리보기가 재생됩니다. 농도는 독 피해량에 영향을 주지 않습니다.", MessageType.Info);
    }

    private void UpdatePreview()
    {
        double now = EditorApplication.timeSinceStartup;
        float deltaTime = (float)(now - _lastPreviewTime);
        if (deltaTime < 1f / 30f)
            return;
        _lastPreviewTime = now;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        bool previewed = false;
        foreach (Object selected in targets)
        {
            PoisonFogEffect effect = selected as PoisonFogEffect;
            if (effect == null || EditorUtility.IsPersistent(effect) || !effect.isActiveAndEnabled)
                continue;
            effect.AdvanceEditorPreview(deltaTime);
            previewed = true;
        }
        if (previewed)
            SceneView.RepaintAll();
    }
}
