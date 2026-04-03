namespace Alexei.Core.GameState;

public enum CombatPhase
{
    Idle,
    SelectingTarget,
    Opening,
    Engaging,
    KillLoop,
    PostKill,
    Looting,
    Recovering
}
