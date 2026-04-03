using Alexei.Core.Config;
using Alexei.Core.Crypto;
using Alexei.Core.Diagnostics;
using Alexei.Core.GameState;
using Alexei.Core.Protocol;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;

namespace Alexei.Core.Proxy;

/// <summary>
/// Manages one game session: S2C/C2S relay with cipher management.
///
/// Cipher architecture (Teon — XOR only, no Blowfish on game packets):
///   BlowfishInit (opcode 0x00) is plaintext, initializes XOR ciphers.
///   S→C observation: shadowXorS2C(decrypt)
///   C→S relay:       clientXor(decrypt) → serverXor(encrypt)
///   C→S inject:      serverXor(encrypt) [shared with relay]
/// </summary>
public sealed class ProxySession
{
    private readonly TcpClient _client;
    private readonly TcpClient _server;
    private readonly GameWorld _world;
    private readonly PacketDispatcher _dispatcher;
    private readonly ServerEntry _serverEntry;
    private readonly ILogger? _logger;
    private readonly PacketEvidenceCollector? _collector;

    // Cipher instances — null until BlowfishInit received
    private L2GameCrypt? _shadowXorS2C;
    private L2GameCrypt? _clientXorC2S;
    private L2GameCrypt? _serverXorC2S;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public PacketSender? Sender { get; private set; }

    public event Action<string>? Log;
    public event Action<PacketSender>? SenderReady;

    public ProxySession(TcpClient client, TcpClient server, GameWorld world,
        PacketDispatcher dispatcher, ServerEntry serverEntry, ILogger? logger, PacketEvidenceCollector? collector = null)
    {
        _client = client;
        _server = server;
        _world = world;
        _dispatcher = dispatcher;
        _serverEntry = serverEntry;
        _logger = logger;
        _collector = collector;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var clientStream = _client.GetStream();
        var serverStream = _server.GetStream();

        // Start relay loops — BlowfishInit is handled inline in S2C relay
        var s2cTask = RelayS2CAsync(serverStream, clientStream, ct);
        var c2sTask = RelayC2SAsync(clientStream, serverStream, ct);

        await Task.WhenAny(s2cTask, c2sTask);
    }

    /// <summary>
    /// S→C: forward raw bytes to client, shadow-decrypt a copy for analysis.
    /// BlowfishInit (first packet, opcode 0x00) initializes ciphers inline.
    /// </summary>
    private async Task RelayS2CAsync(NetworkStream serverStream, NetworkStream clientStream, CancellationToken ct)
    {
        int pktCount = 0;
        while (!ct.IsCancellationRequested)
        {
            // Read header (2 bytes)
            var header = new byte[2];
            if (!await ReadExactAsync(serverStream, header, ct)) break;
            int totalLen = BitConverter.ToUInt16(header, 0);
            int bodyLen = totalLen - 2;
            if (bodyLen <= 0) continue;

            // Read body
            var body = new byte[bodyLen];
            if (!await ReadExactAsync(serverStream, body, ct)) break;

            // Forward raw bytes to client unchanged
            await clientStream.WriteAsync(header, ct);
            await clientStream.WriteAsync(body, ct);

            // Process packet
            if (_shadowXorS2C == null)
            {
                // First packet should be BlowfishInit (opcode 0x00, plaintext)
                HandleBlowfishInit(body, serverStream);
                continue;
            }

            // Shadow-decrypt copy for analysis (XOR only — Teon does not use Blowfish for game packets)
            try
            {
                var copy = (byte[])body.Clone();
                _shadowXorS2C.Decrypt(copy, 0, copy.Length);

                byte wireOpcode = copy[0];
                var payload = ExtractPayload(copy);

                pktCount++;
                if (pktCount <= 100)
                    Log?.Invoke($"S2C #{pktCount}: wire=0x{wireOpcode:X2} bodyLen={bodyLen} payloadLen={payload.Length}");

                _dispatcher.Dispatch(wireOpcode, payload);
            }
            catch (Exception ex)
            {
                Log?.Invoke($"S2C decrypt error: {ex.Message}");
                _logger?.LogDebug(ex, "S2C shadow decrypt failed");
            }
        }
    }

