using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Alexei.Core.Config;

// Shared INPC helper used by editable config models (DataGrid binding requires it)
public abstract class NotifyBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}

[JsonConverter(typeof(JsonStringEnumConverter<PartyMode>))]
public enum PartyMode
{
    None,
    Follow,
    Assist
}

public sealed class SkillRotationEntry : NotifyBase
{
    private int _skillId; [JsonPropertyName("skillId")] public int SkillId { get => _skillId; set => SetField(ref _skillId, value); }
    private int _level = 1; [JsonPropertyName("level")] public int Level { get => _level; set => SetField(ref _level, value); }
    private double _minMpPct = 10; [JsonPropertyName("minMpPct")] public double MinMpPct { get => _minMpPct; set => SetField(ref _minMpPct, value); }
    private int _cooldownMs = 5000; [JsonPropertyName("cooldownMs")] public int CooldownMs { get => _cooldownMs; set => SetField(ref _cooldownMs, value); }
    private double _targetHpBelowPct; [JsonPropertyName("targetHpBelowPct")] public double TargetHpBelowPct { get => _targetHpBelowPct; set => SetField(ref _targetHpBelowPct, value); }
    private double _targetHpAbovePct; [JsonPropertyName("targetHpAbovePct")] public double TargetHpAbovePct { get => _targetHpAbovePct; set => SetField(ref _targetHpAbovePct, value); }
    private double _maxRange; [JsonPropertyName("maxRange")] public double MaxRange { get => _maxRange; set => SetField(ref _maxRange, value); }
    private bool _enabled = true; [JsonPropertyName("enabled")] public bool Enabled { get => _enabled; set => SetField(ref _enabled, value); }
}

public sealed class CombatConfig
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("aggroRadius")] public double AggroRadius { get; set; } = 800;
    [JsonPropertyName("zHeightLimit")] public int ZHeightLimit { get; set; } = 200;
    [JsonPropertyName("anchorLeash")] public double AnchorLeash { get; set; } = 1500;
    [JsonPropertyName("retainTargetMaxDist")] public double RetainTargetMaxDist { get; set; } = 325;
    [JsonPropertyName("preferAggroTargets")] public bool PreferAggroTargets { get; set; }
    [JsonPropertyName("targetPriority")] public string TargetPriority { get; set; } = "nearest";
    [JsonPropertyName("targetNpcIds")] public List<int> TargetNpcIds { get; set; } = new();
    [JsonPropertyName("skillRotation")] public List<SkillRotationEntry> SkillRotation { get; set; } = new();
    [JsonPropertyName("basicAttack")] public bool BasicAttack { get; set; } = true;
    [JsonPropertyName("postSkillDelayMs")] public int PostSkillDelayMs { get; set; } = 100;
    [JsonPropertyName("reattackIntervalMs")] public int ReattackIntervalMs { get; set; } = 1500;
    [JsonPropertyName("postKillSpawnWaitMs")] public int PostKillSpawnWaitMs { get; set; } = 350;
    [JsonPropertyName("postKillLootWindowMs")] public int PostKillLootWindowMs { get; set; } = 2500;
    /// <summary>"2f" = shortcut bar (Teon); "39dcb" = MagicSkillUse dcb format (Bartz); "39ddd" = ddd.</summary>
    [JsonPropertyName("combatSkillPacket")] public string CombatSkillPacket { get; set; } = "2f";
    /// <summary>true for Bartz - use 0x1F TargetEnter for targeting/loot instead of 0x04 Action / 0x48 GetItem.</summary>
    [JsonPropertyName("useTargetEnter")] public bool UseTargetEnter { get; set; } = false;
}

public sealed class BuffEntry : NotifyBase
{
    private int _skillId; [JsonPropertyName("skillId")] public int SkillId { get => _skillId; set => SetField(ref _skillId, value); }
    private int _level = 1; [JsonPropertyName("level")] public int Level { get => _level; set => SetField(ref _level, value); }
    private double _intervalSec = 1200; [JsonPropertyName("intervalSec")] public double IntervalSec { get => _intervalSec; set => SetField(ref _intervalSec, value); }
    private bool _rebuffOnMissing = true; [JsonPropertyName("rebuffOnMissing")] public bool RebuffOnMissing { get => _rebuffOnMissing; set => SetField(ref _rebuffOnMissing, value); }
    private string _target = "self"; [JsonPropertyName("target")] public string Target { get => _target; set => SetField(ref _target, value); }
    private bool _enabled = true; [JsonPropertyName("enabled")] public bool Enabled { get => _enabled; set => SetField(ref _enabled, value); }
}

