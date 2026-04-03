using Alexei.Core.GameState;
using Alexei.Core.Diagnostics;
using Alexei.Core.Protocol.Handlers;
using Microsoft.Extensions.Logging;

namespace Alexei.Core.Protocol;

/// <summary>
/// Routes decrypted S2C packets to registered handlers.
/// Supports re-keying when OpcodeDetector finds the XOR key.
/// Before detection: dispatches by base opcode (fallback for key=0x00).
/// After detection: dispatches by resolved opcode.
/// </summary>
public sealed class PacketDispatcher
{
    private Dictionary<byte, IPacketHandler> _handlers = new();
    // Stores (handler, registeredOpcode) — opcode may differ from handler.BaseOpcode on non-Teon servers
    private readonly List<(IPacketHandler handler, byte opcode)> _allHandlers = new();
    private readonly OpcodeDetector _detector;
    private readonly GameWorld _world;
    private readonly ILogger? _logger;
    private bool _opcodeScramble;
    private int _unknownLogCount;

    public event Action<byte, byte[], string?>? PacketReceived;
    public event Action<PacketObservation, byte[]>? PacketObserved;

    public PacketDispatcher(OpcodeDetector detector, GameWorld world, bool opcodeScramble = true, ILogger? logger = null)
    {
        _detector = detector;
        _world = world;
        _opcodeScramble = opcodeScramble;
        _logger = logger;

        _detector.Detected += OnOpcodeDetected;
    }

    /// <summary>Register handler using its own BaseOpcode.</summary>
    public void Register(IPacketHandler handler) => Register(handler, handler.BaseOpcode);

    /// <summary>Register handler with an explicit base opcode (for servers with different opcode tables).</summary>
    public void Register(IPacketHandler handler, byte opcode)
    {
        _allHandlers.Add((handler, opcode));
        _handlers[opcode] = handler;
    }

    /// <summary>
    /// Dispatch a decrypted S2C packet.
    /// </summary>
    public void Dispatch(byte wireOpcode, byte[] payload)
    {
        if (_opcodeScramble && !_detector.IsDetected)
        {
            bool justDetected = _detector.Feed(wireOpcode, payload);
            if (justDetected)
            {
                // Replay buffered packets with resolved opcodes
                var buffered = _detector.DrainBuffer();
                foreach (var (op, pl) in buffered)
                    DispatchResolved(op, pl);
                return;
            }

            // Before detection: try dispatching by wire opcode directly
            // (works when XOR key is 0x00, which is common on Teon)
            DispatchFallback(wireOpcode, payload);
            return;
        }

        DispatchResolved(wireOpcode, payload);
    }

    /// <summary>
    /// Dispatch using resolved base opcode (after detection or without scramble).
    /// </summary>
    private void DispatchResolved(byte wireOpcode, byte[] payload)
    {
        byte baseOpcode = _opcodeScramble ? _detector.ResolveToBase(wireOpcode) : wireOpcode;

        string? handlerName = null;
        string classification = "unknown";
        string? notes = null;
        if (_handlers.TryGetValue(baseOpcode, out var handler))
        {
            handlerName = handler.GetType().Name;
            classification = "observed";
            try
            {
                handler.Handle(payload, _world);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Handler {Handler} failed for opcode 0x{Opcode:X2}", handlerName, baseOpcode);
            }
        }
        else
        {
            // Log first 30 unknown packets with hex to help identify Bartz opcodes
            if (_unknownLogCount < 30)
            {
                _unknownLogCount++;
                var hex = BitConverter.ToString(payload, 0, Math.Min(payload.Length, 8)).Replace("-", " ");
                _logger?.LogDebug("Unknown 0x{Op:X2} wire=0x{Wire:X2} len={Len} hex=[{Hex}]",
                    baseOpcode, wireOpcode, payload.Length, hex);
                notes = $"preview=[{hex}]";
                EmitObservation(wireOpcode, baseOpcode, payload, handlerName, classification, notes);
                PacketReceived?.Invoke(baseOpcode, payload, $"?hex=[{hex}]");
                return;
            }
        }

        EmitObservation(wireOpcode, baseOpcode, payload, handlerName, classification, notes);
        PacketReceived?.Invoke(baseOpcode, payload, handlerName);
    }

    /// <summary>
    /// Fallback dispatch before detection: try wire opcode directly as base opcode.
    /// If key=0x00 (common), wire == base and handlers match.
    /// </summary>
    private void DispatchFallback(byte wireOpcode, byte[] payload)
    {
        string? handlerName = null;
        string classification = "pre-detect";
        if (_handlers.TryGetValue(wireOpcode, out var handler))
        {
            handlerName = handler.GetType().Name;
            try
            {
                handler.Handle(payload, _world);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Fallback handler {Handler} failed for wire 0x{Opcode:X2}", handlerName, wireOpcode);
            }
        }

        EmitObservation(wireOpcode, handlerName != null ? wireOpcode : null, payload, handlerName, classification, "fallback-dispatch");
        PacketReceived?.Invoke(wireOpcode, payload, handlerName);
    }

    private void EmitObservation(byte wireOpcode, byte? resolvedOpcode, byte[] payload, string? handlerName, string classification, string? notes)
    {
        PacketObserved?.Invoke(
            new PacketObservation(
                TimestampUtc: DateTime.UtcNow,
                Direction: PacketDirection.S2C,
                Source: "server",
                WireOpcode: wireOpcode,
                ResolvedOpcode: resolvedOpcode,
                PayloadLength: payload.Length,
                HandlerName: handlerName,
                Classification: classification,
                Notes: notes),
            (byte[])payload.Clone());
    }

    private void OnOpcodeDetected(byte xorKey)
    {
        _handlers.Clear();
        foreach (var (h, op) in _allHandlers)
            _handlers[op] = h;
        _logger?.LogInformation("PacketDispatcher re-keyed with XOR 0x{Key:X2}, {Count} handlers", xorKey, _handlers.Count);
    }

    public void SetOpcodeScramble(bool enabled)
    {
        _opcodeScramble = enabled;
        if (!enabled)
        {
            _handlers.Clear();
            foreach (var (h, op) in _allHandlers)
                _handlers[op] = h;
        }
    }
}
