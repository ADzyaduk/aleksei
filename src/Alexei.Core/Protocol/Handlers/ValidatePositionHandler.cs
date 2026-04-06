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
        DateTime now = DateTime.UtcNow;

        if (objectId == world.Me.ObjectId)
        {
            world.Me.X = x;
            world.Me.Y = y;
            world.Me.Z = z;
            world.Me.Heading = heading;
            world.LastSelfMoveEvidenceUtc = now;
            world.PositionConfidence = PositionConfidence.Confirmed;
            world.NotifyUpdated();
        }
        else if (world.Party.TryGetValue(objectId, out var member))
        {
            member.X = x;
            member.Y = y;
            member.Z = z;
            member.Heading = heading;
            member.LastPositionUpdateUtc = now;
            member.LastUpdateUtc = now;
            world.NotifyUpdated();
        }
        else if (world.Characters.TryGetValue(objectId, out var character))
        {
            character.X = x;
            character.Y = y;
            character.Z = z;
            character.Heading = heading;
            character.LastPositionUpdateUtc = now;
            character.LastUpdateUtc = now;
            world.NotifyUpdated();
        }
    }
}