public sealed class BuffConfig
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("list")] public List<BuffEntry> List { get; set; } = new();
}

public sealed class HealRule : NotifyBase
{
    private int _skillId; [JsonPropertyName("skillId")] public int SkillId { get => _skillId; set => SetField(ref _skillId, value); }
    private int _level = 1; [JsonPropertyName("level")] public int Level { get => _level; set => SetField(ref _level, value); }
    private double _hpThreshold = 70; [JsonPropertyName("hpThreshold")] public double HpThreshold { get => _hpThreshold; set => SetField(ref _hpThreshold, value); }
    private double _mpMinPct = 20; [JsonPropertyName("mpMinPct")] public double MpMinPct { get => _mpMinPct; set => SetField(ref _mpMinPct, value); }
    private int _cooldownMs = 1500; [JsonPropertyName("cooldownMs")] public int CooldownMs { get => _cooldownMs; set => SetField(ref _cooldownMs, value); }
    private bool _enabled = true; [JsonPropertyName("enabled")] public bool Enabled { get => _enabled; set => SetField(ref _enabled, value); }
}

public sealed class PartyConfig
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("mode")] public PartyMode Mode { get; set; } = PartyMode.None;
    [JsonPropertyName("leaderName")] public string LeaderName { get; set; } = string.Empty;
    [JsonPropertyName("assistName")] public string AssistName { get; set; } = string.Empty;
    [JsonPropertyName("followDistance")] public double FollowDistance { get; set; } = 150;
    [JsonPropertyName("repathDistance")] public double RepathDistance { get; set; } = 300;
    [JsonPropertyName("positionTimeoutMs")] public int PositionTimeoutMs { get; set; } = 2000;
    [JsonPropertyName("healRules")] public List<HealRule> HealRules { get; set; } = new();
    [JsonPropertyName("buffRules")] public List<BuffEntry> BuffRules { get; set; } = new();
}

public sealed class LootConfig
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
    [JsonPropertyName("radius")] public double Radius { get; set; } = 400;
}

public sealed class SpoilConfig
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("skillId")] public int SkillId { get; set; }
    [JsonPropertyName("maxAttempts")] public int MaxAttempts { get; set; } = 3;
    [JsonPropertyName("sweepSkillId")] public int SweepSkillId { get; set; }
    [JsonPropertyName("sweepEnabled")] public bool SweepEnabled { get; set; }
}

public sealed class RecoveryConfig
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("sitBelowHpPct")] public double SitBelowHpPct { get; set; } = 30;
    [JsonPropertyName("sitBelowMpPct")] public double SitBelowMpPct { get; set; } = 20;
    [JsonPropertyName("standAboveHpPct")] public double StandAboveHpPct { get; set; } = 80;
    [JsonPropertyName("standAboveMpPct")] public double StandAboveMpPct { get; set; } = 60;
}

public sealed class CharacterProfile
{
    [JsonPropertyName("combat")] public CombatConfig Combat { get; set; } = new();
    [JsonPropertyName("buffs")] public BuffConfig Buffs { get; set; } = new();
    [JsonPropertyName("party")] public PartyConfig Party { get; set; } = new();
    [JsonPropertyName("loot")] public LootConfig Loot { get; set; } = new();
    [JsonPropertyName("recovery")] public RecoveryConfig Recovery { get; set; } = new();
    [JsonPropertyName("spoil")] public SpoilConfig Spoil { get; set; } = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static CharacterProfile Load(string path)
    {
        if (!File.Exists(path))
            return new CharacterProfile();
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<CharacterProfile>(json, JsonOpts) ?? new CharacterProfile();
    }

    public void Save(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOpts));
    }
}
