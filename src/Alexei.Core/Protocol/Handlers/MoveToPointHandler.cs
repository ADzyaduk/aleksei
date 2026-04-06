using Alexei.Core.GameState;

namespace Alexei.Core.Protocol.Handlers;

public sealed class MoveToPointHandler : IPacketHandler
{
    public byte BaseOpcode => Opcodes.GameS2C.MoveToPoint;

    public void Handle(byte[] payload, GameWorld world)
    {
        if (payload.Length < 28) return;

        var r = new PacketReader(payload);
        int objectId = r.ReadInt32();
        int destX = r.ReadInt32();
        int destY = r.ReadInt32();
        int destZ = r.ReadInt32();
        int origX = r.ReadInt32();
        int origY = r.ReadInt32();
        int origZ = r.ReadInt32();
        DateTime now = DateTime.UtcNow;

        if (objectId == world.Me.ObjectId)
        {
            world.Me.X = destX;
            world.Me.Y = destY;
            world.Me.Z = destZ;
            world.Me.IsSitting = false;
            world.LastSelfMoveEvidenceUtc = now;
            world.LastCombatProgressUtc = now;
            world.PositionConfidence = PositionConfidence.Confirmed;
            world.NotifyUpdated();
        }
        else if (world.Party.TryGetValue(objectId, out var member))
        {
            member.X = destX;
            member.Y = destY;
            member.Z = destZ;
            member.LastPositionUpdateUtc = now;
            member.LastUpdateUtc = now;
            world.NotifyUpdated();
        }
        else if (world.Characters.TryGetValue(objectId, out var character))
        {
            character.X = destX;
            character.Y = destY;
            character.Z = destZ;
            character.LastPositionUpdateUtc = now;
            character.LastUpdateUtc = now;
            world.NotifyUpdated();
        }
        else if (world.Npcs.TryGetValue(objectId, out var npc))
        {
            npc.X = origX;
            npc.Y = origY;
            npc.Z = origZ;
            npc.LastUpdate = now;
        }
    }
}
