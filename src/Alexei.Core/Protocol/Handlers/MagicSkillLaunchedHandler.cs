using Alexei.Core.GameState;

namespace Alexei.Core.Protocol.Handlers;

public sealed class MagicSkillLaunchedHandler : IPacketHandler
{
    public byte BaseOpcode => Opcodes.GameS2C.MagicSkillLaunched;

    public void Handle(byte[] payload, GameWorld world)
    {
        if (TryHandleBartzNamedCharacter(payload, world))
            return;

        if (payload.Length < 16) return;

        var r = new PacketReader(payload);
        int casterId = r.ReadInt32();
        int targetId = r.ReadInt32();
        int skillId = r.ReadInt32();
        int level = r.ReadInt32();

        if (world.Party.TryGetValue(casterId, out var member))
        {
            member.TargetId = targetId;
            member.LastUpdateUtc = DateTime.UtcNow;
            world.NotifyUpdated();
        }
        else if (world.Characters.TryGetValue(casterId, out var character))
        {
            character.TargetId = targetId;
            character.LastUpdateUtc = DateTime.UtcNow;
            world.NotifyUpdated();
        }

        if (casterId == world.Me.ObjectId && world.Skills.TryGetValue(skillId, out var skill))
        {
            // Server confirmed cast - cooldown tracking handled by SkillCoolTime packet
        }
    }

    private static bool TryHandleBartzNamedCharacter(byte[] payload, GameWorld world)
    {
        if (payload.Length < 12)
            return false;

        var r = new PacketReader(payload);
        int objectId = r.ReadInt32();
        if (objectId == 0)
            return false;

        string name = r.ReadString();
        if (!LooksLikeCharacterName(name))
            return false;

        DateTime now = DateTime.UtcNow;
        var character = world.Characters.GetOrAdd(objectId, id => new PartyMember { ObjectId = id });
        character.Name = name;
        character.LastUpdateUtc = now;

        if (world.Party.TryGetValue(objectId, out var partyMember))
        {
            partyMember.Name = name;
            partyMember.LastUpdateUtc = now;
        }

        world.NotifyUpdated();
        return true;
    }

    private static bool LooksLikeCharacterName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 24)
            return false;

        foreach (char ch in value)
        {
            if (char.IsLetterOrDigit(ch) || ch is '_' or '-')
                continue;

            return false;
        }

        return true;
    }
}
