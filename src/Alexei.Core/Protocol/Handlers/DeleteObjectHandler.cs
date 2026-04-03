using Alexei.Core.GameState;

namespace Alexei.Core.Protocol.Handlers;

public sealed class DeleteObjectHandler : IPacketHandler
{
    public byte BaseOpcode => Opcodes.GameS2C.DeleteObject;

    public void Handle(byte[] payload, GameWorld world)
    {
        if (payload.Length < 4) return;

        var r = new PacketReader(payload);
        int objectId = r.ReadInt32();

        world.Npcs.TryRemove(objectId, out _);
        world.Items.TryRemove(objectId, out _);

        if (objectId == world.Me.TargetId)
        {
            world.Me.TargetId = 0;
            world.NotifyUpdated();
        }
    }
}
