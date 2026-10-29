using Unity.Netcode;
using UnityEngine;

[UnityEngine.Scripting.Preserve]
public class MonsterMeleeAtkObj : MonoBehaviour
{
    private float _damage;

    public void Init(float damage)
    {
        _damage = damage;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_damage <= 0f)
            return;

        var networkManager = Unity.Netcode.NetworkManager.Singleton;

        if (networkManager == null || !networkManager.IsServer)
            return;

        PlayerControllerView player = other.GetComponentInParent<PlayerControllerView>();

        if (player == null)
            return;

        if (ServiceManager.Inst == null || ServiceManager.Inst.PlayerService == null)
            return;

        ApplyDamage();
    }

    private void ApplyDamage()
    {
        ServiceManager.Inst.PlayerService.DecreaseHp(_damage);
    }
}
