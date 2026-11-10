using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkRuneService : IDisposable
{
    private readonly InventoryService _inventoryService;

    private PlayerRuneEquipmentController _localEquipment;
    private RuneInventoryViewModel _viewModel;

    public NetworkRuneService(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    public RuneInventoryViewModel GetRuneInventoryViewModel()
    {
        if (_viewModel == null)
        {
            _viewModel = new RuneInventoryViewModel(
                PlayerRuneEquipmentController.SlotCount);
        }

        return _viewModel;
    }

    public bool BindRuneInventoryView(RuneInventoryUI view)
    {
        if (view == null)
            return false;

        if (!TryConnectLocalPlayer())
        {
            Debug.LogWarning("로컬 플레이어의 룬 장착 정보가 없습니다.");
            return false;
        }

        RefreshInventory();
        RefreshEquipment();

        view.Bind(this);
        return true;
    }

    private bool TryConnectLocalPlayer()
    {
        var manager = Unity.Netcode.NetworkManager.Singleton;

        if (manager == null ||
            !manager.IsClient ||
            manager.LocalClient == null)
        {
            return false;
        }

        NetworkObject player = manager.LocalClient.PlayerObject;

        if (player == null ||
            !player.TryGetComponent<PlayerRuneEquipmentController>(
                out var equipment) ||
            !equipment.IsSpawned ||
            !equipment.IsOwner)
        {
            return false;
        }

        if (_localEquipment == equipment)
            return true;

        DisconnectLocalPlayer();

        _localEquipment = equipment;

        _localEquipment.EquipmentChanged += RefreshEquipment;
        _localEquipment.RequestMessageReceived += OnRequestMessage;
        _localEquipment.Despawned += OnLocalPlayerDespawned;

        return true;
    }

    public void ConnectLocalPlayer()
    {
        if (TryConnectLocalPlayer() == false)
        {
            return;
        }

        RefreshInventory();
        RefreshEquipment();
    }

    public void RefreshInventory()
    {
        var ids = new List<string>();
        var addedIds = new HashSet<string>();

        if (_inventoryService != null && DataManager.Inst != null)
        {
            var runeTable = DataManager.Inst.ReadData<RuneData>();

            foreach (InventoryItemViewModel item
                     in _inventoryService.GetInventoryItems())
            {
                if (item == null ||
                    item.Count <= 0 ||
                    string.IsNullOrEmpty(item.Id))
                {
                    continue;
                }

                if (runeTable.ContainsKey(item.Id) &&
                    addedIds.Add(item.Id))
                {
                    ids.Add(item.Id);
                }
            }
        }

        ids.Sort(StringComparer.Ordinal);

        var vm = GetRuneInventoryViewModel();

        vm.SetInventoryRunes(ids);
        vm.SetMessage("룬을 선택한 뒤 장착 슬롯을 누르세요.");
    }

    private void RefreshEquipment()
    {
        var vm = GetRuneInventoryViewModel();

        for (int i = 0;
             i < PlayerRuneEquipmentController.SlotCount;
             i++)
        {
            string id = _localEquipment != null &&
                        _localEquipment.IsSpawned
                ? _localEquipment.GetEquippedRuneId(i)
                : "";

            vm.EquipmentSlots[i].SetRuneId(id);
        }
    }

    public void SelectRune(string runeId)
    {
        var vm = GetRuneInventoryViewModel();

        foreach (RuneSlotViewModel slot in vm.InventoryRunes)
        {
            if (slot.RuneId != runeId)
                continue;

            vm.SelectRune(runeId);
            vm.SetMessage("장착할 슬롯을 누르세요.");
            return;
        }
    }

    public void ClearSelection()
    {
        var vm = GetRuneInventoryViewModel();

        vm.SelectRune("");
        vm.SetMessage("장착 슬롯을 누르면 해당 룬을 해제합니다.");
    }

    public void RequestSlotClick(int slotIndex)
    {
        var vm = GetRuneInventoryViewModel();

        if (_localEquipment == null ||
            !_localEquipment.IsSpawned ||
            !_localEquipment.IsOwner)
        {
            vm.SetMessage("플레이어 연결을 확인하세요.");
            return;
        }

        // 호스트에서는 RPC가 즉시 처리될 수 있으므로
        // 대기 문구는 요청을 보내기 전에 설정.
        vm.SetMessage("서버에 장착 변경을 요청했습니다.");

        if (!_localEquipment.RequestSetRune(
                slotIndex, vm.SelectedRuneId))
        {
            vm.SetMessage("장착 변경을 요청하지 못했습니다.");
        }
    }

    private void OnRequestMessage(string message)
    {
        GetRuneInventoryViewModel().SetMessage(message);
    }

    private void OnLocalPlayerDespawned()
    {
        DisconnectLocalPlayer();
        RefreshEquipment();

        var vm = GetRuneInventoryViewModel();
        vm.SetInventoryRunes(Array.Empty<string>());
        vm.SetMessage("플레이어 연결이 종료되었습니다.");
    }

    private void DisconnectLocalPlayer()
    {
        // 파괴 중인 Unity 객체에서도 관리 이벤트 구독은 정리.
        if (!ReferenceEquals(_localEquipment, null))
        {
            _localEquipment.EquipmentChanged -= RefreshEquipment;
            _localEquipment.RequestMessageReceived -= OnRequestMessage;
            _localEquipment.Despawned -= OnLocalPlayerDespawned;
        }

        _localEquipment = null;
    }

    public void Dispose()
    {
        DisconnectLocalPlayer();
    }
}