using Microsoft.Extensions.Logging;

namespace Alexei.Core.Protocol;

/// <summary>
/// Detects per-session XOR key on Teon/Elmorelab servers
/// by fingerprinting NpcInfo packets.
/// Two strategies (matching alesha reference):
///   1. Strict: 3+ packets with exactly 187-byte payload
///   2. Range heuristic: 8+ packets in 180-210 range, cross-validated with other opcodes
/// </summary>
public sealed class OpcodeDetector
{
    private const int NpcInfoPayloadSize = 187;
    private const int NpcInfoPayloadMin = 180;
    private const int NpcInfoPayloadMax = 210;
    private const int StrictThreshold = 3;
    private const int RangeThreshold = 8;

    private readonly Dictionary<byte, List<int>> _sizeCounts = new();
    private readonly Dictionary<byte, int> _rangeHits = new();
    private readonly List<(byte opcode, byte[] payload)> _buffer = new();
    private readonly ILogger? _logger;

    public byte XorKey { get; private set; }
    public bool IsDetected { get; private set; }

    public event Action<byte>? Detected;

    public OpcodeDetector(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Feed a decrypted S2C packet. Returns true if opcode detection just completed.
    /// </summary>
    public bool Feed(byte wireOpcode, byte[] payload)
    {
        if (IsDetected) return false;

        _buffer.Add((wireOpcode, payload));

        // Track sizes per opcode
        if (!_sizeCounts.TryGetValue(wireOpcode, out var sizes))
        {
            sizes = new List<int>();
            _sizeCounts[wireOpcode] = sizes;
        }
        sizes.Add(payload.Length);

        // Track range hits for NpcInfo-like packets
        if (payload.Length is >= NpcInfoPayloadMin and <= NpcInfoPayloadMax)
        {
            _rangeHits.TryGetValue(wireOpcode, out int hits);
            _rangeHits[wireOpcode] = hits + 1;
        }

        return TryDetect();
    }

    private bool TryDetect()
    {
        // Strategy 1: strict 187-byte match
        foreach (var (wireOpcode, sizes) in _sizeCounts)
        {
            int count187 = sizes.Count(x => x == NpcInfoPayloadSize);
            if (count187 >= StrictThreshold)
            {
                Finalize(wireOpcode, $"strict-187 (count={count187})");
                return true;
            }
        }

        // Strategy 2: range heuristic with cross-validation
        foreach (var (wireOpcode, rangeCount) in _rangeHits)
        {
            if (rangeCount < RangeThreshold) continue;

            byte candidateKey = (byte)(wireOpcode ^ Opcodes.GameS2C.NpcInfo);

            // High-confidence: 15+ range hits is strong enough without cross-validation
            if (rangeCount >= 15 || LooksPlausibleKey(candidateKey))
            {
                Finalize(wireOpcode, $"range-heuristic (rangeCount={rangeCount})");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Cross-validate a candidate XOR key by checking other known packet patterns.
    /// </summary>
    private bool LooksPlausibleKey(byte key)
    {
        byte moveWire = (byte)(Opcodes.GameS2C.MoveToPoint ^ key);
        byte userInfoWire = (byte)(Opcodes.GameS2C.UserInfo ^ key);
        byte statusWire = (byte)(Opcodes.GameS2C.StatusUpdate ^ key);

        int moveHits = CountSizeRange(moveWire, 24, 28);
        int userInfoHits = CountSizeRange(userInfoWire, 100, int.MaxValue);
        int statusHits = CountSizeRange(statusWire, 8, 96);

        // MoveToPoint only appears when character moves — don't require it
        return moveHits >= 1 || userInfoHits >= 1 || statusHits >= 2;
    }

    private int CountSizeRange(byte opcode, int min, int max)
    {
        if (!_sizeCounts.TryGetValue(opcode, out var sizes)) return 0;
        return sizes.Count(x => x >= min && x <= max);
    }

    private void Finalize(byte npcInfoWireOpcode, string reason)
    {
        XorKey = (byte)(npcInfoWireOpcode ^ Opcodes.GameS2C.NpcInfo);
        IsDetected = true;
        _logger?.LogInformation("Opcode XOR key detected: 0x{Key:X2} ({Reason})", XorKey, reason);
        Detected?.Invoke(XorKey);
    }

    public byte Resolve(byte baseOpcode) => (byte)(baseOpcode ^ XorKey);
    public byte ResolveToBase(byte wireOpcode) => (byte)(wireOpcode ^ XorKey);

    public List<(byte opcode, byte[] payload)> DrainBuffer()
    {
        var copy = new List<(byte, byte[])>(_buffer);
        _buffer.Clear();
        return copy;
    }
}
