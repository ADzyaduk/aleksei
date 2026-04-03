using System.Collections.Concurrent;

namespace Alexei.Core.GameState;

public sealed class GameWorld
{
    public MyCharacter Me { get; } = new();
    public ConcurrentDictionary<int, Npc> Npcs { get; } = new();
    public ConcurrentDictionary<int, GroundItem> Items { get; } = new();
    public ConcurrentDictionary<int, PartyMember> Party { get; } = new();
    public ConcurrentDictionary<int, SkillInfo> Skills { get; } = new();
    public ConcurrentDictionary<int, AbnormalEffect> Buffs { get; } = new(); // self buffs
    public ConcurrentDictionary<int, SpoilStatus> SpoiledNpcs { get; } = new();

    public bool IsConnected { get; set; }
    public bool IsOpcodeDetected { get; set; }
    public byte OpcodeXorKey { get; set; }
    public CombatPhase CurrentCombatPhase { get; private set; } = CombatPhase.Idle;
    public DateTime CombatPhaseChangedUtc { get; private set; } = DateTime.UtcNow;
    public int LastEngagedTargetId { get; set; }
    public DateTime? LastSelfMoveEvidenceUtc { get; set; }
    public DateTime? LastCombatProgressUtc { get; set; }
    public PositionConfidence PositionConfidence { get; set; } = PositionConfidence.Unknown;

    public event Action? Updated;

    public void NotifyUpdated() => Updated?.Invoke();

    public void SetCombatPhase(CombatPhase phase)
    {
        if (CurrentCombatPhase == phase) return;
        CurrentCombatPhase = phase;
        CombatPhaseChangedUtc = DateTime.UtcNow;
        NotifyUpdated();
    }

    /// <summary>
    /// Clean dead NPCs older than the given threshold.
    /// </summary>
    public void CleanDeadNpcs(TimeSpan threshold)
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in Npcs)
        {
            if (kvp.Value.IsDead && (now - kvp.Value.LastUpdate) > threshold)
                Npcs.TryRemove(kvp.Key, out _);
        }
    }

    /// <summary>
    /// Remove expired buffs.
    /// </summary>
    public void CleanExpiredBuffs()
    {
        foreach (var kvp in Buffs)
        {
            if (!kvp.Value.IsActive)
                Buffs.TryRemove(kvp.Key, out _);
        }
    }

    public void Reset()
    {
        Npcs.Clear();
        Items.Clear();
        Party.Clear();
        Skills.Clear();
        Buffs.Clear();
        SpoiledNpcs.Clear();
        IsOpcodeDetected = false;
        OpcodeXorKey = 0;
        LastEngagedTargetId = 0;
        LastSelfMoveEvidenceUtc = null;
        LastCombatProgressUtc = null;
        PositionConfidence = PositionConfidence.Unknown;
        CurrentCombatPhase = CombatPhase.Idle;
        CombatPhaseChangedUtc = DateTime.UtcNow;
    }
}
