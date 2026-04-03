using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

namespace Alexei.Core.Crypto;

/// <summary>
/// L2 Blowfish ECB with non-standard LE word swap within 8-byte blocks.
/// </summary>
public sealed class L2Blowfish
{
    public static readonly byte[] DefaultGameKey =
    {
        0x6B, 0x60, 0xCB, 0x5B, 0x82, 0xCE, 0x90, 0xB1,
        0xCC, 0x2B, 0x6C, 0x55, 0x6C, 0x6C, 0x6C, 0x6C
    };

    public static readonly byte[] StaticKeySuffix =
    {
        0xC8, 0x27, 0x93, 0x01, 0xA1, 0x6C, 0x31, 0x97
    };

    private readonly BlowfishEngine _encEngine;
    private readonly BlowfishEngine _decEngine;

    public L2Blowfish(byte[] key)
    {
        var keyParam = new KeyParameter(key);
        _encEngine = new BlowfishEngine();
        _encEngine.Init(true, keyParam);
        _decEngine = new BlowfishEngine();
        _decEngine.Init(false, keyParam);
    }

    public byte[] Encrypt(byte[] data)
    {
        var padded = PadTo8(data);
        for (int i = 0; i < padded.Length; i += 8)
        {
            SwapWords(padded, i);
            _encEngine.ProcessBlock(padded, i, padded, i);
            SwapWords(padded, i);
        }
        return padded;
    }

    public byte[] Decrypt(byte[] data)
    {
        var result = new byte[data.Length];
        Buffer.BlockCopy(data, 0, result, 0, data.Length);
        for (int i = 0; i + 7 < result.Length; i += 8)
        {
            SwapWords(result, i);
            _decEngine.ProcessBlock(result, i, result, i);
            SwapWords(result, i);
        }
        return result;
    }

    /// <summary>
    /// Byte-reverse each 4-byte word within an 8-byte block (L2 non-standard LE↔BE swap).
    /// [A,B,C,D,E,F,G,H] → [D,C,B,A,H,G,F,E]
    /// </summary>
    private static void SwapWords(byte[] buf, int offset)
    {
        // Reverse bytes in first 4-byte word
        (buf[offset], buf[offset + 3]) = (buf[offset + 3], buf[offset]);
        (buf[offset + 1], buf[offset + 2]) = (buf[offset + 2], buf[offset + 1]);
        // Reverse bytes in second 4-byte word
        (buf[offset + 4], buf[offset + 7]) = (buf[offset + 7], buf[offset + 4]);
        (buf[offset + 5], buf[offset + 6]) = (buf[offset + 6], buf[offset + 5]);
    }

    private static byte[] PadTo8(byte[] data)
    {
        int padded = (data.Length + 7) & ~7;
        if (padded == data.Length) return (byte[])data.Clone();
        var result = new byte[padded];
        Buffer.BlockCopy(data, 0, result, 0, data.Length);
        return result;
    }

    public static byte[] BuildGameKey(byte[] dynamicKey)
    {
        var full = new byte[16];
        Buffer.BlockCopy(dynamicKey, 0, full, 0, Math.Min(dynamicKey.Length, 8));
        Buffer.BlockCopy(StaticKeySuffix, 0, full, 8, 8);
        return full;
    }
}
