public interface IRuneStatusReceiver
{
    void ApplyStatusOnServer(string statusProfileId, RuneCombatActor attacker);
}
