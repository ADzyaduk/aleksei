using System.Text;

namespace Alexei.Core.Protocol;

/// <summary>
/// Sequential reader for L2 packet payloads (little-endian).
/// </summary>
public ref struct PacketReader
{
    private readonly ReadOnlySpan<byte> _data;
    private int _pos;

    public PacketReader(ReadOnlySpan<byte> data)
    {
        _data = data;
        _pos = 0;
    }

    public int Position => _pos;
    public int Remaining => _data.Length - _pos;

    public byte ReadByte()
    {
        return _data[_pos++];
    }

    public short ReadInt16()
    {
        var val = BitConverter.ToInt16(_data.Slice(_pos, 2));
        _pos += 2;
        return val;
    }

    public ushort ReadUInt16()
    {
        var val = BitConverter.ToUInt16(_data.Slice(_pos, 2));
        _pos += 2;
        return val;
    }

    public int ReadInt32()
    {
        var val = BitConverter.ToInt32(_data.Slice(_pos, 4));
        _pos += 4;
        return val;
    }

    public uint ReadUInt32()
    {
        var val = BitConverter.ToUInt32(_data.Slice(_pos, 4));
        _pos += 4;
        return val;
    }

    public long ReadInt64()
    {
        var val = BitConverter.ToInt64(_data.Slice(_pos, 8));
        _pos += 8;
        return val;
    }

    public double ReadDouble()
    {
        var val = BitConverter.ToDouble(_data.Slice(_pos, 8));
        _pos += 8;
        return val;
    }

    /// <summary>
    /// Read UTF-16LE null-terminated string.
    /// </summary>
    public string ReadString()
    {
        int start = _pos;
        while (_pos + 1 < _data.Length)
        {
            ushort ch = BitConverter.ToUInt16(_data.Slice(_pos, 2));
            _pos += 2;
            if (ch == 0) break;
        }
        int byteLen = _pos - start - 2; // exclude null terminator
        if (byteLen <= 0) return string.Empty;
        return Encoding.Unicode.GetString(_data.Slice(start, byteLen));
    }

    public ReadOnlySpan<byte> ReadBytes(int count)
    {
        var slice = _data.Slice(_pos, count);
        _pos += count;
        return slice;
    }

    public void Skip(int count)
    {
        _pos += count;
    }
}
