using System.Collections.ObjectModel;
using System.Windows.Input;
using Alexei.App.Infrastructure;
using Alexei.Core.Config;
using Alexei.Core.GameState;

namespace Alexei.App.ViewModels;

public sealed class CombatTabVM : ViewModelBase
{
    private readonly ProfileManager _pm;

    private bool _enabled; public bool Enabled { get => _enabled; set => SetField(ref _enabled, value); }
    private double _aggroRadius; public double AggroRadius { get => _aggroRadius; set => SetField(ref _aggroRadius, value); }
    private int _zHeightLimit; public int ZHeightLimit { get => _zHeightLimit; set => SetField(ref _zHeightLimit, value); }
    private double _anchorLeash; public double AnchorLeash { get => _anchorLeash; set => SetField(ref _anchorLeash, value); }
    private string _targetPriority = "nearest"; public string TargetPriority { get => _targetPriority; set => SetField(ref _targetPriority, value); }
    private bool _basicAttack = true; public bool BasicAttack { get => _basicAttack; set => SetField(ref _basicAttack, value); }

    // Spoil
    private bool _spoilEnabled; public bool SpoilEnabled { get => _spoilEnabled; set => SetField(ref _spoilEnabled, value); }
    private int _spoilSkillId; public int SpoilSkillId { get => _spoilSkillId; set => SetField(ref _spoilSkillId, value); }
    private int _spoilMaxAttempts = 3; public int SpoilMaxAttempts { get => _spoilMaxAttempts; set => SetField(ref _spoilMaxAttempts, value); }
    private bool _sweepEnabled; public bool SweepEnabled { get => _sweepEnabled; set => SetField(ref _sweepEnabled, value); }
    private int _sweepSkillId; public int SweepSkillId { get => _sweepSkillId; set => SetField(ref _sweepSkillId, value); }

    // Skill picker
    public ObservableCollection<SkillPickerItem> AvailableSkills { get; } = new();
    private SkillPickerItem? _selectedSkill;
    public SkillPickerItem? SelectedSkill { get => _selectedSkill; set => SetField(ref _selectedSkill, value); }

    private SkillRotationEntry? _selectedRotationEntry;
    public SkillRotationEntry? SelectedRotationEntry
    {
        get => _selectedRotationEntry;
        set => SetField(ref _selectedRotationEntry, value);
    }

    public ObservableCollection<SkillRotationEntry> SkillRotation { get; } = new();

    public ICommand AddSkillCommand { get; }
    public ICommand RemoveSkillCommand { get; }
    public ICommand SetSpoilSkillCommand { get; }

    public CombatTabVM(ProfileManager pm)
    {
        _pm = pm;
        AddSkillCommand = new RelayCommand(AddSkill, () => SelectedSkill != null);
        RemoveSkillCommand = new RelayCommand(RemoveSkill, () => SelectedRotationEntry != null);
        SetSpoilSkillCommand = new RelayCommand(SetSpoilSkill, () => SelectedSkill != null);
    }

    private void AddSkill()
    {
        if (SelectedSkill == null) return;
        SkillRotation.Add(new SkillRotationEntry
        {
            SkillId = SelectedSkill.SkillId,
            Level = SelectedSkill.Level,
            Enabled = true,
            MinMpPct = 10,
            CooldownMs = 0,
            TargetHpBelowPct = 0,
            TargetHpAbovePct = 0,
            MaxRange = 0
        });
    }

    private void RemoveSkill()
    {
        if (SelectedRotationEntry != null)
            SkillRotation.Remove(SelectedRotationEntry);
    }

    private void SetSpoilSkill()
    {
        if (SelectedSkill != null)
            SpoilSkillId = SelectedSkill.SkillId;
    }

    public void UpdateAvailableSkills(IEnumerable<SkillInfo> skills)
    {
        var list = skills.Where(s => !s.IsPassive && !s.IsItemLike).OrderBy(s => s.SkillId)
            .Select(s => new SkillPickerItem(s.SkillId, s.Level)).ToList();

        // Skip rebuild if unchanged вЂ” clearing AvailableSkills resets ComboBox SelectedItem to null
        if (list.Count == AvailableSkills.Count &&
            list.Zip(AvailableSkills).All(p => p.First == p.Second))
            return;

        var prevSkillId = SelectedSkill?.SkillId;
        AvailableSkills.Clear();
        foreach (var s in list) AvailableSkills.Add(s);
        if (prevSkillId.HasValue)
            SelectedSkill = AvailableSkills.FirstOrDefault(s => s.SkillId == prevSkillId);
    }

    public void Refresh()
    {
        var c = _pm.Current.Combat;
        Enabled = c.Enabled;
        AggroRadius = c.AggroRadius;
        ZHeightLimit = c.ZHeightLimit;
        AnchorLeash = c.AnchorLeash;
        TargetPriority = c.TargetPriority;
        BasicAttack = c.BasicAttack;
        SkillRotation.Clear();
        foreach (var s in c.SkillRotation) SkillRotation.Add(s);

        var sp = _pm.Current.Spoil;
        SpoilEnabled = sp.Enabled;
        SpoilSkillId = sp.SkillId;
        SpoilMaxAttempts = sp.MaxAttempts;
        SweepEnabled = sp.SweepEnabled;
        SweepSkillId = sp.SweepSkillId;
    }

    public void ApplyToProfile()
    {
        var c = _pm.Current.Combat;
        c.Enabled = Enabled;
        c.AggroRadius = AggroRadius;
        c.ZHeightLimit = ZHeightLimit;
        c.AnchorLeash = AnchorLeash;
        c.TargetPriority = TargetPriority;
        c.BasicAttack = BasicAttack;
        c.SkillRotation = new List<SkillRotationEntry>(SkillRotation);

        var sp = _pm.Current.Spoil;
        sp.Enabled = SpoilEnabled;
        sp.SkillId = SpoilSkillId;
        sp.MaxAttempts = SpoilMaxAttempts;
        sp.SweepEnabled = SweepEnabled;
        sp.SweepSkillId = SweepSkillId;
    }
}


