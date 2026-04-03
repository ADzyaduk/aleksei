namespace Alexei.Core.GameState;

public sealed class SkillInfo
{
    public int SkillId { get; set; }
    public int Level { get; set; }
    public bool IsPassive { get; set; }
    public bool IsToggle { get; set; }
    public bool IsItemLike { get; set; }
    public DateTime CooldownUntil { get; set; } = DateTime.MinValue;

    public bool IsReady => DateTime.UtcNow >= CooldownUntil;

    public void SetCooldown(int remainingMs)
    {
        CooldownUntil = DateTime.UtcNow.AddMilliseconds(remainingMs);
    }
}
