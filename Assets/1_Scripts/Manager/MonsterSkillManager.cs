using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;

public class MonsterSkillManager : MonoBehaviour
{
    public static MonsterSkillManager Inst { get; private set; }

    private readonly Dictionary<string, Type> _skillTypesList = new();

    private void Awake()
    {
        if (Inst != null && Inst != this)
        {
            Destroy(gameObject);
            return;
        }

        Inst = this;
        RegisterSkillTypes();
    }

    private void OnDestroy()
    {
        if (Inst == this)
            Inst = null;
    }

    private void RegisterSkillTypes()
    {
        _skillTypesList.Clear();

        Type baseType = typeof(MonsterSkillBase);

        Type[] types = baseType.Assembly.GetTypes();

        foreach (Type type in types)
        {
            if (!type.IsClass || type.IsAbstract || type.ContainsGenericParameters || !type.IsSubclassOf(baseType))
            {
                continue;
            }

            if (_skillTypesList.ContainsKey(type.Name))
            {
                Debug.LogError($"중복된 스킬 클래스 이름: {type.Name}");
                continue;
            }

            _skillTypesList.Add(type.Name, type);

            Debug.Log($"스킬 클래스 등록: {type.Name}");
        }
    }

    public bool TryGetSkillType(string className, out Type skillType)
    {
        skillType = null;

        if (string.IsNullOrWhiteSpace(className))
        {
            Debug.LogError("스킬 클래스 이름이 비어 있습니다.", this);
            return false;
        }

        if (!_skillTypesList.TryGetValue(className, out skillType))
        {
            Debug.LogError($"등록되지 않은 스킬 클래스입니다: {className}", this);
            return false;
        }

        return true;
    }

    public async UniTask<MonsterSkillBase> CreateSkillAsync(string skillId, Transform owner, float bodyHeight, float atk)
    {
        if (owner == null)
        {
            Debug.LogError($"[스킬 초기화 실패] ID: {skillId}, 원인: 스킬 소유 오브젝트가 없습니다.", this);
            return null;
        }

        if (string.IsNullOrWhiteSpace(skillId))
        {
            Debug.LogError($"[스킬 초기화 실패] 대상: {owner.name}, 원인: 스킬 ID가 비어 있습니다.", this);
            return null;
        }

        if (DataManager.Inst == null)
        {
            Debug.LogError($"[스킬 초기화 실패] ID: {skillId}, 원인: DataManager가 준비되지 않았습니다.", this);
            return null;
        }

        MonsterSkillData data = DataManager.Inst.GetData<MonsterSkillData>(skillId);
        if (data == null)
        {
            Debug.LogError($"[스킬 초기화 실패] ID: {skillId}, 원인: 스킬 데이터를 찾을 수 없습니다.", this);
            return null;
        }

        string skillContext = $"ID: {skillId}, 클래스: {data.SkillClassName}, 프리팹: {data.SkillPrefabPath}";
        if (!TryGetSkillType(data.SkillClassName, out Type skillType))
        {
            Debug.LogError($"[스킬 초기화 실패] {skillContext}, 원인: 스킬 클래스가 비어 있거나 등록되지 않았습니다.", this);
            return null;
        }

        MonsterSkillBase skill = null;
        bool initialized = false;

        try
        {
            skill = owner.gameObject.AddComponent(skillType) as MonsterSkillBase;
            if (skill == null)
            {
                Debug.LogError($"[스킬 초기화 실패] {skillContext}, 원인: 스킬 컴포넌트를 추가하지 못했습니다.", this);
                return null;
            }

            skill.SetSkillId(skillId);
            string address = data.SkillPrefabPath;
            GameObject attackPrefab = null;

            if (string.IsNullOrWhiteSpace(address))
            {
                if (skill.RequiresAttackPrefab)
                {
                    Debug.LogError($"[스킬 초기화 실패] {skillContext}, 원인: 스킬 프리팹 경로가 비어 있습니다.", this);
                    return null;
                }
            }
            else
            {
                if (ResourceManager.Inst == null)
                {
                    Debug.LogError($"[스킬 초기화 실패] {skillContext}, 원인: ResourceManager가 준비되지 않았습니다.", this);
                    return null;
                }

                attackPrefab = await ResourceManager.Inst.LoadAsset<GameObject>(address);
                if (owner == null || skill == null)
                    return null;

                if (attackPrefab == null)
                {
                    Debug.LogError($"[스킬 초기화 실패] {skillContext}, 원인: 공격 프리팹을 불러오지 못했습니다.", this);
                    return null;
                }
            }

            if (!skill.ConfigureDamage(data))
                return null;

            initialized = skill.InitSkill(owner, bodyHeight, atk, attackPrefab);
            if (!initialized)
                return null;

            return skill;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[스킬 초기화 실패] {skillContext}, 원인: {exception.Message}", this);
            Debug.LogException(exception, this);
            return null;
        }
        finally
        {
            if (!initialized && skill != null)
                Destroy(skill);
        }
    }


}
