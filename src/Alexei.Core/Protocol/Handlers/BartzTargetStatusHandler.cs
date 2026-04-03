using Alexei.Core.GameState;

namespace Alexei.Core.Protocol.Handlers;

/// <summary>
/// Bartz target-related packet observed as base 0xA3 / wire 0xB9.
/// The first dword consistently matches the selected target object id.
/// The remaining fields vary and are kept for future reverse-engineering.
/// </summary>
public sealed class BartzTargetStatusHandler : IPacketHandler
{
    public byte BaseOpcode => 0xA3;

    public void Handle(byte[] payload, GameWorld world)
    {
        if (payload.Length < 4)
            return;

        var r = new PacketReader(payload);
        int objectId = r.ReadInt32();

        if (objectId == 0)
            return;

        if (world.Me.PendingTargetId == objectId || world.Me.TargetId == objectId)
        {
            world.Me.TargetId = objectId;
            world.Me.PendingTargetId = 0;
            world.NotifyUpdated();
        }
    }
}

