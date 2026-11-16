using System;
using Unity.Netcode;

public enum ResourceAnimationKind : byte { None, Logging, Mining, Fishing }
public enum ResourceAnimationPhase : byte { None, Preparing, Casting, Active, WaitingForInput, Swing }

// 요청 번호와 모션을 한 번에 전달하여 서로 다른 요청의 필드가 섞이지 않게 합니다.
public struct PlayerResourceAnimationState : INetworkSerializable, IEquatable<PlayerResourceAnimationState>
{
    public ulong RequestId;
    public ResourceAnimationKind Kind;
    public ResourceAnimationPhase Phase;
    public double StartTime;
    public float CastDuration;
    public int SwingSequence;
    public float MotionDuration;
    public int FacingDirection;
    public bool PlaysAnimation => Kind != ResourceAnimationKind.None &&
        (Phase == ResourceAnimationPhase.Casting || Phase == ResourceAnimationPhase.Active || Phase == ResourceAnimationPhase.Swing);

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref RequestId);
        serializer.SerializeValue(ref Kind);
        serializer.SerializeValue(ref Phase);
        serializer.SerializeValue(ref StartTime);
        serializer.SerializeValue(ref CastDuration);
        serializer.SerializeValue(ref SwingSequence);
        serializer.SerializeValue(ref MotionDuration);
        serializer.SerializeValue(ref FacingDirection);
    }

    public bool Equals(PlayerResourceAnimationState other) => RequestId == other.RequestId &&
        Kind == other.Kind && Phase == other.Phase && StartTime.Equals(other.StartTime) &&
        CastDuration.Equals(other.CastDuration) && SwingSequence == other.SwingSequence &&
        MotionDuration.Equals(other.MotionDuration) && FacingDirection == other.FacingDirection;
}
