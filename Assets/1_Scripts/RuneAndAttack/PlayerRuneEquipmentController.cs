using System;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerRuneEquipmentController : NetworkBehaviour
{
    public const int SlotCount = 3;

    [Header("서버 인벤토리 연결 전 테스트 전용")]
    [SerializeField]
    private bool _allowTestEquipment;

    private NetworkList<FixedString128Bytes> _equippedRuneIds;

    public event Action EquipmentChanged;
    public event Action<string> RequestMessageReceived;
    public event Action Despawned;

    private void Awake()
    {
        _equippedRuneIds = new NetworkList<FixedString128Bytes>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // 현재 정책: 플레이어가 새로 스폰되면 빈 슬롯으로 시작.
            _equippedRuneIds.Clear();

            for (int i = 0; i < SlotCount; i++)
            {
                _equippedRuneIds.Add(default);
            }
        }

        _equippedRuneIds.OnListChanged += OnEquipmentListChanged;

        EquipmentChanged?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        _equippedRuneIds.OnListChanged -= OnEquipmentListChanged;
        Despawned?.Invoke();
    }

    private void OnEquipmentListChanged(
        NetworkListEvent<FixedString128Bytes> changeEvent)
    {
        EquipmentChanged?.Invoke();
    }

    public string GetEquippedRuneId(int slotIndex)
    {
        if (!IsSpawned ||
            slotIndex < 0 ||
            slotIndex >= _equippedRuneIds.Count)
        {
            return "";
        }

        return _equippedRuneIds[slotIndex].ToString();
    }

    // 소유 클라이언트가 호출하는 입구.
    // 반환값은 요청을 보냈는지이며, 서버의 장착 승인 여부가 아님.
    public bool RequestSetRune(int slotIndex, string runeId)
    {
        if (!IsSpawned || !IsOwner)
            return false;

        if (slotIndex < 0 || slotIndex >= SlotCount)
            return false;

        RequestSetRuneServerRpc(slotIndex, runeId ?? "");
        return true;
    }

    [ServerRpc]
    private void RequestSetRuneServerRpc(
        int slotIndex,
        string runeId,
        ServerRpcParams rpcParams = default)
    {
        // 이 플레이어의 소유자만 장착 변경 가능.
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        if (!IsSpawned ||
            slotIndex < 0 ||
            slotIndex >= SlotCount ||
            _equippedRuneIds.Count != SlotCount)
        {
            return;
        }

        if (DataManager.Inst == null)
        {
            SendMessageToOwner("DataManager가 준비되지 않았습니다.");
            return;
        }

        runeId ??= "";

        FixedString128Bytes networkId = default;

        if (Encoding.UTF8.GetByteCount(runeId) > networkId.Capacity)
        {
            SendMessageToOwner("룬 ID가 너무 깁니다.");
            return;
        }

        RuneData requestedRune = null;

        // 빈 문자열은 해제 요청.
        if (string.IsNullOrEmpty(runeId) == false)
        {
            var runeTable = DataManager.Inst.ReadData<RuneData>();
            var itemTable = DataManager.Inst.ReadData<ItemData>();

            if (!runeTable.TryGetValue(runeId, out requestedRune) ||
                requestedRune == null ||
                !itemTable.TryGetValue(runeId, out var itemData) ||
                itemData == null)
            {
                SendMessageToOwner("아이템 또는 룬 데이터가 없습니다.");
                return;
            }

            if (CanEquipOwnedRuneOnServer(runeId) == false)
            {
                SendMessageToOwner(
                    "서버 인벤토리 검증이 연결되지 않았습니다. " +
                    "테스트 중이라면 테스트 장착 옵션을 켜세요.");
                return;
            }
        }

        // 변경하려는 결과를 먼저 계산하고 검증.
        if (!TryReadEquippedRunes(out RuneData[] slots))
        {
            SendMessageToOwner("현재 장착 데이터를 읽지 못했습니다.");
            return;
        }

        slots[slotIndex] = requestedRune;

        if (!AttackProfileBuilder.TryBuild(
                slots, out _, out string error))
        {
            SendMessageToOwner(error);
            return;
        }

        // 검증 성공 후에만 실제 상태 변경.
        _equippedRuneIds[slotIndex] =
            new FixedString128Bytes(runeId);

        SendMessageToOwner(
            string.IsNullOrEmpty(runeId)
                ? "룬을 해제했습니다."
                : "룬을 장착했습니다.");
    }

    private bool CanEquipOwnedRuneOnServer(string runeId)
    {
        // 현재 InventoryService는 로컬 목록이므로
        // 서버에서 다른 플레이어의 소지품 검증에 사용할 수 없음.
        //
        // 서버 인벤토리 구현 후:
        // OwnerClientId에 해당하는 인벤토리에서 runeId 소유 확인.
        //
        // 현재는 명시적으로 활성화한 테스트에서만 장착 허용.
        return IsServer && _allowTestEquipment;
    }

    private bool TryReadEquippedRunes(out RuneData[] slots)
    {
        slots = null;

        if (DataManager.Inst == null ||
            _equippedRuneIds.Count != SlotCount)
        {
            return false;
        }

        var result = new RuneData[SlotCount];
        var runeTable = DataManager.Inst.ReadData<RuneData>();

        for (int i = 0; i < SlotCount; i++)
        {
            string runeId = _equippedRuneIds[i].ToString();

            if (string.IsNullOrEmpty(runeId))
                continue;

            if (!runeTable.TryGetValue(runeId, out RuneData rune) || rune == null)
            {
                return false;
            }

            result[i] = rune;
        }

        slots = result;
        return true;
    }

    // 공격하는 플레이어 자신의 컴포넌트에서 호출.
    public bool TryBuildAttackProfile(
        out AttackProfile profile,
        out string error)
    {
        profile = null;
        error = null;

        if (!IsServer || !IsSpawned)
        {
            error = "서버에서 스폰된 플레이어만 공격을 계산합니다.";
            return false;
        }

        if (!TryReadEquippedRunes(out RuneData[] slots))
        {
            error = "장착 룬 데이터를 읽지 못했습니다.";
            return false;
        }

        return AttackProfileBuilder.TryBuild(slots, out profile, out error);
    }

    private void SendMessageToOwner(string message)
    {
        var rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { OwnerClientId }
            }
        };

        ReceiveMessageClientRpc(message, rpcParams);
    }

    [ClientRpc]
    private void ReceiveMessageClientRpc(string message, ClientRpcParams rpcParams = default)
    {
        RequestMessageReceived?.Invoke(message);
    }
}
