namespace Alexei.Core.Crypto;

/// <summary>
/// Login server XOR cipher pass (applied after/before Blowfish).
/// </summary>
public static class NewCrypt
{
    /// <summary>
    /// Decrypt XOR pass for login S2C packets (post-Blowfish).
    /// </summary>
    public static void DecXorPass(byte[] data, int offset, int length, int key)
    {
        int stop = offset + length - 8; // last 8 bytes are checksum + padding
        int prev = key;
        for (int i = offset; i < stop; i += 4)
        {
            int cur = BitConverter.ToInt32(data, i);
            int dec = cur ^ prev;
            prev = cur;
            BitConverter.GetBytes(dec).CopyTo(data, i);
        }
    }

    /// <summary>
    /// Encrypt XOR pass for login C2S packets (pre-Blowfish).
    /// </summary>
    public static void EncXorPass(byte[] data, int offset, int length, int key)
    {
        int stop = offset + length - 8;
        int prev = key;
        for (int i = offset; i < stop; i += 4)
        {
            int cur = BitConverter.ToInt32(data, i);
            int enc = cur ^ prev;
            prev = enc;
            BitConverter.GetBytes(enc).CopyTo(data, i);
        }
    }
}
