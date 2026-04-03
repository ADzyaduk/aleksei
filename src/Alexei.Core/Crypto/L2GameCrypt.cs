namespace Alexei.Core.Crypto;

/// <summary>
/// L2 Game XOR stream cipher with evolving counter (Interlude).
/// Separate instances needed for S2C/C2S and for observation/injection.
/// </summary>
public sealed class L2GameCrypt
{
    private readonly byte[] _key;
    private bool _enabled;

    public L2GameCrypt(byte[] key)
    {
        _key = new byte[16];
        Buffer.BlockCopy(key, 0, _key, 0, Math.Min(key.Length, 16));
    }

    public void Enable() => _enabled = true;

    public bool IsEnabled => _enabled;

    /// <summary>
    /// Decrypt S2C data in-place. Returns the same buffer.
    /// </summary>
    public byte[] Decrypt(byte[] data, int offset, int length)
    {
        if (!_enabled) return data;

        byte prev = 0;
        for (int i = 0; i < length; i++)
        {
            byte raw = data[offset + i];
            data[offset + i] = (byte)(raw ^ _key[(i) & 0x0F] ^ prev);
            prev = raw;
        }
        AdvanceCounter(length);
        return data;
    }

    /// <summary>
    /// Encrypt C2S data in-place. Returns the same buffer.
    /// </summary>
    public byte[] Encrypt(byte[] data, int offset, int length)
    {
        if (!_enabled) return data;

        byte prev = 0;
        for (int i = 0; i < length; i++)
        {
            byte plain = data[offset + i];
            byte enc = (byte)(plain ^ _key[i & 0x0F] ^ prev);
            data[offset + i] = enc;
            prev = enc;
        }
        AdvanceCounter(length);
        return data;
    }

    private void AdvanceCounter(int amount)
    {
        uint counter = (uint)(_key[8] | (_key[9] << 8) | (_key[10] << 16) | (_key[11] << 24));
        counter += (uint)amount;
        _key[8] = (byte)(counter & 0xFF);
        _key[9] = (byte)((counter >> 8) & 0xFF);
        _key[10] = (byte)((counter >> 16) & 0xFF);
        _key[11] = (byte)((counter >> 24) & 0xFF);
    }

    public L2GameCrypt Clone()
    {
        var clone = new L2GameCrypt(_key);
        if (_enabled) clone.Enable();
        return clone;
    }
}
