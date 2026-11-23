using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ScenarioNpcBinding : MonoBehaviour
{
    [Header("공유 사건 상태")]
    [SerializeField] private string _completedScenarioId;
    [Header("상태별 NPC 표시")]
    [SerializeField] private GameObject _beforeCompletion;
    [SerializeField] private GameObject _afterCompletion;
    [Header("이주 연출")]
    [SerializeField] private ScreenTransitionUI _fadePrefab;

    private ScenarioManager _manager;
    private bool _observedBefore;
    private CancellationTokenSource _fadeLifetime;
    private GameObject _canvas;
    private GameObject _picture;

    private void OnEnable()
    {
        if (string.IsNullOrWhiteSpace(_completedScenarioId) || _beforeCompletion == null ||
            _afterCompletion == null || _beforeCompletion == _afterCompletion ||
            transform.IsChildOf(_beforeCompletion.transform) || transform.IsChildOf(_afterCompletion.transform))
        {
            Debug.LogError("사건 ID와 서로 다른 두 NPC 객체를 연결하고, Binding은 항상 켜진 별도 객체에 배치해 주세요.", this);
            enabled = false;
            return;
        }
        ScenarioManager.AvailabilityChanged += BindManager;
        BindManager();
    }

    private void BindManager()
    {
        if (_manager != null)
            _manager.ProgressChanged -= Refresh;
        if (!ReferenceEquals(_manager, ScenarioManager.Inst))
        {
            _observedBefore = false;
            StopPresentation();
        }
        _manager = ScenarioManager.Inst;
        if (_manager != null)
            _manager.ProgressChanged += Refresh;
        Refresh();
    }

    private void Refresh()
    {
        bool ready = _manager != null && _manager.IsReady;
        if (ready && (!_manager.TryGetScenario(_completedScenarioId, out ScenarioData data) || !data.CompleteWorldOnFinish))
        {
            Debug.LogError($"공유 완료 사건 ID를 확인해 주세요: {_completedScenarioId}", this);
            ready = false;
        }
        bool completed = ready && _manager.IsCompleted(_completedScenarioId);
        bool showDeparture = completed && _observedBefore;
        _observedBefore = ready && !completed;
        if (!ready) StopPresentation();
        if (showDeparture) TryShowDeparture();
        // 상태 수신 전에는 잘못된 지하 NPC가 잠깐 나타나거나 상호작용하지 않게 합니다.
        _beforeCompletion.SetActive(ready && !completed);
        _afterCompletion.SetActive(ready && completed);
    }

    private void TryShowDeparture()
    {
        Camera camera = Camera.main;
        var local = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        if (_fadePrefab == null || camera == null || local == null ||
            !local.TryGetComponent(out PlayerControllerView player) || !CanPresent(player) ||
            !_beforeCompletion.TryGetComponent(out SpriteRenderer source) || !source.enabled || !source.gameObject.activeInHierarchy ||
            source.sprite == null || source.forceRenderingOff ||
            (camera.cullingMask & (1 << source.gameObject.layer)) == 0 ||
            !GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera), source.bounds)) return;

        // 실제 NPC는 즉시 교체하고 현재 보이는 한 프레임만 암전까지 남긴다.
        _picture = new GameObject("Departing NPC sprite");
        _picture.layer = source.gameObject.layer;
        _picture.transform.SetParent(source.transform.parent, false);
        _picture.transform.SetLocalPositionAndRotation(source.transform.localPosition, source.transform.localRotation);
        _picture.transform.localScale = source.transform.localScale;
        var picture = _picture.AddComponent<SpriteRenderer>();
        picture.sprite = source.sprite;
        picture.color = source.color;
        picture.flipX = source.flipX;
        picture.flipY = source.flipY;
        picture.sharedMaterial = source.sharedMaterial;
        picture.sortingLayerID = source.sortingLayerID;
        picture.sortingOrder = source.sortingOrder;

        _canvas = new GameObject("NPC relocation fade", typeof(RectTransform), typeof(Canvas));
        var canvas = _canvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;
        var cover = Instantiate(_fadePrefab, canvas.transform, false);
        _fadeLifetime = new CancellationTokenSource();
        StartCoroutine(WatchPlayer(player));
        FadeDeparture(cover, _fadeLifetime).Forget();
    }

    private static bool CanPresent(PlayerControllerView player)
    {
        var ui = UIManager.Inst;
        return player != null && player.CanWatchDoor && !player.IsWatchingDoor && ui != null &&
            !ui.IsLoading(UIType.ScreenTransitionUI) && !ui.IsOpen(UIType.ScreenTransitionUI) &&
            !ui.IsLoading(UIType.CinematicEffectUI) && !ui.IsOpen(UIType.CinematicEffectUI);
    }

    private IEnumerator WatchPlayer(PlayerControllerView player)
    {
        while (_fadeLifetime != null && CanPresent(player)) yield return null;
        if (_fadeLifetime != null) StopPresentation();
    }

    private async UniTask FadeDeparture(ScreenTransitionUI cover, CancellationTokenSource lifetime)
    {
        var token = lifetime.Token;
        try
        {
            await cover.FadeAsync(true, token);
            _picture.SetActive(false);
            await UniTask.NextFrame(cancellationToken: token);
            await cover.FadeAsync(false, token);
        }
        catch (System.OperationCanceledException) when (token.IsCancellationRequested) { }
        finally
        {
            if (_fadeLifetime == lifetime) StopPresentation();
        }
    }

    private void StopPresentation()
    {
        var lifetime = _fadeLifetime;
        _fadeLifetime = null;
        if (lifetime != null)
        {
            lifetime.Cancel();
            lifetime.Dispose();
        }
        if (_picture != null) { _picture.SetActive(false); Destroy(_picture); }
        if (_canvas != null) { _canvas.SetActive(false); Destroy(_canvas); }
        _picture = null;
        _canvas = null;
    }

    private void OnDisable()
    {
        ScenarioManager.AvailabilityChanged -= BindManager;
        if (_manager != null)
            _manager.ProgressChanged -= Refresh;
        _manager = null;
        _observedBefore = false;
        StopAllCoroutines();
        StopPresentation();
    }
}
