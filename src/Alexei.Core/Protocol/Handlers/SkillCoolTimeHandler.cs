using Alexei.Core.GameState;

namespace Alexei.Core.Protocol.Handlers;

public sealed class SkillCoolTimeHandler : IPacketHandler
{
    public byte BaseOpcode => Opcodes.GameS2C.SkillCoolTime;

    public void Handle(byte[] payload, GameWorld world)
    {
        if (payload.Length < 4) return;

        var r = new PacketReader(payload);
        int count = r.ReadInt32();

        for (int i = 0; i < count && r.Remaining >= 12; i++)
        {
            int skillId = r.ReadInt32();
            int remaining = r.ReadInt32();
            int total = r.ReadInt32();

            if (world.Skills.TryGetValue(skillId, out var skill))
            {
                skill.SetCooldown(remaining);
            }
        }
    }
}
