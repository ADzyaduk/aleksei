using Alexei.Core.GameState;

namespace Alexei.Core.Protocol.Handlers;

/// <summary>
/// Short die notify (0x12 on Teon) — same logic as DieHandler.
/// </summary>
public sealed class Die2Handler : IPacketHandler
{
    public byte BaseOpcode => Opcodes.GameS2C.Die2;

    public void Handle(byte[] payload, GameWorld world)
    {
        if (payload.Length < 4) return;

        var r = new PacketReader(payload);
        int objectId = r.ReadInt32();

        if (objectId == world.Me.ObjectId)
        {
            world.Me.IsDead = true;
            world.Me.TargetId = 0;
        }
        else if (world.Npcs.TryGetValue(objectId, out var npc))
        {
            npc.IsDead = true;
            npc.LastDeathEvidenceUtc = DateTime.UtcNow;
            npc.LastUpdate = DateTime.UtcNow;
            world.SpoiledNpcs.TryRemove(objectId, out _);
        }

        world.NotifyUpdated();
    }
}
