using Unity.Netcode;

public enum RestStep : byte { Waiting, Fading, Restoring, Releasing, Ended }

public sealed partial class ScenarioManager
{
    public void RequestRest(uint request, bool enter)
    {
        if (IsSpawned && IsClient) RequestRestServerRpc(request, enter);
    }

    public void AcknowledgeRest(uint request, uint run, RestStep phase, bool success)
    {
        if (IsSpawned && IsClient) AcknowledgeRestServerRpc(request, run, phase, success);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestRestServerRpc(uint request, bool enter, ServerRpcParams rpc = default)
    {
        if (IsReady && RestTent.Active != null) RestTent.Active.RequestOnServer(rpc.Receive.SenderClientId, request, enter);
        else SendRestStepOnServer(rpc.Receive.SenderClientId, request, 0, RestStep.Ended, 0, 0,
            System.Array.Empty<ulong>(), "텐트가 준비되지 않았습니다.");
    }

    [ServerRpc(RequireOwnership = false)]
    private void AcknowledgeRestServerRpc(uint request, uint run, RestStep phase, bool success, ServerRpcParams rpc = default)
    {
        RestTent.Active?.AcknowledgeOnServer(rpc.Receive.SenderClientId, request, run, phase, success);
    }

    public void SendRestStepOnServer(ulong id, uint request, uint run, RestStep phase,
        int accepted, int total, ulong[] resources, string message)
    {
        if (!IsServer || !IsSpawned || !NetworkManager.ConnectedClients.ContainsKey(id)) return;
        ReceiveRestStepClientRpc(request, run, phase, accepted, total, resources, message,
            new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { id } } });
    }

    [ClientRpc]
    private void ReceiveRestStepClientRpc(uint request, uint run, RestStep phase, int accepted, int total,
        ulong[] resources, string message, ClientRpcParams rpc = default)
    {
        if (RestTent.Active != null) RestTent.Active.ReceiveRestStep(request, run, phase, accepted, total, resources, message);
        else if (phase != RestStep.Ended) RequestRest(request, false);
    }
}
