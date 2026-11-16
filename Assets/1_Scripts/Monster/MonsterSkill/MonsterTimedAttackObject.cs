using System;
using UnityEngine;

public abstract class MonsterTimedAttackObject : MonoBehaviour
{
    protected MonsterNetwork Owner { get; private set; }
    protected RuneDamageInfo DamageInfo { get; private set; }
    protected Vector3 Origin { get; private set; }
    protected Vector2 Direction { get; private set; }
    protected float Lifetime { get; private set; }
    public bool IsPlaying { get; private set; }
    public abstract float DurationSeconds { get; }
    public abstract bool HasValidSettings();
    public event Action<MonsterTimedAttackObject> Finished;

    private double _startTime;
    private float _previousElapsed;

    public void Play(MonsterNetwork owner, RuneDamageInfo damageInfo, Vector3 position,
        Vector2 direction, double startTime, float duration)
    {
        Owner = owner;
        DamageInfo = damageInfo;
        Origin = position;
        Direction = direction.normalized;
        Lifetime = duration;
        _startTime = startTime;
        _previousElapsed = 0f;
        transform.SetPositionAndRotation(position, Quaternion.identity);
        IsPlaying = true;
        gameObject.SetActive(true);
        OnPlay();
        Advance();
    }

    protected virtual void OnPlay() { }
    protected abstract void Tick(float elapsed, float previousElapsed);
    protected virtual void OnStopped() { }

    private void FixedUpdate()
    {
        if (Owner != null && Owner.IsServer)
            Advance();
    }

    private void Update()
    {
        if (Owner == null || !Owner.IsServer)
            Advance();
    }

    private void Advance()
    {
        if (!IsPlaying)
            return;

        if (Owner == null || !Owner.IsSpawned || !Owner.IsAlive || Owner.NetworkManager == null)
        {
            Stop();
            return;
        }

        float elapsed = Mathf.Clamp((float)(Owner.NetworkManager.ServerTime.Time - _startTime), 0f, Lifetime);
        Tick(elapsed, _previousElapsed);
        _previousElapsed = elapsed;
        if (elapsed >= Lifetime)
            Stop();
    }

    public void Stop()
    {
        if (!IsPlaying)
            return;

        IsPlaying = false;
        OnStopped();
        Owner = null;
        gameObject.SetActive(false);
        Finished?.Invoke(this);
    }

    private void OnDisable()
    {
        Stop();
    }

    protected static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
