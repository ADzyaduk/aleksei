namespace Alexei.Core.GameState;

public sealed class SpoilStatus
{
    public int Attempts { get; set; }
    public bool Succeeded { get; set; }
    public DateTime LastCastTime { get; set; } = DateTime.MinValue;

    /// <summary>Spoil was cast and we're waiting up to 3s for SystemMessage confirmation.</summary>
    public bool IsPendingConfirmation =>
        !Succeeded && DateTime.UtcNow < LastCastTime.AddSeconds(3);
}
