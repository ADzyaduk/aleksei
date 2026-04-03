namespace Alexei.Core.Crypto;

/// <summary>
/// L2 packet checksum utilities (DWORD XOR).
/// </summary>
public static class ChecksumUtil
{
    /// <summary>
    /// Verify XOR checksum on S2C game packets (last 4 bytes of body).
    /// </summary>
    public static bool Verify(byte[] data, int offset, int length)
    {
        if (length < 4) return false;
        uint checksum = 0;
        int words = (length - 4) / 4;
        for (int i = 0; i < words; i++)
        {
            checksum ^= BitConverter.ToUInt32(data, offset + i * 4);
        }
        uint stored = BitConverter.ToUInt32(data, offset + words * 4);
        return checksum == stored;
    }

    /// <summary>
    /// Append XOR checksum to packet body. Data must have 4 extra bytes at end.
    /// Returns total length including checksum.
    /// </summary>
    public static int Append(byte[] data, int offset, int payloadLength)
    {
        uint checksum = 0;
        int words = payloadLength / 4;
        for (int i = 0; i < words; i++)
        {
            checksum ^= BitConverter.ToUInt32(data, offset + i * 4);
        }
        BitConverter.GetBytes(checksum).CopyTo(data, offset + payloadLength);
        return payloadLength + 4;
    }

    /// <summary>
    /// Recalculate XOR checksum in-place. Last 4 bytes of data are the checksum.
    /// </summary>
    public static void Recalculate(byte[] data)
    {
        int payloadLen = data.Length - 4;
        if (payloadLen < 0) return;
        uint checksum = 0;
        int words = payloadLen / 4;
        for (int i = 0; i < words; i++)
        {
            checksum ^= BitConverter.ToUInt32(data, i * 4);
        }
        BitConverter.GetBytes(checksum).CopyTo(data, payloadLen);
    }
}
