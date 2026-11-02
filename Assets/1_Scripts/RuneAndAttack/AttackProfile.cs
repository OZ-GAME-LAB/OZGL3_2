using System.Collections.Generic;

public enum RuneAttackElement
{
    Physical,
    Fire,
    Cold,
    Lightning
}

public class AttackProfile
{
    public bool _useProjectile;
    public RuneAttackElement _attackElement = RuneAttackElement.Physical;

    public float _damageMultiple = 1f;
    public float _attackSpeedMultiple = 1f;
    public float _rangeMultiple = 1f;
    public float _projectileSizeMultiple = 1f;

    public int _hitCount = 1;
    public bool _preventAttackInterrupt;
    public float _healAttackRatio;
    public string _attackPrefabPath;

    public readonly List<string> _onHitStatusProfileIdList = new();
}
