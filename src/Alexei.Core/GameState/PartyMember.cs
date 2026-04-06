namespace Alexei.Core.GameState;

public sealed class PartyMember
{
    public int ObjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CurHp { get; set; }
    public int MaxHp { get; set; }
    public int CurMp { get; set; }
    public int MaxMp { get; set; }
    public int CurCp { get; set; }
    public int MaxCp { get; set; }
    public int Level { get; set; }
    public int ClassId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int Heading { get; set; }
    public int TargetId { get; set; }
    public DateTime LastUpdateUtc { get; set; }
    public DateTime LastPositionUpdateUtc { get; set; }
    public Dictionary<int, AbnormalEffect> Buffs { get; } = new();

    public bool HasVitals => MaxHp > 0 && CurHp > 0;
    public bool HasPosition => LastPositionUpdateUtc != DateTime.MinValue;
    public double HpPct => MaxHp > 0 ? (double)CurHp / MaxHp * 100 : 0;
    public double MpPct => MaxMp > 0 ? (double)CurMp / MaxMp * 100 : 0;

    public double DistanceTo(int x, int y, int z)
    {
        double dx = X - x;
        double dy = Y - y;
        double dz = Z - z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}
