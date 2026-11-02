using UnityEngine;

public class RuneCombinationTest : MonoBehaviour
{
    [SerializeField] private string[] _runeIds;
    [ContextMenu("테스트/룬 조합 계산")]

    private void TestCombination()
    {
        if (Application.isPlaying == false || DataManager.Inst == null)
        {
            return;
        }

        if (_runeIds == null)
        {
            return;
        }

        var slots = new RuneData[_runeIds.Length];

        for (int i = 0; i < _runeIds.Length; i++)
        {
            string id = _runeIds[i];

            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            slots[i] = DataManager.Inst.GetData<RuneData>(id);

            if (slots[i] == null)
            {
                Debug.LogError($"룬 데이터를 찾을 수 없습니다: {id}", this);
                return;
            }
        }

        if (AttackProfileBuilder.TryBuild(slots, out var profile, out string error) == false)
        {
            Debug.LogError(error, this);
            return;
        }

        Debug.Log(
            $"투사체: {profile._useProjectile}\n" +
            $"속성: {profile._attackElement}\n" +
            $"타격 수: {profile._hitCount}\n" +
            $"피해 배율: {profile._damageMultiple:F4}\n" +
            $"공격속도 배율: {profile._attackSpeedMultiple}\n" +
            $"범위 배율: {profile._rangeMultiple}\n" +
            $"투사체 크기 배율: {profile._projectileSizeMultiple}\n" +
            $"공격 중단 방지: {profile._preventAttackInterrupt}\n" +
            $"피해 기준 회복 비율: {profile._healAttackRatio}\n" +
            $"적중 상태이상: {string.Join(", ", profile._onHitStatusProfileIdList)}",
            this);
    }
}
