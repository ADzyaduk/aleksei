namespace Alexei.Core.Config;

public sealed class ProfileManager
{
    private readonly string _profilesDir;

    public CharacterProfile Current { get; private set; } = new();
    public string CurrentCharName { get; private set; } = "";

    public ProfileManager(string profilesDir)
    {
        _profilesDir = profilesDir;
        if (!Directory.Exists(_profilesDir))
            Directory.CreateDirectory(_profilesDir);
    }

    public void LoadForCharacter(string charName, string? serverId = null)
    {
        CurrentCharName = charName;
        var slug = SanitizeFileName(charName);
        var path = Path.Combine(_profilesDir, $"{slug}.json");

        if (File.Exists(path))
        {
            Current = CharacterProfile.Load(path);
        }
        else
        {
            var defaultPath = Path.Combine(_profilesDir, "default.json");
            Current = File.Exists(defaultPath)
                ? CharacterProfile.Load(defaultPath)
                : new CharacterProfile();
        }
    }

    /// <summary>
    /// After loading, fix up profile defaults for the given server when protocol-specific flags are missing.
    /// Do not normalize user-tuned radii or leash values here.
    /// </summary>
    public bool ApplyServerDefaults(string serverId)
    {
        if (serverId != "bartz") return false;

        bool changed = false;

        if (Current.Combat.CombatSkillPacket != "39dcb")
        {
            Current.Combat.CombatSkillPacket = "39dcb";
            changed = true;
        }

        if (!Current.Combat.UseTargetEnter)
        {
            Current.Combat.UseTargetEnter = true;
            changed = true;
        }

        if (!Current.Combat.Enabled)
        {
            Current.Combat.Enabled = true;
            changed = true;
        }

        if (Current.Combat.ReattackIntervalMs <= 0)
        {
            Current.Combat.ReattackIntervalMs = 1500;
            changed = true;
        }

        if (Current.Combat.PostKillSpawnWaitMs <= 0)
        {
            Current.Combat.PostKillSpawnWaitMs = 350;
            changed = true;
        }

        if (Current.Combat.PostKillLootWindowMs <= 0)
        {
            Current.Combat.PostKillLootWindowMs = 2500;
            changed = true;
        }

        return changed;
    }

    public void Save()
    {
        if (string.IsNullOrEmpty(CurrentCharName)) return;
        var slug = SanitizeFileName(CurrentCharName);
        var path = Path.Combine(_profilesDir, $"{slug}.json");
        Current.Save(path);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrEmpty(clean) ? "unnamed" : clean.ToLowerInvariant();
    }
}
