using Unity.Netcode;
using UnityEngine;

public enum BossEncounterPhase : byte
{
    Ready, Prompt, Fading, Moving, Framing, Intro, Releasing, Fighting, Completed, Recovering
}

public sealed partial class ScenarioManager
{
    private readonly NetworkVariable<BossEncounterPhase> _bossPhase = new(BossEncounterPhase.Ready);
    public BossEncounterPhase BossPhase => _bossPhase.Value;
    private readonly NetworkVariable<ulong> _encounterBossId = new(ulong.MaxValue);
    public ulong EncounterBossId => _encounterBossId.Value;
    public void SetEncounterBossOnServer(ulong id)
    {
        if (IsServer && IsSpawned) _encounterBossId.Value = id;
    }

    public void SetBossPhaseOnServer(BossEncounterPhase phase)
    {
        if (IsServer && IsSpawned) _bossPhase.Value = phase;
    }

    public void RequestBossPrompt()
    {
        if (IsSpawned && IsClient) RequestBossPromptServerRpc();
    }
    public void ChooseBossEntry(uint run, bool enter)
    {
        if (IsSpawned && IsClient) ChooseBossEntryServerRpc(run, enter);
    }
    public void AcknowledgeBossStep(uint run, BossEncounterPhase phase, bool success)
    {
        if (IsSpawned && IsClient) AcknowledgeBossStepServerRpc(run, phase, success);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestBossPromptServerRpc(ServerRpcParams rpc = default)
    {
        if (IsReady) BossEncounterController.Active?.RequestPromptOnServer(rpc.Receive.SenderClientId);
    }
    [ServerRpc(RequireOwnership = false)]
    private void ChooseBossEntryServerRpc(uint run, bool enter, ServerRpcParams rpc = default)
    {
        if (IsReady) BossEncounterController.Active?.ChooseOnServer(rpc.Receive.SenderClientId, run, enter);
    }
    [ServerRpc(RequireOwnership = false)]
    private void AcknowledgeBossStepServerRpc(uint run, BossEncounterPhase phase, bool success, ServerRpcParams rpc = default)
    {
        BossEncounterController.Active?.AcknowledgeOnServer(rpc.Receive.SenderClientId, run, phase, success);
    }

    public void SendBossStepOnServer(ulong clientId, uint run, BossEncounterPhase phase, Vector3 destination)
    {
        if (!IsSpawned || !IsServer || !NetworkManager.ConnectedClients.ContainsKey(clientId)) return;
        ReceiveBossStepClientRpc(run, phase, destination, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        });
    }

    [ClientRpc]
    private void ReceiveBossStepClientRpc(uint run, BossEncounterPhase phase, Vector3 destination, ClientRpcParams rpc = default)
    {
        if (BossEncounterController.Active != null)
            BossEncounterController.Active.ReceiveStep(run, phase, destination);
        else
            AcknowledgeBossStep(run, phase, false);
    }
}
