using Alexei.Core.GameState;

namespace Alexei.Core.Protocol.Handlers;

public sealed class DieHandler : IPacketHandler
{
    public byte BaseOpcode => Opcodes.GameS2C.Die;

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
            // Do NOT remove SpoiledNpcs here — AutoCombatTask sweep needs it
        }

        world.NotifyUpdated();
    }
}
