using UnityEngine;

/// <summary>창 크기와 무관하게 1920:1080 테스트 화면 비율을 유지합니다.</summary>
[RequireComponent(typeof(Camera))]
public class InteractionTestViewport : MonoBehaviour
{
    private Camera _camera;
    private int _width;
    private int _height;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        RefreshViewport();
    }

    private void Update()
    {
        if (Screen.width != _width || Screen.height != _height)
            RefreshViewport();
    }

    private void RefreshViewport()
    {
        _width = Screen.width;
        _height = Screen.height;
        float scale = (_width / (float)Mathf.Max(1, _height)) / (16f / 9f);
        _camera.rect = scale < 1f
            ? new Rect(0f, (1f - scale) * 0.5f, 1f, scale)
            : new Rect((1f - 1f / scale) * 0.5f, 0f, 1f / scale, 1f);
        _camera.aspect = 16f / 9f;
    }
}
