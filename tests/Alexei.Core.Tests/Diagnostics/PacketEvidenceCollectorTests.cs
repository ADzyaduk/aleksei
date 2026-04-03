using Alexei.Core.Diagnostics;
using Xunit;

namespace Alexei.Core.Tests.Diagnostics;

public sealed class PacketEvidenceCollectorTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "alexei-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Flush_WritesMarkdownMatrix_WithGroupedS2CAndC2SRows()
    {
        Directory.CreateDirectory(_tempDir);
        var collector = new PacketEvidenceCollector(_tempDir, "bartz", "kamael-like", () => new DateTime(2026, 4, 3, 10, 0, 0, DateTimeKind.Utc));

        collector.Record(new PacketObservation(
            TimestampUtc: new DateTime(2026, 4, 3, 10, 0, 0, DateTimeKind.Utc),
            Direction: PacketDirection.S2C,
            Source: "server",
            WireOpcode: 0x85,
            ResolvedOpcode: 0x9F,
            PayloadLength: 62,
            HandlerName: null,
            Classification: "unknown",
            Notes: "Repeated after re-key"),
            new byte[] { 0x06, 0x00, 0xE2, 0x10 });

        collector.Record(new PacketObservation(
            TimestampUtc: new DateTime(2026, 4, 3, 10, 0, 1, DateTimeKind.Utc),
            Direction: PacketDirection.C2S,
            Source: "bot",
            WireOpcode: 0x39,
            ResolvedOpcode: 0x39,
            PayloadLength: 9,
            HandlerName: "UseSkill:dcb",
            Classification: "confirmed",
            Notes: "Combat skill packet"),
            new byte[] { 0x2F, 0x00, 0x00, 0x00 });

        string reportPath = collector.FlushReport();
        string markdown = File.ReadAllText(reportPath);

        Assert.Contains("# Bartz Packet Investigation", markdown);
        Assert.Contains("| S2C | 0x85 | 0x9F | 62 | unknown |", markdown);
        Assert.Contains("| C2S | 0x39 | 0x39 | 9 | UseSkill:dcb |", markdown);
        Assert.Contains("kamael-like", markdown);
    }

    [Fact]
    public void Record_WritesHexCaptureFiles_WithStableMetadata()
    {
        Directory.CreateDirectory(_tempDir);
        var collector = new PacketEvidenceCollector(_tempDir, "bartz", "kamael-like", () => new DateTime(2026, 4, 3, 10, 0, 0, DateTimeKind.Utc));

        collector.Record(new PacketObservation(
            TimestampUtc: new DateTime(2026, 4, 3, 10, 0, 0, DateTimeKind.Utc),
            Direction: PacketDirection.S2C,
            Source: "server",
            WireOpcode: 0x32,
            ResolvedOpcode: 0x28,
            PayloadLength: 703,
            HandlerName: null,
            Classification: "unknown",
            Notes: "Npc candidate"),
            new byte[] { 0x79, 0x01, 0xFF, 0xFF, 0xC2, 0x8A });

        collector.FlushReport();

        string capturesDir = Path.Combine(_tempDir, "captures");
        string capturePath = Directory.GetFiles(capturesDir, "*.hex", SearchOption.TopDirectoryOnly).Single();
        string capture = File.ReadAllText(capturePath);

        Assert.Contains("direction=s2c", capture, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("wire=0x32", capture, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("resolved=0x28", capture, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("79-01-FF-FF-C2-8A", capture);
    }

    [Fact]
    public void Flush_WritesGroundTruthAndTimeline_WithScenarioMarkersAndBehavior()
    {
        Directory.CreateDirectory(_tempDir);
        var collector = new PacketEvidenceCollector(_tempDir, "bartz", "kamael-like", () => new DateTime(2026, 4, 3, 10, 0, 0, DateTimeKind.Utc));

        collector.MarkScenario("select-target", "manual test scenario");
        collector.RecordBehavior("AutoCombat", "picked target=1209037226");
        collector.Record(new PacketObservation(
            TimestampUtc: new DateTime(2026, 4, 3, 10, 0, 1, DateTimeKind.Utc),
            Direction: PacketDirection.C2S,
            Source: "client",
            WireOpcode: 0x1F,
            ResolvedOpcode: 0x1F,
            PayloadLength: 17,
            HandlerName: "TargetEnter",
            Classification: "observed",
            Notes: "live-client"),
            new byte[] { 0xAA, 0x71, 0x10, 0x48, 0xDE, 0xFD, 0xFE, 0xFF });

        collector.FlushReport();

        string groundTruthPath = Path.Combine(_tempDir, "bartz-ground-truth.md");
        string timelinePath = Path.Combine(_tempDir, "bartz-scenario-timeline.log");
        string groundTruth = File.ReadAllText(groundTruthPath);
        string timeline = File.ReadAllText(timelinePath);

        Assert.Contains("# Bartz Ground Truth", groundTruth);
        Assert.Contains("Scenario: `select-target`", groundTruth);
        Assert.Contains("TargetEnter", groundTruth);
        Assert.Contains("AutoCombat", groundTruth);
        Assert.Contains("[scenario] select-target", timeline);
        Assert.Contains("[behavior] AutoCombat", timeline);
    }

    [Fact]
    public void Record_AutoMarksScenario_ForBotInjectedCombatPackets()
    {
        Directory.CreateDirectory(_tempDir);
        var collector = new PacketEvidenceCollector(_tempDir, "bartz", "kamael-like", () => new DateTime(2026, 4, 3, 10, 0, 0, DateTimeKind.Utc));

        collector.Record(new PacketObservation(
            TimestampUtc: new DateTime(2026, 4, 3, 10, 0, 1, DateTimeKind.Utc),
            Direction: PacketDirection.C2S,
            Source: "bot",
            WireOpcode: 0x59,
            ResolvedOpcode: 0x59,
            PayloadLength: 20,
            HandlerName: "RequestAttackUse59",
            Classification: "observed",
            Notes: "bot-injected"),
            new byte[] { 0x01, 0x00, 0x00, 0x00 });

        collector.FlushReport();

        string groundTruthPath = Path.Combine(_tempDir, "bartz-ground-truth.md");
        string timelinePath = Path.Combine(_tempDir, "bartz-scenario-timeline.log");
        string groundTruth = File.ReadAllText(groundTruthPath);
        string timeline = File.ReadAllText(timelinePath);

        Assert.Contains("engage-attack", groundTruth);
        Assert.Contains("[scenario] engage-attack", timeline);
    }

    [Fact]
    public void Flush_MarksBotOnlyScenarioMode_ForUncontaminatedBotCombatWindow()
    {
        Directory.CreateDirectory(_tempDir);
        var collector = new PacketEvidenceCollector(_tempDir, "bartz", "kamael-like", () => new DateTime(2026, 4, 3, 10, 0, 0, DateTimeKind.Utc));

        collector.Record(new PacketObservation(
            TimestampUtc: new DateTime(2026, 4, 3, 10, 0, 1, DateTimeKind.Utc),
            Direction: PacketDirection.C2S,
            Source: "bot",
            WireOpcode: 0x1F,
            ResolvedOpcode: 0x1F,
            PayloadLength: 17,
            HandlerName: "TargetEnter",
            Classification: "observed",
            Notes: "bot-only"),
            new byte[] { 0xAA, 0x71, 0x10, 0x48 });

        collector.Record(new PacketObservation(
            TimestampUtc: new DateTime(2026, 4, 3, 10, 0, 2, DateTimeKind.Utc),
            Direction: PacketDirection.C2S,
            Source: "bot",
            WireOpcode: 0x59,
            ResolvedOpcode: 0x59,
            PayloadLength: 20,
            HandlerName: "RequestAttackUse59",
            Classification: "observed",
            Notes: "bot-only"),
            new byte[] { 0x01, 0x00, 0x00, 0x00 });

        collector.FlushReport();

        string groundTruth = File.ReadAllText(Path.Combine(_tempDir, "bartz-ground-truth.md"));
        string timeline = File.ReadAllText(Path.Combine(_tempDir, "bartz-scenario-timeline.log"));

        Assert.Contains("Scenario mode: `bot-only`", groundTruth);
        Assert.Contains("Scenario contaminated: `false`", groundTruth);
        Assert.Contains("[scenario-mode] engage-attack :: mode=bot-only contaminated=false", timeline);
    }

    [Fact]
    public void Flush_MarksMixedManualAndContaminated_WhenClientCombatPacketsAppearInsideBotWindow()
    {
        Directory.CreateDirectory(_tempDir);
        var collector = new PacketEvidenceCollector(_tempDir, "bartz", "kamael-like", () => new DateTime(2026, 4, 3, 10, 0, 0, DateTimeKind.Utc));

        collector.Record(new PacketObservation(
            TimestampUtc: new DateTime(2026, 4, 3, 10, 0, 1, DateTimeKind.Utc),
            Direction: PacketDirection.C2S,
            Source: "bot",
            WireOpcode: 0x59,
            ResolvedOpcode: 0x59,
            PayloadLength: 20,
            HandlerName: "RequestAttackUse59",
            Classification: "observed",
            Notes: "bot-only"),
            new byte[] { 0x01, 0x00, 0x00, 0x00 });

        collector.Record(new PacketObservation(
            TimestampUtc: new DateTime(2026, 4, 3, 10, 0, 2, DateTimeKind.Utc),
            Direction: PacketDirection.C2S,
            Source: "client",
            WireOpcode: 0x39,
            ResolvedOpcode: 0x39,
            PayloadLength: 9,
            HandlerName: "RequestMagicSkillUse",
            Classification: "observed",
            Notes: "manual contamination"),
            new byte[] { 0x01, 0x00, 0x00, 0x00 });

        collector.FlushReport();

        string groundTruth = File.ReadAllText(Path.Combine(_tempDir, "bartz-ground-truth.md"));
        string timeline = File.ReadAllText(Path.Combine(_tempDir, "bartz-scenario-timeline.log"));

        Assert.Contains("Scenario mode: `mixed-manual`", groundTruth);
        Assert.Contains("Scenario contaminated: `true`", groundTruth);
        Assert.Contains("[scenario-mode] engage-attack :: mode=mixed-manual contaminated=true", timeline);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
