using System.Collections;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

// 맵에 배치한 문 카메라와 이 화면에서만 보이는 문 그림을 관리합니다.
[RequireComponent(typeof(CinemachineCamera))]
public sealed class PuzzleDoorShot : MonoBehaviour
{
    [SerializeField] private CinematicEffectUI _barsPrefab;
    [SerializeField] private AudioClip _riseSound;
    [SerializeField, Range(0f, 1f)] private float _volume = 0.32f;
    [SerializeField, Min(1f)] private float _hearingDistance = 24f;
    [SerializeField, Min(0f)] private float _holdSeconds = 0.55f;

    private CinemachineCamera _camera;
    private PlayerControllerView _player;
    private GameObject _canvas;
    private SpriteRenderer _original;
    private SpriteRenderer _picture;
    private bool _previousForceOff;

    private void Awake() => _camera = GetComponent<CinemachineCamera>();

    internal void Present(EventDoorOpen door, bool solver)
    {
        var local = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        if (local == null || !local.TryGetComponent(out PlayerControllerView player)) return;
        if (!solver || !player.CanWatchDoor)
        {
            float distance = Vector2.Distance(player.transform.position, door.ClosedPosition);
            if (distance < _hearingDistance)
                SoundManager.Inst?.PlaySfx(_riseSound, _volume * (1f - distance / _hearingDistance));
            return;
        }
        var main = Camera.main;
        if (_player != null || main == null || !main.TryGetComponent(out CinemachineBrain brain)) return;
        _player = player;
        _player.BeginDoorShot(this);
        StartCoroutine(ShowDoor(door, brain));
    }

    private IEnumerator ShowDoor(EventDoorOpen door, CinemachineBrain brain)
    {
        try
        {
            _original = door.GetComponent<SpriteRenderer>();
            var picture = new GameObject("Door presentation sprite");
            _picture = picture.AddComponent<SpriteRenderer>();
            picture.transform.SetParent(door.transform.parent, false);
            picture.transform.SetPositionAndRotation(door.ClosedPosition, door.transform.rotation);
            picture.transform.localScale = door.transform.localScale;
            _picture.sprite = _original.sprite;
            _picture.sharedMaterial = _original.sharedMaterial;
            _picture.sortingLayerID = _original.sortingLayerID;
            _picture.sortingOrder = _original.sortingOrder;
            _picture.color = _original.color;
            _picture.flipX = _original.flipX;
            _picture.flipY = _original.flipY;
            _previousForceOff = _original.forceRenderingOff;
            _original.forceRenderingOff = true;

            _canvas = new GameObject("Door cinematic UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            var scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            var bars = Instantiate(_barsPrefab, _canvas.transform, false);
            bars.SetVisibility(1f);
            _camera.enabled = true;
            float deadline = Time.unscaledTime + 5f;
            do
            {
                yield return null;
                if (!CanContinue(brain) || Time.unscaledTime > deadline) yield break;
            } while (brain.ActiveVirtualCamera != _camera || brain.IsBlending);

            SoundManager.Inst?.PlaySfx(_riseSound, _volume);
            float start = Time.unscaledTime;
            while (Time.unscaledTime - start < door.OpenDuration + _holdSeconds)
            {
                if (!CanContinue(brain)) yield break;
                float t = door.OpenDuration > 0f ? (Time.unscaledTime - start) / door.OpenDuration : 1f;
                _picture.transform.position = Vector3.Lerp(door.ClosedPosition, door.OpenPosition, t);
                yield return null;
            }
            _camera.enabled = false;
            deadline = Time.unscaledTime + 5f;
            do
            {
                yield return null;
                if (!CanContinue(brain) || Time.unscaledTime > deadline) yield break;
            } while (brain.ActiveVirtualCamera == _camera || brain.IsBlending);
        }
        finally { Release(); }
    }

    private bool CanContinue(CinemachineBrain brain) =>
        brain != null && brain.isActiveAndEnabled && _player != null && _player.CanWatchDoor;

    private void Release()
    {
        if (_camera != null) _camera.enabled = false;
        if (_original != null) _original.forceRenderingOff = _previousForceOff;
        if (_picture != null) Destroy(_picture.gameObject);
        if (_canvas != null) Destroy(_canvas);
        if (_player != null) _player.EndDoorShot(this);
        _original = null;
        _picture = null;
        _canvas = null;
        _player = null;
    }

    internal void StopShot()
    {
        StopAllCoroutines();
        Release();
    }

    private void OnDisable() => StopShot();
}
