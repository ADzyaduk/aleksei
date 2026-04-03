using Alexei.Core.Data;
using System.Text;

namespace Alexei.Core.Diagnostics;

public sealed class PacketEvidenceCollector
{
    private sealed record InvestigationEvent(DateTime TimestampUtc, string Type, string Label, string? Details);

    private sealed record ScenarioSlice(
        string Label,
        DateTime TimestampUtc,
        DateTime EndUtc,
        string? Details,
        List<(PacketObservation Observation, string Hex)> Packets,
        List<InvestigationEvent> Behavior,
        string ScenarioMode,
        bool ScenarioContaminated);

    private readonly string _rootPath;
    private readonly string _serverId;
    private readonly string _chronicle;
    private readonly Func<DateTime> _nowUtc;
    private readonly object _gate = new();
    private readonly List<(PacketObservation Observation, string Hex)> _entries = new();
    private readonly List<InvestigationEvent> _events = new();
    private readonly Dictionary<string, DateTime> _lastScenarioMarkers = new(StringComparer.OrdinalIgnoreCase);
    private int _captureIndex;

    public PacketEvidenceCollector(string rootPath, string serverId, string chronicle, Func<DateTime>? nowUtc = null)
    {
        _rootPath = rootPath;
        _serverId = serverId;
        _chronicle = chronicle;
        _nowUtc = nowUtc ?? (() => DateTime.UtcNow);
    }

    public void Record(PacketObservation observation, ReadOnlySpan<byte> payload)
    {
        byte[] payloadBytes = payload.ToArray();
        string dashedHex = ToDashedHex(Convert.ToHexString(payloadBytes));

        lock (_gate)
        {
            _entries.Add((observation, dashedHex));
            WriteCaptureFile(observation, dashedHex, ++_captureIndex);
            if (IsConfirmedBartzSkillList(observation))
                WriteSkillListDump(observation, payloadBytes);
            TryAutoMarkScenario(observation, dashedHex);
        }
    }

    public void MarkScenario(string label, string? details = null)
    {
        lock (_gate)
        {
            AddScenarioEvent(label, details, _nowUtc());
        }
    }

    public void RecordBehavior(string subsystem, string message)
    {
        lock (_gate)
        {
            _events.Add(new InvestigationEvent(_nowUtc(), "behavior", subsystem, message));
        }
    }

    public string FlushReport()
    {
        List<(PacketObservation Observation, string Hex)> snapshot;
        List<InvestigationEvent> events;
        lock (_gate)
        {
            snapshot = new List<(PacketObservation, string)>(_entries);
            events = new List<InvestigationEvent>(_events);
        }

        var scenarios = BuildScenarioSlices(snapshot, events);

        Directory.CreateDirectory(_rootPath);
        string reportPath = Path.Combine(_rootPath, "bartz-packet-investigation.md");
        File.WriteAllText(reportPath, BuildMarkdown(snapshot, events, scenarios));

        string groundTruthPath = Path.Combine(_rootPath, "bartz-ground-truth.md");
        File.WriteAllText(groundTruthPath, BuildGroundTruthMarkdown(scenarios));

        string timelinePath = Path.Combine(_rootPath, "bartz-scenario-timeline.log");
        File.WriteAllLines(timelinePath, BuildTimelineLines(events, scenarios));

        return reportPath;
    }

    private void WriteCaptureFile(PacketObservation observation, string dashedHex, int index)
    {
        string capturesDir = Path.Combine(_rootPath, "captures");
        Directory.CreateDirectory(capturesDir);

        string direction = observation.Direction.ToString().ToLowerInvariant();
        string fileName =
            $"{index:D4}-{direction}-wire-{observation.WireOpcode:X2}-resolved-{(observation.ResolvedOpcode?.ToString("X2") ?? "NA")}-len-{observation.PayloadLength}.hex";
        string path = Path.Combine(capturesDir, fileName);

        var lines = new[]
        {
            $"# server={_serverId}",
            $"# chronicle={_chronicle}",
            $"# timestamp_utc={observation.TimestampUtc:O}",
            $"# direction={direction}",
            $"# source={observation.Source}",
            $"# wire=0x{observation.WireOpcode:X2}",
            $"# resolved={(observation.ResolvedOpcode.HasValue ? $"0x{observation.ResolvedOpcode.Value:X2}" : "n/a")}",
            $"# length={observation.PayloadLength}",
            $"# handler={observation.HandlerName ?? "unknown"}",
            $"# classification={observation.Classification}",
            $"# notes={observation.Notes ?? string.Empty}",
            dashedHex
        };

        File.WriteAllLines(path, lines);
    }

