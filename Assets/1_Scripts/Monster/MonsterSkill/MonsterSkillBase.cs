using UnityEngine;

public class MonsterSkillBase : MonoBehaviour
{
    public virtual void InitSkill(Transform owner, float bodyHeight, float atk, GameObject attackPrefab) { }
    public virtual void UseSkill(Transform target, Vector3 attackPosition) { }
}
