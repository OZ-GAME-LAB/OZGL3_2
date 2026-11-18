using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public sealed class NetworkSessionSaveService : IDisposable
{
    private const float AutoSaveInterval = 60f;
    private const string CharacterSaveRequestMessage = "SessionSave.RequestCharacter";

    private readonly NetworkManager _network;
    private readonly UnityEngine.Object _logContext;
    private CustomMessagingManager _messages;
    private bool _initialized;
    private bool _autoSaveRunning;
    private float _autoSaveRemaining = AutoSaveInterval;
    private ulong _nextRequestId;
    private ulong _lastCharacterRequestId;

    private bool IsNetworkReady => _network != null && _network.IsListening &&
        !_network.ShutdownInProgress && _messages != null &&
        ReferenceEquals(_messages, _network.CustomMessagingManager);

    public NetworkSessionSaveService(NetworkManager network, UnityEngine.Object logContext)
    {
        _network = network;
        _logContext = logContext;
    }

    public void Initialize()
    {
        if (_initialized || _network == null)
            return;

        _initialized = true;
        _network.OnServerStarted += OnSessionStarted;
        _network.OnClientStarted += OnSessionStarted;
        _network.OnPreShutdown += EndSession;
        _network.OnServerStopped += OnSessionStopped;
        _network.OnClientStopped += OnSessionStopped;

        if (_network.IsListening)
            OnSessionStarted();
    }

    public void Dispose()
    {
        if (_initialized && _network != null)
        {
            _network.OnServerStarted -= OnSessionStarted;
            _network.OnClientStarted -= OnSessionStarted;
            _network.OnPreShutdown -= EndSession;
            _network.OnServerStopped -= OnSessionStopped;
            _network.OnClientStopped -= OnSessionStopped;
        }

        _initialized = false;
        EndSession();
    }

    public void StartAutoSave()
    {
        if (!IsNetworkReady || !_network.IsHost)
            return;

        _autoSaveRemaining = AutoSaveInterval;
        _autoSaveRunning = true;
    }

    public void StopAutoSave()
    {
        _autoSaveRunning = false;
        _autoSaveRemaining = AutoSaveInterval;
    }

    public void Tick(float deltaTime)
    {
        if (!_autoSaveRunning || !IsNetworkReady || !_network.IsHost || deltaTime <= 0f)
            return;

        _autoSaveRemaining -= deltaTime;
        if (_autoSaveRemaining > 0f)
            return;

        _autoSaveRemaining = AutoSaveInterval;
        // TODO(저장 연결) : 저장 함수 구현 후 아래 로그를 RequestSessionSave() 호출로 교체한다.
        Debug.Log("60초 저장", _logContext);
    }

    public bool RequestSessionSave()
    {
        if (!IsNetworkReady || !_network.IsHost || !_network.IsConnectedClient)
        {
            Debug.LogWarning("전체 저장 요청은 접속 중인 호스트에서만 호출할 수 있습니다.", _logContext);
            return false;
        }

        ulong requestId = ++_nextRequestId;
        using var writer = new FastBufferWriter(sizeof(ulong), Allocator.Temp);
        writer.WriteValueSafe(requestId);

        foreach (ulong clientId in _network.ConnectedClientsIds)
        {
            if (clientId == _network.LocalClientId)
                continue;

            _messages.SendNamedMessage(CharacterSaveRequestMessage, clientId, writer);
        }

        RequestLocalCharacterSave(requestId);
        RequestWorldSave(requestId);
        return true;
    }

    private void OnSessionStarted()
    {
        if (_network == null || ReferenceEquals(_messages, _network.CustomMessagingManager))
            return;

        EndSession();
        _messages = _network.CustomMessagingManager;
        if (_messages == null)
            return;

        _messages.RegisterNamedMessageHandler(CharacterSaveRequestMessage, OnCharacterSaveRequested);
        if (_network.IsHost)
            StartAutoSave();
    }

    private void OnSessionStopped(bool wasHost)
    {
        EndSession();
    }

    private void EndSession()
    {
        StopAutoSave();
        _messages?.UnregisterNamedMessageHandler(CharacterSaveRequestMessage);
        _messages = null;
        _nextRequestId = 0;
        _lastCharacterRequestId = 0;
    }

    private void OnCharacterSaveRequested(ulong senderClientId, FastBufferReader reader)
    {
        if (!IsNetworkReady || !_network.IsConnectedClient ||
            senderClientId != NetworkManager.ServerClientId || !reader.TryBeginRead(sizeof(ulong)))
            return;

        reader.ReadValueSafe(out ulong requestId);
        RequestLocalCharacterSave(requestId);
    }

    private void RequestLocalCharacterSave(ulong requestId)
    {
        if (requestId <= _lastCharacterRequestId)
            return;

        _lastCharacterRequestId = requestId;
        // TODO(저장 연결) : 저장 함수 구현 후 현재 로컬 캐릭터 데이터를 수집하여 저장하고, 성공·실패 결과를 호스트에 전달한다.
        Debug.Log($"[캐릭터 저장 요청] 요청: {requestId}, 클라이언트: {_network.LocalClientId}, 실제 저장 연결 대기", _logContext);
    }

    private void RequestWorldSave(ulong requestId)
    {
        // TODO : 시나리오 상황 저장 구현 후 월드 진행도를 수집하는 처리를 연결한다. 기존 진행도는 유지한다.
        // TODO(저장 연결) : 저장 함수 구현 후 호스트의 월드 데이터를 저장하고 결과를 확인한다.
        Debug.Log($"[월드 저장 요청] 요청: {requestId}, 실제 저장 연결 대기", _logContext);
    }
}
