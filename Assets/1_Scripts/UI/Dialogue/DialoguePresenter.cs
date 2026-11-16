using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 대화 규칙은 서비스에 두고 비동기 UI 생성/해제만 연결합니다.
public sealed class DialoguePresenter : IDisposable
{
    private readonly ScenarioService _service;
    private CancellationTokenSource _openCancellation;
    private DialogueUI _ui;
    private UIManager _uiManager;
    private ulong _openingRequestId;
    private bool _disposed;
    private string _spriteAddress = string.Empty;
    private ulong _spriteVersion;
    private readonly Dictionary<string, Task<Sprite>> _spriteLoads = new Dictionary<string, Task<Sprite>>();

    public DialoguePresenter(ScenarioService service)
    {
        _service = service;
        _service.StateChanged += OnStateChanged;
        _service.Ended += OnEnded;
        OnStateChanged();
    }

    private void OnStateChanged()
    {
        if (_disposed || _service.State != ScenarioLocalState.OpeningDialogue ||
            _openingRequestId == _service.ActiveRequestId)
            return;

        _openingRequestId = _service.ActiveRequestId;
        _openCancellation = new CancellationTokenSource();
        OpenAsync(_openingRequestId, _openCancellation.Token).Forget();
    }

    private async UniTask OpenAsync(ulong requestId, CancellationToken token)
    {
        UIManager manager = UIManager.Inst;
        try
        {
            if (manager == null)
            {
                _service.NotifyDialogueFailed(requestId);
                return;
            }

            DialogueUI ui = await manager.OpenDialogueUIAsync(token);
            // 이 Presenter는 생성 시의 서비스에만 묶입니다. 새 서비스/사건을 건드리지 않습니다.
            if (_disposed || token.IsCancellationRequested || _service.ActiveRequestId != requestId)
                return;

            if (ui == null)
            {
                if (manager.IsOpen(UIType.DialogueUI))
                    manager.CloseDialogueUI();
                _service.NotifyDialogueFailed(requestId);
                return;
            }

            _uiManager = manager;
            _ui = ui;
            _ui.NextRequested += _service.AdvanceDialogue;
            _ui.Hidden += _service.Cancel;
            if (!_ui.Bind(_service.Dialogue))
            {
                _service.NotifyDialogueFailed(requestId);
                return;
            }
            _service.Dialogue.PropertyChanged += OnDialogueChanged;
            RefreshNpcSprite();
            _service.NotifyDialogueOpened(requestId);
        }
        catch (OperationCanceledException)
        {
            // 서비스 종료 시 UIManager가 늦은 생성 결과를 캐시에만 남기고 열지 않습니다.
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            _service.NotifyDialogueFailed(requestId);
        }
    }

    private void OnEnded(ScenarioSource source, ScenarioEndReason reason)
    {
        ReleaseUI();
    }

    private void OnDialogueChanged(object sender, PropertyChangedEventArgs args)
    {
        RefreshNpcSprite();
    }

    private void RefreshNpcSprite()
    {
        string address = _service.Dialogue.NpcSpriteAddress.Trim();
        if (_ui == null || address == _spriteAddress)
            return;
        _spriteAddress = address;
        ulong version = ++_spriteVersion;
        _ui.SetNpcSprite(null);
        if (address.Length > 0)
            LoadNpcSpriteAsync(address, version, _ui).Forget();
    }

    private async UniTask LoadNpcSpriteAsync(string address, ulong version, DialogueUI target)
    {
        try
        {
            ResourceManager manager = ResourceManager.Inst;
            if (manager == null)
                return;
            // 같은 주소의 진행 중 로딩은 공유하여 A→B→A 전환에서도 중복 요청을 막습니다.
            if (!_spriteLoads.TryGetValue(address, out Task<Sprite> loading))
            {
                loading = manager.LoadSprite(address).AsTask();
                _spriteLoads.Add(address, loading);
            }
            Sprite sprite = await loading;
            if (!_disposed && version == _spriteVersion && target != null && target == _ui)
                target.SetNpcSprite(sprite);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            if (_spriteLoads.TryGetValue(address, out Task<Sprite> loading) && loading.IsCompleted)
                _spriteLoads.Remove(address);
        }
    }

    private void ReleaseUI()
    {
        _service.Dialogue.PropertyChanged -= OnDialogueChanged;
        ++_spriteVersion;
        _spriteAddress = string.Empty;
        if (_openCancellation != null)
        {
            _openCancellation.Cancel();
            _openCancellation.Dispose();
            _openCancellation = null;
        }
        _openingRequestId = 0;
        if (_ui != null)
        {
            _ui.NextRequested -= _service.AdvanceDialogue;
            _ui.Hidden -= _service.Cancel;
            _ui.Unbind();
            if (_uiManager != null && _uiManager.IsOpen(UIType.DialogueUI))
                _uiManager.CloseDialogueUI();
        }
        _ui = null;
        _uiManager = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _service.StateChanged -= OnStateChanged;
        _service.Ended -= OnEnded;
        ReleaseUI();
    }
}

