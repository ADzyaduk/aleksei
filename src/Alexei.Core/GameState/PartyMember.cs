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

    public double HpPct => MaxHp > 0 ? (double)CurHp / MaxHp * 100 : 0;
    public double MpPct => MaxMp > 0 ? (double)CurMp / MaxMp * 100 : 0;
}
