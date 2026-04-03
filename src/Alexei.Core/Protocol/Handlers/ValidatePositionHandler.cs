using Alexei.Core.GameState;

namespace Alexei.Core.Protocol.Handlers;

public sealed class ValidatePositionHandler : IPacketHandler
{
    public byte BaseOpcode => Opcodes.GameS2C.ValidatePosition;

    public void Handle(byte[] payload, GameWorld world)
    {
        if (payload.Length < 20) return;

        var r = new PacketReader(payload);
        int objectId = r.ReadInt32();
        int x = r.ReadInt32();
        int y = r.ReadInt32();
        int z = r.ReadInt32();
        int heading = r.ReadInt32();

        if (objectId == world.Me.ObjectId)
        {
            world.Me.X = x;
            world.Me.Y = y;
            world.Me.Z = z;
            world.Me.Heading = heading;
        }
    }
}
