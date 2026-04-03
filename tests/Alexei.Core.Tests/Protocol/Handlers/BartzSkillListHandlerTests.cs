using Alexei.Core.GameState;
using Alexei.Core.Protocol.Handlers;
using Xunit;

namespace Alexei.Core.Tests.Protocol.Handlers;

public sealed class BartzSkillListHandlerTests
{
    [Fact]
    public void Handle_ParsesCompactBartzSkillRows()
    {
        var world = new GameWorld();
        var handler = new BartZSkillListHandler();

        handler.Handle(BuildPayload(
            CreateEntry(0, 7, 29, 0),
            CreateEntry(0, 1, 83, 0),
            CreateEntry(0, 1, 95, 0),
            CreateEntry(1, 3, 120, 0)), world);

        Assert.Equal(4, world.Skills.Count);

        Assert.False(world.Skills[29].IsPassive);
        Assert.Equal(7, world.Skills[29].Level);

        Assert.False(world.Skills[83].IsPassive);
        Assert.Equal(1, world.Skills[83].Level);

        Assert.False(world.Skills[95].IsPassive);
        Assert.Equal(1, world.Skills[95].Level);

        Assert.True(world.Skills[120].IsPassive);
        Assert.Equal(3, world.Skills[120].Level);
    }

    [Fact]
    public void Handle_IgnoresInvalidLength()
    {
        var world = new GameWorld();
        var handler = new BartZSkillListHandler();

        handler.Handle(new byte[] { 0x02, 0x00, 0x00, 0x00, 0x01 }, world);

        Assert.Empty(world.Skills);
    }

    [Fact]
    public void Handle_SkipsInvalidSkillIdOrLevel()
    {
        var world = new GameWorld();
        var handler = new BartZSkillListHandler();

        handler.Handle(BuildPayload(
            CreateEntry(0, 0, 29, 0),
            CreateEntry(0, 1, 0, 0),
            CreateEntry(0, 2, 120, 0)), world);

        Assert.Single(world.Skills);
        Assert.True(world.Skills.ContainsKey(120));
    }

    private static byte[] BuildPayload(params byte[][] entries)
    {
        var payload = new byte[4 + (entries.Length * 13)];
        BitConverter.GetBytes(entries.Length).CopyTo(payload, 0);
        for (int i = 0; i < entries.Length; i++)
        {
            Buffer.BlockCopy(entries[i], 0, payload, 4 + (i * 13), 13);
        }

        return payload;
    }

    private static byte[] CreateEntry(int passiveFlag, int level, int skillId, byte tail)
    {
        var entry = new byte[13];
        BitConverter.GetBytes(passiveFlag).CopyTo(entry, 0);
        BitConverter.GetBytes(level).CopyTo(entry, 4);
        BitConverter.GetBytes(skillId).CopyTo(entry, 8);
        entry[12] = tail;
        return entry;
    }
}
