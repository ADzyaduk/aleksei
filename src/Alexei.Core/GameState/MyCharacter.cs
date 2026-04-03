namespace Alexei.Core.GameState;

public sealed class MyCharacter
{
    public int ObjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int Heading { get; set; }

    public int CurHp { get; set; }
    public int MaxHp { get; set; }
    public int CurMp { get; set; }
    public int MaxMp { get; set; }
    public int CurCp { get; set; }
    public int MaxCp { get; set; }

    public int Level { get; set; }
    public long Exp { get; set; }
    public int SP { get; set; }

    public int Race { get; set; }
    public int Sex { get; set; }
    public int ClassId { get; set; }

    public int TargetId { get; set; }
    public int PendingTargetId { get; set; }
    public bool IsSitting { get; set; }
    public bool IsDead { get; set; }

    // Combat anchor
    public int AnchorX { get; set; }
    public int AnchorY { get; set; }
    public int AnchorZ { get; set; }
    public bool AnchorSet { get; set; }

    public double HpPct => MaxHp > 0 ? (double)CurHp / MaxHp * 100 : 0;
    public double MpPct => MaxMp > 0 ? (double)CurMp / MaxMp * 100 : 0;
    public double CpPct => MaxCp > 0 ? (double)CurCp / MaxCp * 100 : 0;

    public double DistanceTo(int x, int y, int z)
    {
        double dx = X - x;
        double dy = Y - y;
        double dz = Z - z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}
