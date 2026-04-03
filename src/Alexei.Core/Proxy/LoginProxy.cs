using Alexei.Core.Crypto;
using Alexei.Core.Protocol;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;

namespace Alexei.Core.Proxy;

/// <summary>
/// Login MITM proxy. Listens on localhost, relays to real login server.
/// Patches ServerList to redirect client to local GameProxy.
/// Discovers real game server endpoint from ServerList and notifies via event.
/// </summary>
public sealed class LoginProxy
{
    private readonly string _realHost;
    private readonly int _realPort;
    private readonly int _listenPort;
    private readonly int _gameProxyPort;
    private readonly ILogger? _logger;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    public int PlayOk1 { get; private set; }
    public int PlayOk2 { get; private set; }
    public int LoginOk1 { get; private set; }
    public int LoginOk2 { get; private set; }

    public event Action? ClientConnected;
    public event Action? ClientDisconnected;
    public event Action<string>? Log;

    /// <summary>
    /// Fired when real game server endpoint is discovered from ServerList.
    /// Parameters: (host, port).
    /// </summary>
    public event Action<string, int>? GameServerDiscovered;

    public LoginProxy(string realHost, int realPort, int listenPort, int gameProxyPort, ILogger? logger = null)
    {
        _realHost = realHost;
        _realPort = realPort;
        _listenPort = listenPort;
        _gameProxyPort = gameProxyPort;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _listener = new TcpListener(IPAddress.Loopback, _listenPort);
        _listener.Start();
        _logger?.LogInformation("LoginProxy listening on :{Port}", _listenPort);
        Log?.Invoke($"LoginProxy listening on :{_listenPort}");

        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                _ = HandleClientAsync(client, _cts.Token);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            _listener.Stop();
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        ClientConnected?.Invoke();
        Log?.Invoke("Login client connected");

        using var server = new TcpClient();
        try
        {
            await server.ConnectAsync(_realHost, _realPort, ct);
            using var clientStream = client.GetStream();
            using var serverStream = server.GetStream();

            L2Blowfish? blowfish = null;

            // Relay loop — read header+body, process S2C inline, forward unchanged bytes
            var s2cTask = RelayS2CAsync(serverStream, clientStream, () => blowfish, bf => blowfish = bf, ct);
            var c2sTask = RelayRawAsync(clientStream, serverStream, ct);

            await Task.WhenAny(s2cTask, c2sTask);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogError(ex, "Login session error");
            Log?.Invoke($"Login error: {ex.Message}");
        }
        finally
        {
            client.Close();
            ClientDisconnected?.Invoke();
            Log?.Invoke("Login client disconnected");
        }
    }

    private async Task RelayS2CAsync(NetworkStream from, NetworkStream to,
        Func<L2Blowfish?> getBf, Action<L2Blowfish> setBf, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // Read header (2 bytes)
            var header = new byte[2];
            if (!await ReadExactAsync(from, header, ct)) break;
            int totalLen = BitConverter.ToUInt16(header, 0);
            int bodyLen = totalLen - 2;
            if (bodyLen <= 0) continue;

            // Read body
            var body = new byte[bodyLen];
            if (!await ReadExactAsync(from, body, ct)) break;

            var blowfish = getBf();

            if (blowfish == null)
            {
                // First packet should be Init (plaintext)
                if (body.Length > 0 && body[0] == Opcodes.LoginS2C.Init && body.Length >= 1 + 4 + 4 + 128 + 16 + 16)
                {
                    // BF key at offset 1+4+4+128+16 = 153, length 16
                    int keyStart = 1 + 4 + 4 + 128 + 16;
                    var bfKey = new byte[16];
                    Buffer.BlockCopy(body, keyStart, bfKey, 0, 16);
                    setBf(new L2Blowfish(bfKey));
                    Log?.Invoke($"Login Init: opcode=0x{body[0]:X2} len={body.Length}, BF key extracted");
                }
                else
                {
                    Log?.Invoke($"Login Init: opcode=0x{(body.Length > 0 ? body[0] : 0):X2} len={body.Length} (unexpected)");
                }

                // Forward unchanged
                await to.WriteAsync(header, ct);
                await to.WriteAsync(body, ct);
                continue;
            }

            // Encrypted packet — only process if 8-byte aligned
            if (body.Length % 8 == 0)
            {
                var decrypted = getBf()!.Decrypt(body);
                byte opcode = decrypted[0];

                if (opcode == Opcodes.LoginS2C.ServerList)
                {
                    Log?.Invoke($"ServerList: {decrypted.Length} bytes, opcode=0x{opcode:X2}");
                    Log?.Invoke($"ServerList hex: {BitConverter.ToString(decrypted, 0, Math.Min(decrypted.Length, 60)).Replace("-", " ")}");
                    try
                    {
                        if (TryPatchServerList(decrypted, out int patched))
                        {
                            var reEncrypted = getBf()!.Encrypt(decrypted);
                            Buffer.BlockCopy(reEncrypted, 0, body, 0, body.Length);
                            Log?.Invoke($"ServerList patched: {patched} entries → 127.0.0.1:{_gameProxyPort}");
                        }
                        else
                        {
                            Log?.Invoke("ServerList patch: TryPatchServerList returned false");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log?.Invoke($"ServerList patch error: {ex.Message}");
                    }
                }
                else if (opcode == Opcodes.LoginS2C.PlayOk && decrypted.Length >= 17)
                {
                    LoginOk1 = BitConverter.ToInt32(decrypted, 1);
                    LoginOk2 = BitConverter.ToInt32(decrypted, 5);
                    PlayOk1 = BitConverter.ToInt32(decrypted, 9);
                    PlayOk2 = BitConverter.ToInt32(decrypted, 13);
                    Log?.Invoke($"PlayOk: play_ok1=0x{PlayOk1:X8} play_ok2=0x{PlayOk2:X8}");
                }
                else
                {
                    Log?.Invoke($"Login S2C: opcode=0x{opcode:X2} len={decrypted.Length}");
                }
            }

            // Forward (possibly patched) bytes
            await to.WriteAsync(header, ct);
            await to.WriteAsync(body, ct);
        }
    }

