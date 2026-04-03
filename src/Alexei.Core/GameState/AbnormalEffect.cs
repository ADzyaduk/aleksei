namespace Alexei.Core.GameState;

public sealed class AbnormalEffect
{
    public int SkillId { get; set; }
    public int Level { get; set; }
    public int DurationMs { get; set; }
    public DateTime ExpiresAt { get; set; }

    public bool IsActive => DateTime.UtcNow < ExpiresAt;

    public void SetDuration(int durationMs)
    {
        DurationMs = durationMs;
        ExpiresAt = DateTime.UtcNow.AddMilliseconds(durationMs);
    }
}
