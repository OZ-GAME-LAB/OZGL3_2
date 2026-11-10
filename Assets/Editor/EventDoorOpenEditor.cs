using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>게임 실행 없이 문 열림을 미리 보고 원래 위치로 복원합니다.</summary>
[CustomEditor(typeof(EventDoorOpen))]
public sealed class EventDoorOpenEditor : Editor
{
    private AnimationClip _previewClip;
    private GameObject _previewObject;
    private double _startedAt;
    private float _duration;
    private bool _ownsPreview;

    private void OnEnable()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload += RestorePosition;
        EditorSceneManager.sceneSaving += OnSceneSaving;
    }

    private void OnDisable()
    {
        RestorePosition();
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= RestorePosition;
        EditorSceneManager.sceneSaving -= OnSceneSaving;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EventDoorOpen door = (EventDoorOpen)target;
        bool canPreview = !EditorApplication.isPlayingOrWillChangePlaymode
            && !EditorUtility.IsPersistent(door)
            && (!AnimationMode.InAnimationMode() || _ownsPreview);

        using (new EditorGUI.DisabledScope(!canPreview))
        {
            if (GUILayout.Button("미리보기 재생"))
                PlayPreview(door);
        }

        using (new EditorGUI.DisabledScope(!_ownsPreview))
        {
            if (GUILayout.Button("원위치 복원"))
                RestorePosition();
        }

        EditorGUILayout.HelpBox(
            "씬에 배치한 문 또는 Prefab Mode에서 사용하세요. 선택 해제, 씬 저장, 게임 실행 시 원위치로 복원됩니다.",
            MessageType.Info);
    }

    private void PlayPreview(EventDoorOpen door)
    {
        RestorePosition();
        SpriteRenderer spriteRenderer = door.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null || spriteRenderer.sprite == null)
        {
            Debug.LogWarning("미리보기에 사용할 문 이미지가 필요합니다.", door);
            return;
        }

        // Inspector의 직렬화 값을 읽어 런타임 클래스의 필드 접근 범위를 유지합니다.
        serializedObject.Update();
        _duration = Mathf.Max(0f, serializedObject.FindProperty("_openDuration").floatValue);
        float percent = Mathf.Clamp(serializedObject.FindProperty("_movePercent").floatValue, 0f, 100f);
        Transform doorTransform = door.transform;
        Vector3 start = doorTransform.localPosition;
        Vector3 worldTarget = doorTransform.position + Vector3.up * (spriteRenderer.bounds.size.y * percent * 0.01f);
        Vector3 end = doorTransform.parent == null
            ? worldTarget : doorTransform.parent.InverseTransformPoint(worldTarget);

        _previewObject = door.gameObject;
        _previewClip = new AnimationClip { hideFlags = HideFlags.HideAndDontSave };
        // 클립 시간은 0~1로 두고 실제 재생 시간은 에디터 시계로 계산합니다.
        SetPositionCurve("m_LocalPosition.x", start.x, end.x);
        SetPositionCurve("m_LocalPosition.y", start.y, end.y);
        SetPositionCurve("m_LocalPosition.z", start.z, end.z);

        AnimationMode.StartAnimationMode();
        _ownsPreview = true;
        _startedAt = EditorApplication.timeSinceStartup;
        SamplePreview(_duration <= 0f ? 1f : 0f);
        if (_duration > 0f)
            EditorApplication.update += UpdatePreview;
    }

    private void SetPositionCurve(string propertyName, float start, float end)
    {
        _previewClip.SetCurve(string.Empty, typeof(Transform), propertyName,
            AnimationCurve.Linear(0f, start, 1f, end));
    }

    private void UpdatePreview()
    {
        if (_previewObject == null || !AnimationMode.InAnimationMode())
        {
            RestorePosition();
            return;
        }

        float progress = Mathf.Clamp01((float)((EditorApplication.timeSinceStartup - _startedAt) / _duration));
        SamplePreview(progress);
        if (progress >= 1f)
            EditorApplication.update -= UpdatePreview;
    }

    private void SamplePreview(float progress)
    {
        AnimationMode.BeginSampling();
        AnimationMode.SampleAnimationClip(_previewObject, _previewClip, progress);
        AnimationMode.EndSampling();
        SceneView.RepaintAll();
    }

    private void RestorePosition()
    {
        EditorApplication.update -= UpdatePreview;
        if (_ownsPreview)
        {
            AnimationMode.StopAnimationMode();
            _ownsPreview = false;
            SceneView.RepaintAll();
        }

        if (_previewClip != null)
            DestroyImmediate(_previewClip);
        _previewClip = null;
        _previewObject = null;
    }

    private void OnPlayModeChanged(PlayModeStateChange state)
    {
        RestorePosition();
    }

    private void OnSceneSaving(Scene scene, string path)
    {
        RestorePosition();
    }
}
