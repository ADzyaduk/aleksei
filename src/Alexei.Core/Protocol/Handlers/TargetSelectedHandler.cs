using Alexei.Core.GameState;

namespace Alexei.Core.Protocol.Handlers;

public sealed class TargetSelectedHandler : IPacketHandler
{
    public byte BaseOpcode => Opcodes.GameS2C.TargetSelected;

    public void Handle(byte[] payload, GameWorld world)
    {
        if (payload.Length < 4) return;

        var r = new PacketReader(payload);
        // Teon variant carries objectId first. Bartz emits an 8-byte target-like packet
        // (objectId + state), but captures show these packets are noisy and can refer to
        // nearby entities. Only apply Bartz updates when they match our current/pending target.
        int targetedObjectId = r.ReadInt32();
        bool hasState = r.Remaining >= 4;
        int state = hasState ? r.ReadInt32() : 1;

        if (!hasState)
        {
            world.Me.TargetId = targetedObjectId;
            world.Me.PendingTargetId = 0;
            world.NotifyUpdated();
            return;
        }

        bool relatesToTrackedTarget =
            world.Me.PendingTargetId == targetedObjectId ||
            world.Me.TargetId == targetedObjectId;

        if (!relatesToTrackedTarget)
        {
            return;
        }

        if (state == 1)
        {
            world.Me.TargetId = targetedObjectId;
            world.Me.PendingTargetId = 0;
        }
        else if (state == 2 || state == 3)
        {
            world.Me.TargetId = 0;
            world.Me.PendingTargetId = 0;
        }

        world.NotifyUpdated();
    }
}
