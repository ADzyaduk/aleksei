using System.Net.Sockets;

namespace Alexei.Core.Protocol;

/// <summary>
/// Reads framed L2 packets from a NetworkStream.
/// Wire format: [uint16 LE: total_length] [body of total_length - 2 bytes]
/// </summary>
public sealed class PacketBuffer
{
    private readonly NetworkStream _stream;
    private readonly byte[] _headerBuf = new byte[2];

    public PacketBuffer(NetworkStream stream)
    {
        _stream = stream;
    }

    /// <summary>
    /// Read one complete L2 packet. Returns the body (without length header).
    /// Returns null if the stream is closed.
    /// </summary>
    public async Task<byte[]?> ReadPacketAsync(CancellationToken ct = default)
    {
        if (!await ReadExactAsync(_headerBuf, 0, 2, ct))
            return null;

        int totalLen = BitConverter.ToUInt16(_headerBuf, 0);
        int bodyLen = totalLen - 2;
        if (bodyLen <= 0) return null;

        var body = new byte[bodyLen];
        if (!await ReadExactAsync(body, 0, bodyLen, ct))
            return null;

        return body;
    }

    private async Task<bool> ReadExactAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        int read = 0;
        while (read < count)
        {
            int n = await _stream.ReadAsync(buffer.AsMemory(offset + read, count - read), ct);
            if (n == 0) return false;
            read += n;
        }
        return true;
    }

    /// <summary>
    /// Build a wire-format packet: [uint16 LE length] [body].
    /// </summary>
    public static byte[] Frame(byte[] body)
    {
        int totalLen = body.Length + 2;
        var wire = new byte[totalLen];
        BitConverter.GetBytes((ushort)totalLen).CopyTo(wire, 0);
        Buffer.BlockCopy(body, 0, wire, 2, body.Length);
        return wire;
    }
}
