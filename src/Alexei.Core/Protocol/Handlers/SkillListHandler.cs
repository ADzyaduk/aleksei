using Alexei.Core.GameState;

namespace Alexei.Core.Protocol.Handlers;

public sealed class SkillListHandler : IPacketHandler
{
    public byte BaseOpcode => Opcodes.GameS2C.SkillList;

    public void Handle(byte[] payload, GameWorld world)
    {
        if (payload.Length < 4) return;

        var r = new PacketReader(payload);
        int count = r.ReadInt32();

        world.Skills.Clear();
        for (int i = 0; i < count && r.Remaining >= 13; i++)
        {
            int isPassive = r.ReadInt32();
            int level = r.ReadInt32();
            int skillId = r.ReadInt32();
            r.Skip(1); // extra unk byte in Interlude format

            world.Skills[skillId] = new SkillInfo
            {
                SkillId = skillId,
                Level = level,
                IsPassive = isPassive != 0
            };
        }

        world.NotifyUpdated();
    }
}
