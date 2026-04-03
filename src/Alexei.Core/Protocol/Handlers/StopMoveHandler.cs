using Alexei.Core.GameState;

namespace Alexei.Core.Protocol.Handlers;

public sealed class StopMoveHandler : IPacketHandler
{
    public byte BaseOpcode => Opcodes.GameS2C.StopMove;

    public void Handle(byte[] payload, GameWorld world)
    {
        if (payload.Length < 4) return;

        var r = new PacketReader(payload);
        int objectId = r.ReadInt32();

        // Update position if full coordinates provided
        if (r.Remaining >= 12)
        {
            int x = r.ReadInt32();
            int y = r.ReadInt32();
            int z = r.ReadInt32();

            if (objectId == world.Me.ObjectId)
            {
                world.Me.X = x;
                world.Me.Y = y;
                world.Me.Z = z;
            }
            else if (world.Npcs.TryGetValue(objectId, out var npc))
            {
                npc.X = x;
                npc.Y = y;
                npc.Z = z;
                npc.LastUpdate = DateTime.UtcNow;
            }
        }
    }
}