    private void WriteSkillListDump(PacketObservation observation, byte[] payload)
    {
        const int headerSize = 4;
        const int entrySize = 13;
        if (payload.Length < headerSize)
            return;

        int count = BitConverter.ToInt32(payload, 0);
        if (count <= 0 || count > 512)
            return;

        string path = Path.Combine(_rootPath, "bartz-skilllist-rows.md");
        var sb = new StringBuilder();
        sb.AppendLine("# Bartz SkillList Rows");
        sb.AppendLine();
        sb.AppendLine($"Generated: {_nowUtc():O}");
        sb.AppendLine($"Timestamp: {observation.TimestampUtc:O}");
        sb.AppendLine($"Wire: 0x{observation.WireOpcode:X2}");
        sb.AppendLine($"Resolved: 0x{observation.ResolvedOpcode:X2}");
        sb.AppendLine($"PayloadLength: {payload.Length}");
        sb.AppendLine($"Count: {count}");
        sb.AppendLine($"EntrySize: {entrySize}");
        sb.AppendLine();
        sb.AppendLine("| Idx | Passive | Level | SkillId | Name | Raw13 |");
        sb.AppendLine("| --- | --- | --- | --- | --- | --- |");

        int offset = headerSize;
        for (int i = 0; i < count; i++, offset += entrySize)
        {
            if (offset + entrySize > payload.Length)
                break;

            int passive = BitConverter.ToInt32(payload, offset + 0);
            int level = BitConverter.ToInt32(payload, offset + 4);
            int skillId = BitConverter.ToInt32(payload, offset + 8);
            string raw13 = BitConverter.ToString(payload, offset, entrySize).Replace('-', ' ');
            sb.AppendLine($"| {i} | {passive} | {level} | {skillId} | {EscapeTable(SkillNames.Get(skillId))} | `{raw13}` |");
        }

        File.WriteAllText(path, sb.ToString());
    }

    private static string EscapeTable(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace("|", "\\|");
    private string BuildMarkdown(
        List<(PacketObservation Observation, string Hex)> snapshot,
        List<InvestigationEvent> events,
        List<ScenarioSlice> scenarios)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Bartz Packet Investigation");
        sb.AppendLine();
        sb.AppendLine($"Generated: {_nowUtc():O}");
        sb.AppendLine();
        sb.AppendLine($"Server: `{_serverId}`");
        sb.AppendLine();
        sb.AppendLine($"Chronicle hypothesis: `{_chronicle}`");
        sb.AppendLine();
        sb.AppendLine($"Captured packets: `{snapshot.Count}`");
        sb.AppendLine();

        AppendScenarioSummary(sb, scenarios);
        AppendMatrix(sb, "S2C Matrix", snapshot.Where(x => x.Observation.Direction == PacketDirection.S2C));
        AppendMatrix(sb, "C2S Matrix", snapshot.Where(x => x.Observation.Direction == PacketDirection.C2S));
        AppendBehaviorSummary(sb, events);

        sb.AppendLine("## Notes");
        foreach (var note in snapshot.Select(x => x.Observation.Notes).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal))
        {
            sb.AppendLine($"- {note}");
        }

