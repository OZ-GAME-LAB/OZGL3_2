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

    public bool TryGetSkillType(string address, out Type skillType)
    {
        skillType = null;

        if (string.IsNullOrWhiteSpace(address))
            return false;

        int separatorIndex = address.IndexOf('/');

        if (separatorIndex <= 0 || separatorIndex == address.Length - 1)
        {
            Debug.LogError($"스킬 주소 형식을 확인하세요: {address}");
            return false;
        }

        string className = address.Substring(0, separatorIndex);

        if (!_skillTypesList.TryGetValue(className, out skillType))
        {
            Debug.LogError($"등록되지 않은 스킬 클래스입니다: {className}");
            return false;
        }

        return true;
    }

    public async UniTask<MonsterSkillBase> CreateSkillAsync(string address, Transform owner, float bodyHeight, float atk)
    {
        if (owner == null || bodyHeight <= 0f)
        {
            Debug.LogError("스킬을 사용할 몬스터와 몸 높이를 확인하세요.");
            return null;
        }

        if (!TryGetSkillType(address, out Type skillType))
            return null;

        if (ResourceManager.Inst == null)
        {
            Debug.LogError("ResourceManager가 준비되지 않았습니다.");
            return null;
        }

        GameObject attackPrefab = await ResourceManager.Inst.LoadAsset<GameObject>(address);
        if (attackPrefab == null || owner == null)
            return null;

        MonsterSkillBase skill = owner.gameObject.AddComponent(skillType) as MonsterSkillBase;
        if (skill == null)
        {
            Debug.LogError($"스킬 컴포넌트를 추가하지 못했습니다: {address}");
            return null;
        }

        // 공격 오브젝트 생성·크기 설정·공격력 전달
        skill.InitSkill(owner, bodyHeight, atk, attackPrefab);

        return skill;
    }


}
