using System.Text;

namespace Alexei.Core.Protocol;

/// <summary>
/// Builder for outgoing L2 packets (little-endian).
/// </summary>
public sealed class PacketWriter
{
    private readonly List<byte> _buf;

    public PacketWriter(int capacity = 64)
    {
        _buf = new List<byte>(capacity);
    }

    public PacketWriter WriteByte(byte value)
    {
        _buf.Add(value);
        return this;
    }

    public PacketWriter WriteInt16(short value)
    {
        _buf.AddRange(BitConverter.GetBytes(value));
        return this;
    }

    public PacketWriter WriteInt32(int value)
    {
        _buf.AddRange(BitConverter.GetBytes(value));
        return this;
    }

    public PacketWriter WriteInt64(long value)
    {
        _buf.AddRange(BitConverter.GetBytes(value));
        return this;
    }

    public PacketWriter WriteDouble(double value)
    {
        _buf.AddRange(BitConverter.GetBytes(value));
        return this;
    }

    /// <summary>
    /// Write UTF-16LE null-terminated string.
    /// </summary>
    public PacketWriter WriteString(string value)
    {
        _buf.AddRange(Encoding.Unicode.GetBytes(value));
        _buf.Add(0);
        _buf.Add(0);
        return this;
    }

    public PacketWriter WriteBytes(byte[] data)
    {
        _buf.AddRange(data);
        return this;
    }

    public PacketWriter WriteBytes(ReadOnlySpan<byte> data)
    {
        foreach (var b in data) _buf.Add(b);
        return this;
    }

    /// <summary>
    /// Returns the built packet payload (without opcode or length header).
    /// </summary>
    public byte[] ToArray() => _buf.ToArray();

    public int Length => _buf.Count;
}