    /// <summary>
    /// Handle BlowfishInit packet (opcode 0x00, plaintext).
    /// Extracts dynamic key and initializes XOR ciphers.
    /// </summary>
    private void HandleBlowfishInit(byte[] body, NetworkStream serverStream)
    {
        if (body.Length < 10 || body[0] != _serverEntry.GameKeyOpcode)
        {
            // Log hex dump of first 32 bytes to help diagnose unexpected first packets
            var hex = BitConverter.ToString(body, 0, Math.Min(body.Length, 32)).Replace("-", " ");
            Log?.Invoke($"First game packet: opcode=0x{(body.Length > 0 ? body[0] : 0):X2} len={body.Length} (not BlowfishInit) hex=[{hex}]");
            return;
        }

        // [opcode(1)][rev(1)][dynamic_key(8)][enc_flag(4)][server_id(4)]
        var dynamicKey = new byte[8];
        Buffer.BlockCopy(body, 2, dynamicKey, 0, 8);

        // Build full XOR key: dynamic(8) + static suffix(8)
        var fullKey = L2Blowfish.BuildGameKey(dynamicKey);

        _shadowXorS2C = new L2GameCrypt(fullKey);
        _clientXorC2S = new L2GameCrypt(fullKey);
        _serverXorC2S = new L2GameCrypt(fullKey);

        _shadowXorS2C.Enable();
        _clientXorC2S.Enable();
        _serverXorC2S.Enable();

        Sender = new PacketSender(serverStream, _serverXorC2S, _sendLock, _collector);
        SenderReady?.Invoke(Sender);

        Log?.Invoke($"BlowfishInit: XOR crypto initialized, key={BitConverter.ToString(dynamicKey).Replace("-", " ")}");
        _logger?.LogInformation("Game session cipher initialized");
    }

    /// <summary>
    /// C→S: decrypt with client cipher, re-encrypt with server cipher, forward.
    /// This keeps server cipher state in sync for both relay and injection.
    /// </summary>
    private async Task RelayC2SAsync(NetworkStream clientStream, NetworkStream serverStream, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // Read header
            var header = new byte[2];
            if (!await ReadExactAsync(clientStream, header, ct)) break;
            int totalLen = BitConverter.ToUInt16(header, 0);
            int bodyLen = totalLen - 2;
            if (bodyLen <= 0) continue;

            // Read body
            var body = new byte[bodyLen];
            if (!await ReadExactAsync(clientStream, body, ct)) break;

            await _sendLock.WaitAsync(ct);
            try
            {
                if (_clientXorC2S != null && _serverXorC2S != null)
                {
                    // Decrypt with client XOR, re-encrypt with server XOR
                    _clientXorC2S.Decrypt(body, 0, body.Length);
                    RecordClientPacket(body);
                    _serverXorC2S.Encrypt(body, 0, body.Length);
                }

                // Forward to server (raw header + possibly re-encrypted body)
                await serverStream.WriteAsync(header, ct);
                await serverStream.WriteAsync(body, ct);
            }
            finally
            {
                _sendLock.Release();
            }
        }
    }

    /// <summary>
    /// Extract payload from decrypted packet, stripping opcode and checksum.
    /// Checksum is XOR of 4-byte DWORDs over the FULL body (including opcode).
    /// </summary>
    private static byte[] ExtractPayload(byte[] plain)
    {
        if (plain.Length <= 1) return Array.Empty<byte>();

        // Scan full body (including opcode) for checksum boundary
        int n = plain.Length;
        if (n >= 8)
        {
            int max = (n / 4) * 4;
            for (int size = 8; size <= max; size += 4)
            {
                if (!VerifyChecksum(plain.AsSpan(0, size))) continue;

                bool tailAllZero = true;
                for (int i = size; i < n; i++)
                {
                    if (plain[i] != 0) { tailAllZero = false; break; }
                }

                if (tailAllZero)
                {
                    // payload = everything between opcode and checksum
                    int payloadLen = Math.Max(0, size - 4 - 1);
                    return plain.AsSpan(1, payloadLen).ToArray();
                }
            }
        }

        // No checksum found — return everything after opcode
        return plain.AsSpan(1).ToArray();
    }

    private static bool VerifyChecksum(ReadOnlySpan<byte> body)
    {
        if (body.Length <= 4 || (body.Length & 3) != 0) return false;
        uint checksum = 0;
        for (int i = 0; i < body.Length - 4; i += 4)
            checksum ^= BitConverter.ToUInt32(body.Slice(i, 4));
        uint stored = BitConverter.ToUInt32(body.Slice(body.Length - 4, 4));
        return checksum == stored;
    }

    private void RecordClientPacket(byte[] plainBody)
    {
        if (_collector == null || plainBody.Length == 0)
            return;

        byte opcode = plainBody[0];
        byte[] payload = plainBody.AsSpan(1).ToArray();

        _collector.Record(
            new PacketObservation(
                TimestampUtc: DateTime.UtcNow,
                Direction: PacketDirection.C2S,
                Source: "client",
                WireOpcode: opcode,
                ResolvedOpcode: opcode,
                PayloadLength: payload.Length,
                HandlerName: PacketIntentClassifier.DescribeOutgoing(opcode, payload.Length),
                Classification: "observed",
                Notes: "live-client"),
            payload);
    }

    private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken ct)
    {
        int read = 0;
        while (read < buffer.Length)
        {
            int n = await stream.ReadAsync(buffer.AsMemory(read, buffer.Length - read), ct);
            if (n == 0) return false;
            read += n;
        }
        return true;
    }
}