        return sb.ToString();
    }

    private string BuildGroundTruthMarkdown(List<ScenarioSlice> scenarios)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Bartz Ground Truth");
        sb.AppendLine();
        sb.AppendLine($"Generated: {_nowUtc():O}");
        sb.AppendLine();
        sb.AppendLine($"Server: `{_serverId}`");
        sb.AppendLine();

        if (scenarios.Count == 0)
        {
            sb.AppendLine("No scenario markers captured yet.");
            sb.AppendLine();
            sb.AppendLine("Run short Bartz scenarios with the live client or bot to populate this artifact.");
            return sb.ToString();
        }

        sb.AppendLine("## Scenario Index");
        sb.AppendLine();
        foreach (var scenario in scenarios)
        {
            sb.AppendLine($"- `{scenario.Label}` at `{scenario.TimestampUtc:O}`{(string.IsNullOrWhiteSpace(scenario.Details) ? string.Empty : $" - {scenario.Details}")} [{scenario.ScenarioMode}]");
        }
        sb.AppendLine();

        foreach (var scenario in scenarios)
        {
            sb.AppendLine($"## Scenario: `{scenario.Label}`");
            sb.AppendLine();
            sb.AppendLine($"Timestamp: `{scenario.TimestampUtc:O}`");
            if (!string.IsNullOrWhiteSpace(scenario.Details))
                sb.AppendLine($"Details: {scenario.Details}");
            sb.AppendLine($"Scenario mode: `{scenario.ScenarioMode}`");
            sb.AppendLine($"Scenario contaminated: `{scenario.ScenarioContaminated.ToString().ToLowerInvariant()}`");
            sb.AppendLine($"Confidence: `{(scenario.Details?.Contains("auto", StringComparison.OrdinalIgnoreCase) == true ? "observed-auto" : "observed")}`");
            sb.AppendLine($"Code impact: {DescribeCodeImpact(scenario.Label)}");
            sb.AppendLine();

            if (scenario.Behavior.Count > 0)
            {
                sb.AppendLine("### Bot Behavior");
                sb.AppendLine();
                foreach (var item in scenario.Behavior)
                    sb.AppendLine($"- `{item.TimestampUtc:HH:mm:ss.fff}` [{item.Label}] {item.Details}");
                sb.AppendLine();
            }

            sb.AppendLine("### Packet Sequence");
            sb.AppendLine();
            sb.AppendLine("| Time | Dir | Source | Wire | Resolved | Handler | Len | Preview | Interpretation |");
            sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- |");
            foreach (var packet in scenario.Packets.Take(40))
            {
                var observation = packet.Observation;
                sb.AppendLine($"| {observation.TimestampUtc:HH:mm:ss.fff} | {observation.Direction} | {observation.Source} | 0x{observation.WireOpcode:X2} | {(observation.ResolvedOpcode.HasValue ? $"0x{observation.ResolvedOpcode.Value:X2}" : "n/a")} | {observation.HandlerName ?? "unknown"} | {observation.PayloadLength} | {Preview(packet.Hex)} | {InterpretScenarioPacket(scenario.Label, observation)} |");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private IEnumerable<string> BuildTimelineLines(List<InvestigationEvent> events, List<ScenarioSlice> scenarios)
    {
        var scenarioIndex = scenarios.ToDictionary(
            keySelector: scenario => (scenario.TimestampUtc, scenario.Label),
            elementSelector: scenario => scenario);

        foreach (var evt in events.OrderBy(x => x.TimestampUtc))
        {
            yield return FormatEventLine(evt);

            if (evt.Type == "scenario" && scenarioIndex.TryGetValue((evt.TimestampUtc, evt.Label), out var scenario))
            {
                yield return $"{evt.TimestampUtc:O} [scenario-mode] {evt.Label} :: mode={scenario.ScenarioMode} contaminated={scenario.ScenarioContaminated.ToString().ToLowerInvariant()}";
            }
        }
    }

    private static void AppendMatrix(StringBuilder sb, string title, IEnumerable<(PacketObservation Observation, string Hex)> rows)
    {
        sb.AppendLine($"## {title}");
        sb.AppendLine();
        sb.AppendLine("| Direction | Wire | Resolved | Length | Handler | Classification | Count | Source |");
        sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");

        foreach (var row in rows.GroupBy(x => new
                 {
                     x.Observation.Direction,
                     x.Observation.WireOpcode,
                     x.Observation.ResolvedOpcode,
                     x.Observation.PayloadLength,
                     Handler = x.Observation.HandlerName ?? "unknown",
                     x.Observation.Classification,
                     x.Observation.Source
                 })
                 .OrderBy(g => g.Key.WireOpcode)
                 .ThenBy(g => g.Key.PayloadLength))
        {
            sb.AppendLine($"| {row.Key.Direction} | 0x{row.Key.WireOpcode:X2} | {(row.Key.ResolvedOpcode.HasValue ? $"0x{row.Key.ResolvedOpcode.Value:X2}" : "n/a")} | {row.Key.PayloadLength} | {row.Key.Handler} | {row.Key.Classification} | {row.Count()} | {row.Key.Source} |");
        }

        sb.AppendLine();
    }

    private static void AppendScenarioSummary(StringBuilder sb, List<ScenarioSlice> scenarios)
    {
        if (scenarios.Count == 0)
            return;

        sb.AppendLine("## Scenario Markers");
        sb.AppendLine();
        foreach (var scenario in scenarios)
        {
            sb.AppendLine($"- `{scenario.TimestampUtc:HH:mm:ss.fff}` `{scenario.Label}` [{scenario.ScenarioMode}] contaminated={scenario.ScenarioContaminated.ToString().ToLowerInvariant()}{(string.IsNullOrWhiteSpace(scenario.Details) ? string.Empty : $" - {scenario.Details}")}");
        }
        sb.AppendLine();
    }

    private static void AppendBehaviorSummary(StringBuilder sb, List<InvestigationEvent> events)
    {
        var behavior = events.Where(x => x.Type == "behavior").OrderBy(x => x.TimestampUtc).TakeLast(30).ToList();
        if (behavior.Count == 0)
            return;

        sb.AppendLine("## Behavior Trace");
        sb.AppendLine();
        foreach (var item in behavior)
            sb.AppendLine($"- `{item.TimestampUtc:HH:mm:ss.fff}` [{item.Label}] {item.Details}");
        sb.AppendLine();
    }

    private void TryAutoMarkScenario(PacketObservation observation, string dashedHex)
    {
        string? label = observation.Direction switch
        {
            PacketDirection.C2S when (observation.Source == "client" || observation.Source == "bot") && observation.WireOpcode == 0x1F => "select-target",
            PacketDirection.C2S when (observation.Source == "client" || observation.Source == "bot") && observation.WireOpcode == 0x59 => "engage-attack",
            PacketDirection.C2S when (observation.Source == "client" || observation.Source == "bot") && observation.WireOpcode == 0x39 => "cast-skill",
            PacketDirection.C2S when (observation.Source == "client" || observation.Source == "bot") && observation.WireOpcode == 0x48 => "pickup-item",
            PacketDirection.S2C when IsConfirmedBartzSkillList(observation) => "skill-list-delivered",
            _ => null
        };

        if (label == null)
            return;

        AddScenarioEvent(label, $"auto marker from {observation.Source} wire=0x{observation.WireOpcode:X2} preview={Preview(dashedHex)}", observation.TimestampUtc);
    }

    private void AddScenarioEvent(string label, string? details, DateTime timestampUtc)
    {
        if (_lastScenarioMarkers.TryGetValue(label, out var last) && timestampUtc < last.AddSeconds(2))
            return;

        _lastScenarioMarkers[label] = timestampUtc;
        _events.Add(new InvestigationEvent(timestampUtc, "scenario", label, details));
    }

    private static List<ScenarioSlice> BuildScenarioSlices(
        List<(PacketObservation Observation, string Hex)> snapshot,
        List<InvestigationEvent> events)
    {
        var scenarioEvents = events.Where(x => x.Type == "scenario").OrderBy(x => x.TimestampUtc).ToList();
        var slices = new List<ScenarioSlice>(scenarioEvents.Count);

        for (int i = 0; i < scenarioEvents.Count; i++)
        {
            var scenario = scenarioEvents[i];
            var endUtc = scenario.TimestampUtc.AddSeconds(5);

            var packets = snapshot.Where(x => x.Observation.TimestampUtc >= scenario.TimestampUtc && x.Observation.TimestampUtc < endUtc)
                .OrderBy(x => x.Observation.TimestampUtc)
                .ToList();

            var behavior = events.Where(x => x.Type == "behavior" && x.TimestampUtc >= scenario.TimestampUtc && x.TimestampUtc < endUtc)
                .OrderBy(x => x.TimestampUtc)
                .ToList();

            bool hasBotCombat = packets.Any(packet => IsCombatActionPacket(packet.Observation, "bot"));
            bool hasClientCombat = packets.Any(packet => IsCombatActionPacket(packet.Observation, "client"));
            string scenarioMode = hasClientCombat ? "mixed-manual"
                : hasBotCombat ? "bot-only"
                : "observational";
            bool scenarioContaminated = hasBotCombat && hasClientCombat;

            slices.Add(new ScenarioSlice(
                scenario.Label,
                scenario.TimestampUtc,
                endUtc,
                scenario.Details,
                packets,
                behavior,
                scenarioMode,
                scenarioContaminated));
        }

        return slices;
    }

    private static bool IsCombatActionPacket(PacketObservation observation, string source) =>
        observation.Direction == PacketDirection.C2S &&
        string.Equals(observation.Source, source, StringComparison.OrdinalIgnoreCase) &&
        (observation.WireOpcode == 0x1F || observation.WireOpcode == 0x39 || observation.WireOpcode == 0x59);

    private static string DescribeCodeImpact(string scenarioLabel) => scenarioLabel switch
    {
        "select-target" => "TargetSelectedHandler, BartzTargetStatusHandler, AutoCombatTask",
        "engage-attack" => "GamePackets.AttackUse59, MoveToPointHandler, StatusUpdateHandler, AutoCombatTask",
        "cast-skill" => "BartZSkillListHandler, GamePackets.UseSkill, SkillCoolTime handling",
        "pickup-item" => "SpawnItemHandler, DeleteObjectHandler, GamePackets.PickupItemShort, AutoLootTask",
        "skill-list-delivered" => "BartZSkillListHandler and skill UI projection",
        _ => "Packet mapping and downstream bot tasks"
    };

    private static string InterpretScenarioPacket(string scenarioLabel, PacketObservation observation) => scenarioLabel switch
    {
        "select-target" when observation.Direction == PacketDirection.C2S && observation.WireOpcode == 0x1F => "client target request",
        "select-target" when observation.Direction == PacketDirection.S2C && observation.ResolvedOpcode == 0x3D => "target state update",
        "select-target" when observation.Direction == PacketDirection.S2C && observation.ResolvedOpcode == 0xA3 => "target companion status",
        "select-target" when observation.Direction == PacketDirection.S2C && observation.ResolvedOpcode == 0x02 => "target vitals update",
        "engage-attack" when observation.Direction == PacketDirection.C2S && observation.WireOpcode == 0x59 => "engage / auto-attack request",
        "engage-attack" when observation.Direction == PacketDirection.S2C && observation.ResolvedOpcode == 0x35 => "server movement / approach",
        "engage-attack" when observation.Direction == PacketDirection.S2C && observation.ResolvedOpcode == 0x02 => "combat hp/mp/cp update",
        "cast-skill" when observation.Direction == PacketDirection.C2S && observation.WireOpcode == 0x39 => "skill cast request",
        "pickup-item" when observation.Direction == PacketDirection.C2S && observation.WireOpcode == 0x48 => "pickup request",
        "pickup-item" when observation.Direction == PacketDirection.S2C && observation.ResolvedOpcode == 0x0C => "item spawn / item update",
        "skill-list-delivered" when observation.Direction == PacketDirection.S2C && IsConfirmedBartzSkillList(observation) => "skill list payload",
        _ => string.Empty
    };

    private static bool IsConfirmedBartzSkillList(PacketObservation observation) =>
        observation.Direction == PacketDirection.S2C &&
        observation.WireOpcode == 0x5F && observation.ResolvedOpcode == 0x45 && string.Equals(observation.HandlerName, "BartZSkillListHandler", StringComparison.Ordinal) && observation.PayloadLength >= 17;

    private static string Preview(string dashedHex)
    {
        if (string.IsNullOrWhiteSpace(dashedHex))
            return string.Empty;

        var parts = dashedHex.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts.Take(8));
    }

    private static string FormatEventLine(InvestigationEvent evt) => $"{evt.TimestampUtc:O} [{evt.Type}] {evt.Label}{(string.IsNullOrWhiteSpace(evt.Details) ? string.Empty : $" :: {evt.Details}")}";

    private static string ToDashedHex(string hex)
    {
        if (string.IsNullOrEmpty(hex))
            return string.Empty;

        var chars = new char[hex.Length + (hex.Length / 2) - 1];
        int pos = 0;
        for (int i = 0; i < hex.Length; i += 2)
        {
            if (i > 0)
                chars[pos++] = '-';
            chars[pos++] = hex[i];
            chars[pos++] = hex[i + 1];
        }

        return new string(chars);
    }
}





