using JetBrains.Annotations;
using NUnit.Framework;
using UnityEngine;

public class PatrolArea : MonoBehaviour
{
    [SerializeField] private GameObject[] _patrolSpotList;

    public GameObject[] PatrolSpotList
    { 
        get
        {
            if(_patrolSpotList == null || _patrolSpotList.Length == 0)
            {
                Debug.LogError($"패트롤 {name}의 순찰 포인트가 지정되지 않았습니다.");
                return null;
            }

            foreach (GameObject spot in _patrolSpotList)
            {
                if (spot == null)
                {
                    Debug.LogError($"{name}: 비어 있는 순찰 지점이 있습니다.");
                    return null;
                }
            }
            return _patrolSpotList;
        }
    }
}
