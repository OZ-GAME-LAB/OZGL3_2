using System.Collections.Generic;

public class AttackProfile
{
    public bool _useProjectile;
    public DamageType _attackElement = DamageType.Physical;

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
