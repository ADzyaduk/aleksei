using Alexei.Core.Diagnostics;
using Xunit;

namespace Alexei.Core.Tests.Diagnostics;

public sealed class PacketEvidenceCollectorSkillListTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "alexei-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Flush_MarksSkillListDelivered_OnlyForCompactConfirmedBartzPacket()
    {
        Directory.CreateDirectory(_tempDir);
        var collector = new PacketEvidenceCollector(_tempDir, "bartz", "kamael-like", () => new DateTime(2026, 4, 3, 10, 0, 0, DateTimeKind.Utc));

        collector.Record(new PacketObservation(
            TimestampUtc: new DateTime(2026, 4, 3, 10, 0, 1, DateTimeKind.Utc),
            Direction: PacketDirection.S2C,
            Source: "server",
            WireOpcode: 0x11,
            ResolvedOpcode: 0x0B,
            PayloadLength: 4036,
            HandlerName: "BartZSkillListHandler",
            Classification: "observed",
            Notes: null),
            new byte[] { 0x00, 0x00, 0x38, 0x00 });

        collector.Record(new PacketObservation(
            TimestampUtc: new DateTime(2026, 4, 3, 10, 0, 2, DateTimeKind.Utc),
            Direction: PacketDirection.S2C,
            Source: "server",
            WireOpcode: 0x5F,
            ResolvedOpcode: 0x45,
            PayloadLength: 238,
            HandlerName: "BartZSkillListHandler",
            Classification: "observed",
            Notes: null),
            BuildCompactSkillListPayload());

        collector.FlushReport();

        string groundTruth = File.ReadAllText(Path.Combine(_tempDir, "bartz-ground-truth.md"));
        Assert.Contains("Scenario: `skill-list-delivered`", groundTruth);
        Assert.Contains("| 10:00:02.000 | S2C | server | 0x5F | 0x45 | BartZSkillListHandler | 238 |", groundTruth);
        Assert.DoesNotContain("| 10:00:01.000 | S2C | server | 0x11 | 0x0B | BartZSkillListHandler | 4036 |", groundTruth);
    }

    [Fact]
    public void Record_WritesSkillListDump_ForConfirmedCompactPacket()
    {
        Directory.CreateDirectory(_tempDir);
        var collector = new PacketEvidenceCollector(_tempDir, "bartz", "kamael-like", () => new DateTime(2026, 4, 3, 10, 0, 0, DateTimeKind.Utc));

        collector.Record(new PacketObservation(
            TimestampUtc: new DateTime(2026, 4, 3, 10, 0, 2, DateTimeKind.Utc),
            Direction: PacketDirection.S2C,
            Source: "server",
            WireOpcode: 0x5F,
            ResolvedOpcode: 0x45,
            PayloadLength: 238,
            HandlerName: "BartZSkillListHandler",
            Classification: "observed",
            Notes: null),
            BuildCompactSkillListPayload());

        string dump = File.ReadAllText(Path.Combine(_tempDir, "bartz-skilllist-rows.md"));
        Assert.Contains("# Bartz SkillList Rows", dump);
        Assert.Contains("Count: 18", dump);
        Assert.Contains("| 0 | 0 | 7 | 29 |", dump);
        Assert.Contains("| 1 | 0 | 1 | 83 |", dump);
        Assert.Contains("| 3 | 1 | 3 | 120 |", dump);
    }

    private static byte[] BuildCompactSkillListPayload()
    {
        byte[] payload = new byte[238];
        BitConverter.GetBytes(18).CopyTo(payload, 0);

        WriteEntry(payload, 0, 0, 7, 29);
        WriteEntry(payload, 1, 0, 1, 83);
        WriteEntry(payload, 2, 0, 1, 95);
        WriteEntry(payload, 3, 1, 3, 120);

        for (int i = 4; i < 18; i++)
            WriteEntry(payload, i, 0, 1, 1000 + i);

        return payload;
    }

    private static void WriteEntry(byte[] payload, int index, int passiveFlag, int level, int skillId)
    {
        int offset = 4 + (index * 13);
        BitConverter.GetBytes(passiveFlag).CopyTo(payload, offset + 0);
        BitConverter.GetBytes(level).CopyTo(payload, offset + 4);
        BitConverter.GetBytes(skillId).CopyTo(payload, offset + 8);
        payload[offset + 12] = 0;
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
