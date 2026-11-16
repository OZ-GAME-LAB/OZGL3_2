using System;
using Unity.Netcode;

public partial class BossNetwork
{
    private readonly NetworkVariable<bool> _combatActive = new(false);
    private readonly NetworkVariable<double> _introStartTime = new(-1d);
    private bool _startCombatWhenReady = true;
    public bool CombatActive => _combatActive.Value;
    public double IntroStartTime => _introStartTime.Value;
    public event Action DiedOnServer;

    public bool BeginIntroductionOnServer(double startTime)
    {
        if (!IsServer || !IsSpawned || !IsReady || CombatActive || !IsAlive) return false;
        _introStartTime.Value = startTime;
        return true;
    }

    public bool StartCombatOnServer()
    {
        if (!IsServer || !IsSpawned || !IsReady || !IsAlive || _bt == null) return false;
        _combatActive.Value = true;
        _bt.enabled = true;
        return true;
    }
}
