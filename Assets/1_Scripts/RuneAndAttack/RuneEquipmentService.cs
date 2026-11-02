using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class RuneEquipmentService : NetworkBehaviour
{
    public const int SlotCount = 3;

    [Header("서버 인벤토리 연결 전 테스트 전용")]
    [SerializeField]
    private bool _allowTestEquipment;

    private NetworkList<FixedString128Bytes> _equippedRuneIds;

    public RuneInventoryViewModel ViewModel { get; private set; }

    private void Awake()
    {
        _equippedRuneIds = new NetworkList<FixedString128Bytes>();
        ViewModel = new RuneInventoryViewModel(SlotCount);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            _equippedRuneIds.Clear();

            for (int i = 0; i < SlotCount; i++)
            {
                _equippedRuneIds.Add(default);
            }
        }

        _equippedRuneIds.OnListChanged += OnEquipmentChanged;
        RefreshEquipmentViewModel();
    }

    public override void OnNetworkDespawn()
    {
        _equippedRuneIds.OnListChanged -= OnEquipmentChanged;

        for (int i = 0; i < SlotCount; i++)
        {
            ViewModel.EquipmentSlots[i].SetRuneId("");
        }

        ViewModel.SelectRune("");
        ViewModel.SetMessage("플레이어 연결이 종료되었습니다.");
    }

    private void OnEquipmentChanged(NetworkListEvent<FixedString128Bytes> changeEvent)
    {
        RefreshEquipmentViewModel();
    }

    private void RefreshEquipmentViewModel()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            string id = i < _equippedRuneIds.Count ? _equippedRuneIds[i].ToString() : "";

            ViewModel.EquipmentSlots[i].SetRuneId(id);
        }
    }

    // UI를 열 때 로컬 인벤토리 목록을 받아 화면용 목록을 구성.
    // 이 목록 자체를 서버의 소유 증명으로 사용하지는 않음.
    public void RefreshInventory(InventoryService inventory)
    {
        var runeIds = new List<string>();

        if (inventory != null && DataManager.Inst != null)
        {
            Dictionary<string, RuneData> runeTable = DataManager.Inst.ReadData<RuneData>();

            foreach (var pair in inventory.InventoryList)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                // 같은 ID의 RuneData가 있는 아이템만 표시.
                if (runeTable.ContainsKey(pair.Key))
                {
                    runeIds.Add(pair.Key);
                }
            }
        }

        runeIds.Sort(System.StringComparer.Ordinal);
        ViewModel.SetInventoryRunes(runeIds);
        ViewModel.SetMessage("룬을 선택한 뒤 장착 슬롯을 누르세요.");
    }

    public void SelectRune(string runeId)
    {
        foreach (RuneSlotViewModel slot in ViewModel.InventoryRunes)
        {
            if (slot.RuneId != runeId)
            {
                continue;
            }

            ViewModel.SelectRune(runeId);
            ViewModel.SetMessage("장착할 슬롯을 누르세요.");
            return;
        }
    }

    public void ClearSelection()
    {
        ViewModel.SelectRune("");
        ViewModel.SetMessage("장착 슬롯을 누르면 해당 룬을 해제합니다.");
    }

    public void RequestSlotClick(int slotIndex)
    {
        if (IsSpawned == false || !IsOwner)
        {
            ViewModel.SetMessage("자신의 플레이어가 준비된 뒤 사용하세요.");
            return;
        }

        if (slotIndex < 0 || slotIndex >= SlotCount)
        {
            return;
        }

        string runeId = ViewModel.SelectedRuneId;

        // 빈 ID는 해제 요청.
        RequestSetRuneServerRpc(slotIndex, runeId);
    }

    [ServerRpc]
    private void RequestSetRuneServerRpc(int slotIndex, string runeId, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
        {
            return;
        }

        if (slotIndex < 0 || slotIndex >= SlotCount || _equippedRuneIds.Count != SlotCount)
        {
            return;
        }

        if (DataManager.Inst == null)
        {
            SendMessageToOwner("DataManager가 준비되지 않았습니다.");
            return;
        }

        runeId ??= "";

        if (string.IsNullOrEmpty(runeId) == false)
        {
            Dictionary<string, RuneData> runeTable = DataManager.Inst.ReadData<RuneData>();

            Dictionary<string, ItemData> itemTable = DataManager.Inst.ReadData<ItemData>();

            if (runeTable.ContainsKey(runeId) == false || itemTable.ContainsKey(runeId) == false)
            {
                SendMessageToOwner("아이템 또는 룬 데이터가 없습니다.");
                return;
            }

            if (CanEquipOwnedRuneOnServer(runeId) == false)
            {
                SendMessageToOwner("서버 소유 검증이 연결되지 않았습니다. " + "테스트하려면 서버의 테스트 장착 옵션을 켜세요.");
                return;
            }
        }

        // 현재 장착 상태를 복사한 뒤 요청받은 슬롯만 변경.
        // 검증 성공 전에는 실제 네트워크 상태를 변경하지 않음.
        RuneData[] candidateSlots = ReadEquippedRunes();
        if (candidateSlots == null)
        {
            SendMessageToOwner("현재 장착 데이터를 읽을 수 없습니다.");
            return;
        }

        candidateSlots[slotIndex] = string.IsNullOrEmpty(runeId) ? null : DataManager.Inst.GetData<RuneData>(runeId);

        // 동일 룬 중복과 룬 설정 오류는 기존 Builder로 검증.
        if (!AttackProfileBuilder.TryBuild(
                candidateSlots, out _, out string error))
        {
            SendMessageToOwner(error);
            return;
        }

        // FixedString의 UTF-8 용량을 넘는 ID도 거절.
        FixedString128Bytes networkId = default;

        if (System.Text.Encoding.UTF8.GetByteCount(runeId)
            > networkId.Capacity)
        {
            SendMessageToOwner("룬 ID가 너무 깁니다.");
            return;
        }

        networkId = new FixedString128Bytes(runeId);
        _equippedRuneIds[slotIndex] = networkId;

        SendMessageToOwner(
            string.IsNullOrEmpty(runeId)
                ? "룬을 해제했습니다."
                : "룬을 장착했습니다.");
    }

    private bool CanEquipOwnedRuneOnServer(string runeId)
    {
        // 현재 InventoryService에는 플레이어별 서버 소유 정보가 없음.
        // 테스트에서는 데이터에 존재하는 룬의 장착을 허용.
        //
        // 추후:
        // 이 플레이어의 서버 인벤토리에 runeId가 있는지,
        // 장착 가능한 수량이 남아 있는지 검사하도록 교체.
        return IsServer && _allowTestEquipment;
    }

    private RuneData[] ReadEquippedRunes()
    {
        if (DataManager.Inst == null ||
            _equippedRuneIds.Count != SlotCount)
        {
            return null;
        }

        var slots = new RuneData[SlotCount];

        for (int i = 0; i < SlotCount; i++)
        {
            string id = _equippedRuneIds[i].ToString();

            if (string.IsNullOrEmpty(id))
                continue;

            slots[i] = DataManager.Inst.GetData<RuneData>(id);

            if (slots[i] == null)
                return null;
        }

        return slots;
    }

    // 실제 공격 컨트롤러가 호출.
    public bool TryBuildAttackProfile(
        out AttackProfile profile,
        out string error)
    {
        profile = null;
        error = null;

        if (!IsServer || !IsSpawned)
        {
            error = "서버에서 스폰된 플레이어만 공격을 계산할 수 있습니다.";
            return false;
        }

        RuneData[] slots = ReadEquippedRunes();

        if (slots == null)
        {
            error = "장착 룬 데이터를 읽지 못했습니다.";
            return false;
        }

        return AttackProfileBuilder.TryBuild(slots, out profile, out error);
    }

    private void SendMessageToOwner(string message)
    {
        var clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { OwnerClientId }
            }
        };

        EquipmentMessageClientRpc(message, clientRpcParams);
    }

    [ClientRpc]
    private void EquipmentMessageClientRpc(
        string message,
        ClientRpcParams clientRpcParams = default)
    {
        ViewModel.SetMessage(message);
    }
}