    /// <summary>
    /// C2S: forward raw bytes unchanged.
    /// </summary>
    private static async Task RelayRawAsync(NetworkStream from, NetworkStream to, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var header = new byte[2];
            if (!await ReadExactAsync(from, header, ct)) break;
            int totalLen = BitConverter.ToUInt16(header, 0);
            int bodyLen = totalLen - 2;
            if (bodyLen <= 0) continue;

            var body = new byte[bodyLen];
            if (!await ReadExactAsync(from, body, ct)) break;

            await to.WriteAsync(header, ct);
            await to.WriteAsync(body, ct);
        }
    }

    /// <summary>
    /// Patch ServerList: discover real game endpoint, replace IPs with localhost.
    /// Uses logical length scanning (checksum verification) like alesha reference.
    /// </summary>
    private bool TryPatchServerList(byte[] packet, out int patchedCount)
    {
        patchedCount = 0;
        if (packet.Length < 4) return false;

        int logicalLen = FindLogicalLength(packet);
        int checksumStart = logicalLen - 4;

        int count = packet[1];
        int pos = 3; // skip opcode(1) + count(1) + last_server_id(1)
        Log?.Invoke($"ServerList parse: logicalLen={logicalLen} checksumStart={checksumStart} count={count} pos={pos} packetLen={packet.Length}");
        if (count <= 0 || checksumStart <= pos) return false;

        int payloadForStride = checksumStart - pos;
        int stride = 16;
        if (payloadForStride > 0 && payloadForStride % count == 0)
            stride = payloadForStride / count;
        if (stride < 9)
            stride = 16;

        bool discovered = false;
        for (int i = 0; i < count; i++)
        {
            if (pos + stride > checksumStart) break;

            int serverId = packet[pos];
            string ip = $"{packet[pos + 1]}.{packet[pos + 2]}.{packet[pos + 3]}.{packet[pos + 4]}";
            int port = BitConverter.ToInt32(packet, pos + 5);

            if (port is < 1 or > 65535)
            {
                pos += stride;
                continue;
            }

            // Discover real game endpoint from first valid entry
            if (!discovered)
            {
                discovered = true;
                Log?.Invoke($"Discovered real game endpoint: {ip}:{port}");
                GameServerDiscovered?.Invoke(ip, port);
            }

            // Patch to localhost
            packet[pos + 1] = 127;
            packet[pos + 2] = 0;
            packet[pos + 3] = 0;
            packet[pos + 4] = 1;
            BitConverter.GetBytes(_gameProxyPort).CopyTo(packet, pos + 5);

            Log?.Invoke($"ServerList entry id={serverId} patched: {ip}:{port} → 127.0.0.1:{_gameProxyPort}");
            patchedCount++;
            pos += stride;
        }

        if (patchedCount == 0) return false;

        // Recalculate checksum
        AppendChecksum(packet, checksumStart);
        return true;
    }

    /// <summary>
    /// Find logical body length by scanning for a valid XOR checksum with zero padding tail.
    /// Same approach as alesha's FindLogicalLength.
    /// </summary>
    private static int FindLogicalLength(byte[] body)
    {
        int n = body.Length;
        for (int end = 8; end <= n; end += 4)
        {
            if (!VerifyChecksum(body, end)) continue;

            bool tailAllZero = true;
            for (int i = end; i < n; i++)
            {
                if (body[i] != 0) { tailAllZero = false; break; }
            }

            if (tailAllZero) return end;
        }
        return n;
    }

    private static bool VerifyChecksum(byte[] body, int size)
    {
        if (size <= 4 || (size & 3) != 0) return false;
        uint checksum = 0;
        for (int i = 0; i < size - 4; i += 4)
            checksum ^= BitConverter.ToUInt32(body, i);
        uint stored = BitConverter.ToUInt32(body, size - 4);
        return stored == checksum;
    }

    private static void AppendChecksum(byte[] body, int checksumStart)
    {
        uint checksum = 0;
        for (int i = 0; i < checksumStart; i += 4)
            checksum ^= BitConverter.ToUInt32(body, i);
        BitConverter.GetBytes(checksum).CopyTo(body, checksumStart);
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
