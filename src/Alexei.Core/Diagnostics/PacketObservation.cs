namespace Alexei.Core.Diagnostics;

public sealed record PacketObservation(
    DateTime TimestampUtc,
    PacketDirection Direction,
    string Source,
    byte WireOpcode,
    byte? ResolvedOpcode,
    int PayloadLength,
    string? HandlerName,
    string Classification,
    string? Notes);
