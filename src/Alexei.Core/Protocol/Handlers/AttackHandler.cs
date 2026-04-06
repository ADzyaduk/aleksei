using Alexei.Core.GameState;

namespace Alexei.Core.Protocol.Handlers;

public sealed class AttackHandler : IPacketHandler
{
    public byte BaseOpcode => Opcodes.GameS2C.Attack;

    public void Handle(byte[] payload, GameWorld world)
    {
        if (payload.Length < 12) return;

        var r = new PacketReader(payload);
        int attackerId = r.ReadInt32();
        int targetId = r.ReadInt32();
        int damage = r.ReadInt32();
        DateTime now = DateTime.UtcNow;

        if (world.Party.TryGetValue(attackerId, out var member))
        {
            member.TargetId = targetId;
            member.LastUpdateUtc = now;
            world.NotifyUpdated();
        }
        else if (world.Characters.TryGetValue(attackerId, out var character))
        {
            character.TargetId = targetId;
            character.LastUpdateUtc = now;
            world.NotifyUpdated();
        }

        if (targetId == world.Me.ObjectId && world.Npcs.TryGetValue(attackerId, out var npc))
        {
            npc.LastAttackOnMeUtc = now;
            npc.LastUpdate = now;
        }
    }
}
