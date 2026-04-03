using Alexei.Core.GameState;

namespace Alexei.Core.Protocol.Handlers;

public sealed class AttackHandler : IPacketHandler
{
    public byte BaseOpcode => Opcodes.GameS2C.Attack;

    public void Handle(byte[] payload, GameWorld world)
    {
        // Attack: attackerId(4) + targetId(4) + damage(4) + ...
        // We track this mainly for diagnostics
        if (payload.Length < 12) return;

        var r = new PacketReader(payload);
        int attackerId = r.ReadInt32();
        int targetId = r.ReadInt32();
        int damage = r.ReadInt32();

        if (targetId == world.Me.ObjectId && world.Npcs.TryGetValue(attackerId, out var npc))
        {
            npc.LastAttackOnMeUtc = DateTime.UtcNow;
            npc.LastUpdate = DateTime.UtcNow;
        }
    }
}
