using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public partial class PlayerNetworkStats
{
    // 서버 세션 소유. 플레이어 오브젝트가 끊겨도 잠근 필드 스택과 마지막 결정은 유지한다.
    private sealed class CharacterFieldState
    {
        public PlayerNetworkStats Owner;
        public FieldExchange Pending;
        public bool HasCommitted;
        public string HistoryId;
        public string LastToken;
        public int LastCount;
        // 같은 세션에 재접속해 예전 버리기 요청을 다시 보내도 새 필드 스택을 만들지 않는다.
        public readonly Dictionary<string, int> DropResults = new();
    }

    private sealed class FieldExchange
    {
        public string Token;
        public FieldItemObject Field;
        public string InstanceId;
        public int Quantity;
        public bool IsDrop => !string.IsNullOrEmpty(InstanceId);
    }

    private static readonly Dictionary<string, CharacterFieldState> FieldCharacters = new();
    private CharacterFieldState _fieldState;
    private CharacterSaveData _localFieldCharacter;
    private Coroutine _fieldRegistration;
    private Coroutine _fieldSaveRetry;
    private bool _fieldRegistered;
    public bool IsFieldRegistered => _fieldRegistered;
    private uint _dropSequence;
    private uint _serverDropSequence;
    private string _serverDropToken;
    private string _clientDropToken;
    private string _activeOffer;
    private string _finishedOffer;
    private int _finishedCount;

    private static readonly Dictionary<ulong, string> AdmittedCharacters = new();

    internal static void ClearFieldSession()
    {
        FieldCharacters.Clear();
        AdmittedCharacters.Clear();
    }

    internal static void ForgetFieldClient(ulong clientId) => AdmittedCharacters.Remove(clientId);

    internal static byte[] FieldConnectionData()
    {
        var character = GameStartSelection.Character;
        if (character == null) return Array.Empty<byte>();
        return System.Text.Encoding.ASCII.GetBytes(character.CharacterId + ":" + character.FieldHistoryId);
    }

    // 같은 세션의 확정 드롭이 다른 사람에게 넘어간 뒤에도 이전 백업의 재입장을 막는다.
    internal static void ApproveFieldCharacter(NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        response.CreatePlayerObject = false; // 플레이어는 기존 OnServerClientConnected에서 생성한다.
        var payload = request.Payload;
        if (payload == null || (payload.Length != 33 && payload.Length != 65))
        {
            response.Reason = "캐릭터 저장 정보를 확인할 수 없습니다.";
            return;
        }
        string identity = System.Text.Encoding.ASCII.GetString(payload);
        string id = identity.Substring(0, 32);
        string history = identity.Substring(33);
        if (!Guid.TryParseExact(id, "N", out _) || identity[32] != ':' ||
            (history.Length != 0 && !Guid.TryParseExact(history, "N", out _)))
        {
            response.Reason = "캐릭터 저장 ID가 올바르지 않습니다.";
            return;
        }
        if (AdmittedCharacters.ContainsValue(id))
        {
            response.Reason = "이미 같은 캐릭터가 접속 중입니다.";
            return;
        }
        if (!FieldCharacters.TryGetValue(id, out var state))
        {
            state = new CharacterFieldState();
            FieldCharacters.Add(id, state);
        }
        if ((state.HasCommitted || state.Pending != null) && state.HistoryId != history)
        {
            response.Reason = "이 캐릭터의 백업은 진행 중인 월드의 아이템 거래와 일치하지 않습니다.";
            return;
        }
        state.HistoryId = history;
        AdmittedCharacters[request.ClientNetworkId] = id;
        response.Approved = true;
    }

    private void InitializeFieldTransfers()
    {
        if (IsOwner && IsClient) _fieldRegistration = StartCoroutine(RegisterWhenReady());
    }

    private IEnumerator RegisterWhenReady()
    {
        // OnNetworkSpawn 때 PlayerObject 등록이 끝나지 않은 호스트도 있다.
        yield return null;
        _localFieldCharacter = GameStartSelection.Character;
        var retry = new WaitForSecondsRealtime(1f);
        bool reported = false;
        while (IsSpawned && !_fieldRegistered && _localFieldCharacter != null &&
               ReferenceEquals(_localFieldCharacter, GameStartSelection.Character))
        {
            if (NetworkManager.LocalClient?.PlayerObject == NetworkObject)
            {
                // 백업으로 입장한 첫 회에는 새 저장 세대와 현재 가방을 먼저 본파일에 확정한다.
                if (!_localFieldCharacter.LoadedPrimary && !string.IsNullOrEmpty(_localFieldCharacter.FieldHistoryId))
                {
                    if (!SaveManager.Inst.TrySaveCurrentCharacter(out string error))
                    {
                        if (!reported) Debug.LogWarning($"백업 복원 저장을 기다립니다. {error}", this);
                        reported = true;
                        yield return retry;
                        continue;
                    }
                    _localFieldCharacter.LoadedPrimary = true;
                }
                RegisterFieldCharacterServerRpc(_localFieldCharacter.CharacterId, _localFieldCharacter.FieldHistoryId ?? "");
            }
            yield return retry;
        }
    }

    private void ReleaseFieldTransfers()
    {
        if (_fieldRegistration != null) StopCoroutine(_fieldRegistration);
        if (_fieldSaveRetry != null) StopCoroutine(_fieldSaveRetry);
        if (_fieldState != null && _fieldState.Owner == this) _fieldState.Owner = null;
        if (_localFieldCharacter != null && ReferenceEquals(_localFieldCharacter, GameStartSelection.Character))
            ServiceManager.Inst?.InventoryService?.ReleaseDrop();
        _fieldState = null;
        _fieldRegistered = false;
        _activeOffer = _clientDropToken = null;
        _localFieldCharacter = null;
    }

    private bool IsConnectedFieldPlayer(ulong sender)
    {
        return IsServer && IsSpawned && NetworkObject.IsPlayerObject && sender == OwnerClientId &&
            NetworkManager.ConnectedClients.TryGetValue(sender, out var client) && client.PlayerObject == NetworkObject;
    }

    private bool IsLocalFieldPlayer => IsOwner && IsSpawned && _localFieldCharacter != null &&
        ReferenceEquals(_localFieldCharacter, GameStartSelection.Character);

    [ServerRpc]
    private void RegisterFieldCharacterServerRpc(string characterId, string history, ServerRpcParams rpc = default)
    {
        if (!IsConnectedFieldPlayer(rpc.Receive.SenderClientId) ||
            !AdmittedCharacters.TryGetValue(OwnerClientId, out var admitted) || characterId != admitted)
            return;
        var state = FieldCharacters[characterId];
        if (state.HistoryId != history) return;
        if ((_fieldState != null && _fieldState != state) || (state.Owner != null && state.Owner != this))
            return;
        bool newlyBound = _fieldState == null;
        _fieldState = state;
        state.Owner = this;
        RegisterFieldCharacterClientRpc(state.LastToken ?? "", OwnerRpcParams());
        if (newlyBound && state.Pending != null) SendFieldOffer(true);
    }

    [ClientRpc]
    private void RegisterFieldCharacterClientRpc(string lastToken, ClientRpcParams rpc = default)
    {
        if (!IsLocalFieldPlayer) return;
        _fieldRegistered = true;
        ForgetFieldReceipt(lastToken);
    }

    internal bool RequestFieldDrop(string instanceId, string itemId, int quantity)
    {
        if (!IsLocalFieldPlayer || !_fieldRegistered || _clientDropToken != null || _activeOffer != null)
            return false;
        _clientDropToken = Guid.NewGuid().ToString("N");
        BeginFieldDropServerRpc(++_dropSequence, _clientDropToken, instanceId, itemId, quantity);
        return true;
    }

    [ServerRpc]
    private void BeginFieldDropServerRpc(uint sequence, string token, string instanceId, string itemId, int quantity,
        ServerRpcParams rpc = default)
    {
        if (!IsConnectedFieldPlayer(rpc.Receive.SenderClientId) || _fieldState == null) return;
        if (!Guid.TryParseExact(token, "N", out _)) return;
        if (_fieldState.DropResults.TryGetValue(token, out int decided))
        {
            FinishFieldExchangeClientRpc(token, decided, OwnerRpcParams());
            return;
        }
        if (_fieldState.Pending?.Token == token)
        {
            SendFieldOffer(_serverDropToken != token);
            return;
        }
        if (sequence <= _serverDropSequence)
        {
            FinishFieldExchangeClientRpc(token, 0, OwnerRpcParams());
            return;
        }
        _serverDropSequence = sequence;
        _serverDropToken = token;
        if (_fieldState.Pending != null || !IsAlive ||
            string.IsNullOrWhiteSpace(instanceId) || string.IsNullOrEmpty(itemId) ||
            !DataManager.Inst.ReadData<ItemData>().TryGetValue(itemId, out var data) ||
            quantity < 1 || quantity > data.MaxStack || token == _fieldState.LastToken)
        {
            _fieldState.DropResults[token] = 0;
            FinishFieldExchangeClientRpc(token, 0, OwnerRpcParams());
            return;
        }
        var field = ObjectManager.Inst.SpawnItem(itemId, quantity, transform.position);
        if (field == null)
        {
            _fieldState.DropResults[token] = 0;
            FinishFieldExchangeClientRpc(token, 0, OwnerRpcParams());
            return;
        }
        StartFieldExchange(field, token, instanceId);
    }

    internal void TryReservePickup(FieldItemObject field)
    {
        if (!IsConnectedFieldPlayer(OwnerClientId) || !IsAlive || _fieldState == null || _fieldState.Pending != null)
            return;
        StartFieldExchange(field, Guid.NewGuid().ToString("N"), "");
    }

    private void StartFieldExchange(FieldItemObject field, string token, string instanceId)
    {
        if (!field.ReserveForTransfer()) return;
        _fieldState.Pending = new FieldExchange
        {
            Token = token, Field = field, InstanceId = instanceId, Quantity = field.Count
        };
        SendFieldOffer(false);
    }

    private void SendFieldOffer(bool recovering)
    {
        var pending = _fieldState.Pending;
        OfferFieldExchangeClientRpc(pending.Token, pending.InstanceId, pending.Field.ItemDataId,
            pending.Quantity, recovering, OwnerRpcParams());
    }

    [ClientRpc]
    private void OfferFieldExchangeClientRpc(string token, string instanceId, string itemId, int quantity,
        bool recovering, ClientRpcParams rpc = default)
    {
        if (!IsLocalFieldPlayer || _activeOffer == token) return;
        if (_finishedOffer == token)
        {
            DecideFieldExchangeServerRpc(token, _finishedCount);
            return;
        }
        _activeOffer = token;
        _fieldSaveRetry = StartCoroutine(SaveFieldOffer(token, instanceId, itemId, quantity, recovering));
    }

    private IEnumerator SaveFieldOffer(string token, string instanceId, string itemId, int quantity, bool recovering)
    {
        // 콜백 내부에서 호스트 RPC가 즉시 재진입해도 코루틴 핸들이 먼저 등록된다.
        yield return null;
        var retry = new WaitForSecondsRealtime(2f);
        bool warned = false;
        while (IsLocalFieldPlayer && _activeOffer == token)
        {
            var receipt = _localFieldCharacter.FieldReceipts?.Find(entry => entry.Token == token);
            int accepted = receipt?.Count ?? 0;
            if (receipt != null && (accepted <= 0 || accepted > quantity ||
                (!string.IsNullOrEmpty(instanceId) && accepted != quantity)))
            {
                Debug.LogError("저장된 수량과 서버 예약이 달라 거래를 보류합니다.", this);
                yield break;
            }
            if (receipt == null)
            {
                if (recovering)
                {
                    if (!_localFieldCharacter.LoadedPrimary)
                    {
                        Debug.LogWarning("복원된 캐릭터에서 거래 결과를 확인할 수 없어 필드 아이템을 보류합니다.", this);
                        yield break;
                    }
                }
                else if (!string.IsNullOrEmpty(instanceId) && token != _clientDropToken)
                {
                    accepted = 0;
                }
                else if (!ServiceManager.Inst.InventoryService.TrySaveFieldExchange(token, instanceId, itemId,
                    quantity, out accepted, out string error))
                {
                    if (!warned) Debug.LogWarning($"아이템 거래 저장을 재시도합니다. {error}", this);
                    warned = true;
                    yield return retry;
                    continue;
                }
            }
            DecideFieldExchangeServerRpc(token, accepted);
            yield break;
        }
    }

    [ServerRpc]
    private void DecideFieldExchangeServerRpc(string token, int accepted, ServerRpcParams rpc = default)
    {
        if (!IsConnectedFieldPlayer(rpc.Receive.SenderClientId) || _fieldState == null) return;
        var pending = _fieldState.Pending;
        if (pending == null || pending.Token != token)
        {
            if (_fieldState.LastToken == token)
                FinishFieldExchangeClientRpc(token, _fieldState.LastCount, OwnerRpcParams());
            return;
        }
        if (accepted < 0 || accepted > pending.Quantity ||
            (pending.IsDrop && accepted != 0 && accepted != pending.Quantity)) return;

        _fieldState.LastToken = token;
        _fieldState.LastCount = accepted;
        if (accepted > 0) _fieldState.HasCommitted = true;
        _fieldState.Pending = null;
        if (pending.IsDrop)
        {
            _fieldState.DropResults[token] = accepted;
            pending.Field.FinishDrop(accepted > 0, this);
        }
        else pending.Field.FinishPickup(accepted);
        FinishFieldExchangeClientRpc(token, accepted, OwnerRpcParams());
    }

    [ClientRpc]
    private void FinishFieldExchangeClientRpc(string token, int count, ClientRpcParams rpc = default)
    {
        if (!IsLocalFieldPlayer) return;
        if (_activeOffer == token)
        {
            _activeOffer = null;
            _finishedOffer = token;
            _finishedCount = count;
        }
        if (_clientDropToken == token)
        {
            _clientDropToken = null;
            ServiceManager.Inst.InventoryService.ReleaseDrop();
        }
        ForgetFieldReceipt(token);
    }

    private void ForgetFieldReceipt(string token)
    {
        var receipts = _localFieldCharacter.FieldReceipts;
        if (receipts == null || string.IsNullOrEmpty(token)) return;
        for (int i = receipts.Count - 1; i >= 0; --i)
            if (receipts[i].Token == token) receipts.RemoveAt(i);
    }
}
