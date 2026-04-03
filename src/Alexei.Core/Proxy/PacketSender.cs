using Alexei.Core.Crypto;
using Alexei.Core.Diagnostics;
using System.Net.Sockets;

namespace Alexei.Core.Proxy;

/// <summary>
/// Encrypts and sends C→S packets to the game server.
/// Thread-safe via shared SemaphoreSlim — shared between relay and bot injection.
/// No checksum for game C→S packets (Teon/Interlude).
/// </summary>
public sealed class PacketSender
{
    private readonly NetworkStream _serverStream;
    private readonly L2GameCrypt _xor;
    private readonly SemaphoreSlim _lock;
    private readonly PacketEvidenceCollector? _collector;

    public event Action<byte, int>? PacketSent;

    public PacketSender(NetworkStream serverStream, L2GameCrypt xor, SemaphoreSlim sharedLock, PacketEvidenceCollector? collector = null)
    {
        _serverStream = serverStream;
        _xor = xor;
        _lock = sharedLock;
        _collector = collector;
    }

    /// <summary>
    /// Send a packet to the game server (opcode + payload).
    /// XOR encrypt → frame → send. No checksum, no Blowfish.
    /// Lock covers both encrypt and send to keep XOR counter in sync with relay.
    /// </summary>
    public async Task SendAsync(byte opcode, byte[] payload, CancellationToken ct = default)
    {
        // Build body: [opcode] [payload]
        var body = new byte[1 + payload.Length];
        body[0] = opcode;
        if (payload.Length > 0)
            Buffer.BlockCopy(payload, 0, body, 1, payload.Length);

        await _lock.WaitAsync(ct);
        try
        {
            // XOR encrypt INSIDE the lock (shares counter state with relay)
            _xor.Encrypt(body, 0, body.Length);

            // Frame: [uint16 LE total_length] [body]
            int totalLen = body.Length + 2;
            var wire = new byte[totalLen];
            BitConverter.GetBytes((ushort)totalLen).CopyTo(wire, 0);
            Buffer.BlockCopy(body, 0, wire, 2, body.Length);

            await _serverStream.WriteAsync(wire, ct);
            await _serverStream.FlushAsync(ct);
        }
        finally
        {
            _lock.Release();
        }

        _collector?.Record(
            new PacketObservation(
                TimestampUtc: DateTime.UtcNow,
                Direction: PacketDirection.C2S,
                Source: "bot",
                WireOpcode: opcode,
                ResolvedOpcode: opcode,
                PayloadLength: payload.Length,
                HandlerName: PacketIntentClassifier.DescribeOutgoing(opcode, payload.Length),
                Classification: "observed",
                Notes: "bot-injected"),
            payload);

        PacketSent?.Invoke(opcode, payload.Length);
    }

    public async Task SendAsync((byte opcode, byte[] payload) packet, CancellationToken ct = default)
    {
        await SendAsync(packet.opcode, packet.payload, ct);
    }
}
