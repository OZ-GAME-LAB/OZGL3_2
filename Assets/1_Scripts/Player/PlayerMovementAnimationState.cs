using System;
using Unity.Netcode;

// 한 번의 표현 판정 결과입니다. 게임플레이의 이동/접촉 판정에는 사용하지 않습니다.
public struct PlayerMovementAnimationState : INetworkSerializable, IEquatable<PlayerMovementAnimationState>
{
    public bool HasSample;
    public uint LifeVersion;
    public uint JumpSequence;
    public bool Moving;
    public bool Grounded;
    public bool Crouching;
    public bool WallClinging;
    public bool LedgeHanging;
    public bool Jumping;
    public bool IdleJump;
    public float VerticalSpeed;

    public static PlayerMovementAnimationState Neutral(uint lifeVersion, uint jumpSequence)
    {
        return new PlayerMovementAnimationState
        {
            HasSample = true, LifeVersion = lifeVersion, JumpSequence = jumpSequence, Grounded = true
        };
    }

    public bool SameDiscreteState(PlayerMovementAnimationState other)
    {
        return HasSample == other.HasSample && LifeVersion == other.LifeVersion &&
            JumpSequence == other.JumpSequence && Moving == other.Moving && Grounded == other.Grounded &&
            Crouching == other.Crouching && WallClinging == other.WallClinging &&
            LedgeHanging == other.LedgeHanging && Jumping == other.Jumping && IdleJump == other.IdleJump;
    }

    public bool Equals(PlayerMovementAnimationState other)
    {
        return SameDiscreteState(other) && VerticalSpeed.Equals(other.VerticalSpeed);
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref HasSample);
        serializer.SerializeValue(ref LifeVersion);
        serializer.SerializeValue(ref JumpSequence);
        serializer.SerializeValue(ref Moving);
        serializer.SerializeValue(ref Grounded);
        serializer.SerializeValue(ref Crouching);
        serializer.SerializeValue(ref WallClinging);
        serializer.SerializeValue(ref LedgeHanging);
        serializer.SerializeValue(ref Jumping);
        serializer.SerializeValue(ref IdleJump);
        serializer.SerializeValue(ref VerticalSpeed);
    }
}
