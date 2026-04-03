using Alexei.Core.GameState;

namespace Alexei.Core.Protocol.Handlers;

public sealed class StatusUpdateHandler : IPacketHandler
{
    public byte BaseOpcode => Opcodes.GameS2C.StatusUpdate;

    public void Handle(byte[] payload, GameWorld world)
    {
        if (payload.Length < 8) return;

        var r = new PacketReader(payload);
        int objectId = r.ReadInt32();
        int count = r.ReadInt32();

        bool isSelf = objectId == world.Me.ObjectId;
        bool changedCombatState = false;

        for (int i = 0; i < count && r.Remaining >= 8; i++)
        {
            int attrId = r.ReadInt32();
            int value = r.ReadInt32();

            if (isSelf)
            {
                switch (attrId)
                {
                    case Opcodes.Attr.CurHp: world.Me.CurHp = value; changedCombatState = true; break;
                    case Opcodes.Attr.MaxHp: world.Me.MaxHp = value; changedCombatState = true; break;
                    case Opcodes.Attr.CurMp: world.Me.CurMp = value; changedCombatState = true; break;
                    case Opcodes.Attr.MaxMp: world.Me.MaxMp = value; changedCombatState = true; break;
                    case Opcodes.Attr.CurCp: world.Me.CurCp = value; changedCombatState = true; break;
                    case Opcodes.Attr.MaxCp: world.Me.MaxCp = value; changedCombatState = true; break;
                    case Opcodes.Attr.Level: world.Me.Level = value; break;
                    case Opcodes.Attr.CurExp: world.Me.Exp = value; break;
                    case Opcodes.Attr.SP: world.Me.SP = value; break;
                }
            }
            else if (world.Party.TryGetValue(objectId, out var member))
            {
                switch (attrId)
                {
                    case Opcodes.Attr.CurHp: member.CurHp = value; break;
                    case Opcodes.Attr.MaxHp: member.MaxHp = value; break;
                    case Opcodes.Attr.CurMp: member.CurMp = value; break;
                    case Opcodes.Attr.MaxMp: member.MaxMp = value; break;
                }
            }
            else if (world.Npcs.TryGetValue(objectId, out var npc))
            {
                switch (attrId)
                {
                    case Opcodes.Attr.CurHp: npc.CurHp = value; changedCombatState = true; break;
                    case Opcodes.Attr.MaxHp: npc.MaxHp = value; changedCombatState = true; break;
                    case Opcodes.Attr.CurMp: npc.CurMp = value; break;
                    case Opcodes.Attr.MaxMp: npc.MaxMp = value; break;
                    case Opcodes.Attr.CurCp: npc.CurCp = value; break;
                    case Opcodes.Attr.MaxCp: npc.MaxCp = value; break;
                }
            }
        }

        if (world.Npcs.TryGetValue(objectId, out var updatedNpc))
        {
            if (updatedNpc.MaxHp > 0)
                updatedNpc.HpPercent = Math.Clamp((int)Math.Round(updatedNpc.CurHp * 100.0 / updatedNpc.MaxHp), 0, 100);
            updatedNpc.IsDead = updatedNpc.MaxHp > 0 && updatedNpc.CurHp <= 0;
            if (updatedNpc.IsDead)
            {
                updatedNpc.LastDeathEvidenceUtc = DateTime.UtcNow;
            }
            else if (updatedNpc.CurHp > 0)
            {
                updatedNpc.LastDeathEvidenceUtc = null;
                updatedNpc.LastDropEvidenceUtc = null;
            }
            updatedNpc.LastUpdate = DateTime.UtcNow;
        }

        if (changedCombatState)
            world.LastCombatProgressUtc = DateTime.UtcNow;

        world.NotifyUpdated();
    }
}
