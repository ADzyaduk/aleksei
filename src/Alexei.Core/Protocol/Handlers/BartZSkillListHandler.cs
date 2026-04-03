using Alexei.Core.GameState;

namespace Alexei.Core.Protocol.Handlers;

/// <summary>
/// Bartz compact SkillList candidate observed around skill window open.
/// Packet shape confirmed from live capture:
///   int32 count
///   repeated entries of 13 bytes:
///     int32 passiveFlag
///     int32 level
///     int32 skillId
///     byte  unknown
///
/// This matches live client casts much better than the previous 72-byte table packet.
/// Example skills seen in the packet and then cast by client: 29, 83, 95, 120.
/// </summary>
public sealed class BartZSkillListHandler : IPacketHandler
{
    private const int EntrySize = 13;

    public byte BaseOpcode => 0x45;

    public void Handle(byte[] payload, GameWorld world)
    {
        if (payload.Length < 4)
            return;

        int count = BitConverter.ToInt32(payload, 0);
        if (count <= 0 || count > 512)
            return;

        int expected = 4 + (count * EntrySize);
        if (payload.Length != expected)
            return;

        var reader = new PacketReader(payload);
        int parsedCount = reader.ReadInt32();
        if (parsedCount != count)
            return;

        world.Skills.Clear();
        for (int i = 0; i < count && reader.Remaining >= EntrySize; i++)
        {
            int passiveFlag = reader.ReadInt32();
            int level = reader.ReadInt32();
            int skillId = reader.ReadInt32();
            reader.Skip(1);

            if (skillId <= 0 || level <= 0)
                continue;

            world.Skills[skillId] = new SkillInfo
            {
                SkillId = skillId,
                Level = level,
                IsPassive = passiveFlag != 0,
                IsToggle = false,
                IsItemLike = false
            };
        }

        world.NotifyUpdated();
    }
}
