namespace Alexei.Core.GameState;

public sealed class Npc
{
    public int ObjectId { get; set; }
    public int NpcTypeId { get; set; }
    public int NpcId => NpcTypeId - 1_000_000;
    public string Name { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int Heading { get; set; }
    public bool IsAttackable { get; set; }
    public bool IsDead { get; set; }
    public int CurHp { get; set; }
    public int MaxHp { get; set; }
    public int CurMp { get; set; }
    public int MaxMp { get; set; }
    public int CurCp { get; set; }
    public int MaxCp { get; set; }
    public int HpPercent { get; set; } = 100;
    public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
    public DateTime? LastDeathEvidenceUtc { get; set; }
    public DateTime? LastDropEvidenceUtc { get; set; }
    public DateTime? LastAttackOnMeUtc { get; set; }

    public double DistanceTo(MyCharacter me)
    {
        double dx = X - me.X;
        double dy = Y - me.Y;
        double dz = Z - me.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public int ZDelta(MyCharacter me) => Math.Abs(Z - me.Z);

    public bool WasAttackingMeRecent(TimeSpan window, DateTime? nowUtc = null) =>
        LastAttackOnMeUtc.HasValue && (nowUtc ?? DateTime.UtcNow) - LastAttackOnMeUtc.Value <= window;
}
