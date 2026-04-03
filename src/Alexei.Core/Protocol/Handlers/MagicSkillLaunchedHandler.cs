using Alexei.Core.GameState;

namespace Alexei.Core.Protocol.Handlers;

public sealed class MagicSkillLaunchedHandler : IPacketHandler
{
    public byte BaseOpcode => Opcodes.GameS2C.MagicSkillLaunched;

    public void Handle(byte[] payload, GameWorld world)
    {
        if (payload.Length < 16) return;

        var r = new PacketReader(payload);
        int casterId = r.ReadInt32();
        int targetId = r.ReadInt32();
        int skillId = r.ReadInt32();
        int level = r.ReadInt32();

        // Track skill cooldown for our character
        if (casterId == world.Me.ObjectId && world.Skills.TryGetValue(skillId, out var skill))
        {
            // Server confirmed cast — cooldown tracking handled by SkillCoolTime packet
        }
    }
}
