using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class BossHealthHUDZone : MonoBehaviour
{
    [SerializeField] private Boss _boss;

    private BoxCollider2D _area;
    private BossNetwork _bossNetwork;
    private MonsterVM _viewModel;
    private readonly HashSet<Collider2D> _localContacts = new();
    private BossHealthUI _hud;
    private UIManager _uiManager;
    private bool _isOpening;
    private bool _openFailed;

    private void Reset()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
        _boss = GetComponentInParent<Boss>();
        gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
    }

    private void Awake()
    {
        _area = GetComponent<BoxCollider2D>();
        if (_boss == null)
            _boss = GetComponentInParent<Boss>();

        if (_boss == null || !_boss.TryGetComponent(out _bossNetwork) || !_area.isTrigger)
        {
            Debug.LogError("보스 HUD 영역의 Boss 연결과 BoxCollider2D의 Is Trigger를 확인하세요.", this);
            enabled = false;
            return;
        }

        _viewModel = _boss.ViewModel;
    }

    private void OnEnable()
    {
        if (_bossNetwork != null)
            _bossNetwork.Despawned += OnBossDespawned;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        RegisterContact(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        RegisterContact(other);
    }

    private void RegisterContact(Collider2D other)
    {
        if (isActiveAndEnabled && IsLocalPlayerCollider(other) && _area.IsTouching(other))
            _localContacts.Add(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other != null && _area != null && _area.IsTouching(other))
            return;

        _localContacts.Remove(other);
    }

    private static bool IsLocalPlayerCollider(Collider2D other)
    {
        if (other == null || !other.isActiveAndEnabled || other.isTrigger)
            return false;

        PlayerNetworkStats player = other.GetComponentInParent<PlayerNetworkStats>();
        return player != null && player.IsSpawned && player.IsOwner;
    }

    private bool CanDisplay()
    {
        if (!isActiveAndEnabled || _area == null || !_area.isActiveAndEnabled
            || _boss == null || _bossNetwork == null || !_bossNetwork.IsClient
            || !_bossNetwork.IsSpawned || _bossNetwork.MaxHp <= 0f)
            return false;

        _localContacts.RemoveWhere(contact =>
            !IsLocalPlayerCollider(contact) || !_area.IsTouching(contact));
        return _localContacts.Count > 0;
    }

    private void LateUpdate()
    {
        if (!CanDisplay())
        {
            HideHUD();
            _openFailed = false;
            return;
        }

        if (_isOpening || _openFailed || (_hud != null && _hud.gameObject.activeSelf))
            return;

        if (UIManager.Inst != null)
            ShowHUDAsync(UIManager.Inst).Forget();
    }

    private async UniTask ShowHUDAsync(UIManager manager)
    {
        _isOpening = true;
        MonsterVM viewModel = _viewModel;
        try
        {
            UIBase loadedUI = await manager.OpenMainRootAsync(UIType.BossHealthUI);
            if (loadedUI == null)
            {
                _openFailed = true;
                return;
            }

            if (loadedUI is not BossHealthUI hud)
            {
                Debug.LogError("UI/BossHealthUI 프리팹 루트에 BossHealthUI를 연결하세요.", loadedUI);
                if (manager != null)
                    manager.Close(UIType.BossHealthUI);
                _openFailed = true;
                return;
            }

            if (this == null || !CanDisplay())
            {
                CloseHUD(manager, hud, viewModel);
                return;
            }

            if (!hud.Bind(viewModel))
            {
                CloseHUD(manager, hud, viewModel);
                _openFailed = true;
                return;
            }

            _hud = hud;
            _uiManager = manager;
        }
        catch (System.Exception exception)
        {
            _openFailed = true;
            Debug.LogException(exception);
        }
        finally
        {
            _isOpening = false;
        }
    }

    private static void CloseHUD(UIManager manager, BossHealthUI hud, MonsterVM viewModel)
    {
        if (hud == null || (hud.ViewModel != null && hud.ViewModel != viewModel))
            return;

        if (manager != null && hud.gameObject.activeSelf)
            manager.Close(UIType.BossHealthUI);
        else
            hud.gameObject.SetActive(false);

        hud.Unbind();
    }

    private void HideHUD()
    {
        if (_hud != null)
            CloseHUD(_uiManager, _hud, _viewModel);

        _hud = null;
        _uiManager = null;
    }

    private void OnBossDespawned()
    {
        _localContacts.Clear();
        HideHUD();
    }

    private void OnDisable()
    {
        if (_bossNetwork != null)
            _bossNetwork.Despawned -= OnBossDespawned;

        _localContacts.Clear();
        HideHUD();
        _openFailed = false;
    }
}
