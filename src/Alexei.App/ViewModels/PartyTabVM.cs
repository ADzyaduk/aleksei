using System.Collections.ObjectModel;
using System.Windows.Input;
using Alexei.App.Infrastructure;
using Alexei.Core.Config;
using Alexei.Core.GameState;

namespace Alexei.App.ViewModels;

public sealed class PartyTabVM : ViewModelBase
{
    private readonly ProfileManager _pm;

    private bool _enabled;
    public bool Enabled { get => _enabled; set => SetField(ref _enabled, value); }

    private PartyMode _mode;
    public PartyMode Mode
    {
        get => _mode;
        set
        {
            if (!SetField(ref _mode, value))
                return;

            OnPropertyChanged(nameof(IsLeaderSelectionEnabled));
            ApplyDefaultLeaderSelectionIfNeeded();
        }
    }

    private string _leaderName = string.Empty;
    public string LeaderName { get => _leaderName; set => SetField(ref _leaderName, value); }

    private string _assistName = string.Empty;
    public string AssistName { get => _assistName; set => SetField(ref _assistName, value); }

    private double _followDistance = 150;
    public double FollowDistance { get => _followDistance; set => SetField(ref _followDistance, value); }

    private double _repathDistance = 300;
    public double RepathDistance { get => _repathDistance; set => SetField(ref _repathDistance, value); }

    private int _positionTimeoutMs = 2000;
    public int PositionTimeoutMs { get => _positionTimeoutMs; set => SetField(ref _positionTimeoutMs, value); }

    public bool IsLeaderSelectionEnabled => AvailablePartyMembers.Count > 0;
    public IReadOnlyList<PartyMode> AvailableModes { get; } = Enum.GetValues<PartyMode>();
    public ObservableCollection<SkillPickerItem> AvailableSkills { get; } = new();
    public ObservableCollection<PartyMemberPickerItem> AvailablePartyMembers { get; } = new();

    private SkillPickerItem? _selectedHealSkill;
    public SkillPickerItem? SelectedHealSkill { get => _selectedHealSkill; set => SetField(ref _selectedHealSkill, value); }

    private SkillPickerItem? _selectedBuffSkill;
    public SkillPickerItem? SelectedBuffSkill { get => _selectedBuffSkill; set => SetField(ref _selectedBuffSkill, value); }

    private HealRule? _selectedHealRule;
    public HealRule? SelectedHealRule { get => _selectedHealRule; set => SetField(ref _selectedHealRule, value); }

    private BuffEntry? _selectedBuffRule;
    public BuffEntry? SelectedBuffRule { get => _selectedBuffRule; set => SetField(ref _selectedBuffRule, value); }

    public ObservableCollection<HealRule> HealRules { get; } = new();
    public ObservableCollection<BuffEntry> BuffRules { get; } = new();

    public ICommand AddHealRuleCommand { get; }
    public ICommand RemoveHealRuleCommand { get; }
    public ICommand AddBuffRuleCommand { get; }
    public ICommand RemoveBuffRuleCommand { get; }

    public PartyTabVM(ProfileManager pm)
    {
        _pm = pm;
        AddHealRuleCommand = new RelayCommand(AddHealRule, () => SelectedHealSkill != null);
        RemoveHealRuleCommand = new RelayCommand(RemoveHealRule, () => SelectedHealRule != null);
        AddBuffRuleCommand = new RelayCommand(AddBuffRule, () => SelectedBuffSkill != null);
        RemoveBuffRuleCommand = new RelayCommand(RemoveBuffRule, () => SelectedBuffRule != null);
    }

    private void AddHealRule()
    {
        if (SelectedHealSkill == null) return;
        bool isRechargeSkill = IsManaSupportSkill(SelectedHealSkill.SkillId);
        HealRules.Add(new HealRule
        {
            SkillId = SelectedHealSkill.SkillId,
            Level = SelectedHealSkill.Level,
            HpThreshold = isRechargeSkill ? 0 : 70,
            MpThreshold = isRechargeSkill ? 60 : 0,
            MpMinPct = 10,
            CooldownMs = 1500,
            Enabled = true
        });
    }

    private void RemoveHealRule()
    {
        if (SelectedHealRule != null)
            HealRules.Remove(SelectedHealRule);
    }

    private void AddBuffRule()
    {
        if (SelectedBuffSkill == null) return;
        BuffRules.Add(new BuffEntry
        {
            SkillId = SelectedBuffSkill.SkillId,
            Level = SelectedBuffSkill.Level,
            IntervalSec = 1200,
            RebuffOnMissing = true,
            Target = "leader",
            Enabled = true
        });
    }

    private void RemoveBuffRule()
    {
        if (SelectedBuffRule != null)
            BuffRules.Remove(SelectedBuffRule);
    }

    public void UpdateAvailableSkills(IEnumerable<SkillInfo> skills)
    {
        var list = skills.Where(s => !s.IsPassive && !s.IsItemLike).OrderBy(s => s.SkillId)
            .Select(s => new SkillPickerItem(s.SkillId, s.Level)).ToList();

        if (list.Count == AvailableSkills.Count &&
            list.Zip(AvailableSkills).All(p => p.First == p.Second))
            return;

        var prevHeal = SelectedHealSkill?.SkillId;
        var prevBuff = SelectedBuffSkill?.SkillId;
        AvailableSkills.Clear();
        foreach (var s in list) AvailableSkills.Add(s);
        if (prevHeal.HasValue)
            SelectedHealSkill = AvailableSkills.FirstOrDefault(s => s.SkillId == prevHeal);
        if (prevBuff.HasValue)
            SelectedBuffSkill = AvailableSkills.FirstOrDefault(s => s.SkillId == prevBuff);
    }

    public void UpdatePartyMembers(IEnumerable<PartyMember> members, int partyLeaderObjectId)
    {
        var previousLeader = LeaderName;
        var previousAssist = AssistName;

        var list = members
            .Where(member => member.ObjectId != 0)
            .GroupBy(member => member.ObjectId)
            .Select(group => group.OrderByDescending(member => member.LastUpdateUtc).First())
            .OrderByDescending(member => member.ObjectId == partyLeaderObjectId)
            .ThenBy(member => string.IsNullOrWhiteSpace(member.Name) ? 1 : 0)
            .ThenBy(member => string.IsNullOrWhiteSpace(member.Name) ? member.ObjectId.ToString() : member.Name, StringComparer.OrdinalIgnoreCase)
            .Select(member => new PartyMemberPickerItem(
                member.ObjectId,
                BuildSelectionKey(member),
                BuildDisplay(member, member.ObjectId == partyLeaderObjectId),
                member.ObjectId == partyLeaderObjectId))
            .ToList();

        if (list.Count == AvailablePartyMembers.Count && list.Zip(AvailablePartyMembers).All(pair => pair.First == pair.Second))
        {
            ApplyDefaultLeaderSelectionIfNeeded();
            OnPropertyChanged(nameof(IsLeaderSelectionEnabled));
            return;
        }

        AvailablePartyMembers.Clear();
        foreach (var member in list)
            AvailablePartyMembers.Add(member);

        if (list.Any(member => member.SelectionKey == previousLeader))
            LeaderName = previousLeader;
        else if (!string.IsNullOrWhiteSpace(previousLeader) && !list.Any())
            LeaderName = previousLeader;
        else if (string.IsNullOrWhiteSpace(LeaderName))
            LeaderName = list.FirstOrDefault(member => member.IsServerLeader)?.SelectionKey ?? string.Empty;

        if (list.Any(member => member.SelectionKey == previousAssist))
            AssistName = previousAssist;
        else if (!string.IsNullOrWhiteSpace(previousAssist) && !list.Any())
            AssistName = previousAssist;
        else if (!list.Any(member => member.SelectionKey == AssistName))
            AssistName = string.Empty;

        ApplyDefaultLeaderSelectionIfNeeded();
        OnPropertyChanged(nameof(IsLeaderSelectionEnabled));
    }

    public void Refresh()
    {
        var p = _pm.Current.Party;
        Enabled = p.Enabled;
        Mode = p.Mode;
        LeaderName = p.LeaderName;
        AssistName = p.AssistName;
        FollowDistance = p.FollowDistance;
        RepathDistance = p.RepathDistance;
        PositionTimeoutMs = p.PositionTimeoutMs;

        HealRules.Clear();
        foreach (var r in p.HealRules) HealRules.Add(r);
        BuffRules.Clear();
        foreach (var r in p.BuffRules) BuffRules.Add(r);
    }

    public void ApplyToProfile()
    {
        var p = _pm.Current.Party;
        p.Enabled = Enabled;
        p.Mode = Mode;
        p.LeaderName = LeaderName?.Trim() ?? string.Empty;
        p.AssistName = AssistName?.Trim() ?? string.Empty;
        p.FollowDistance = FollowDistance;
        p.RepathDistance = RepathDistance;
        p.PositionTimeoutMs = PositionTimeoutMs;
        p.HealRules = new List<HealRule>(HealRules);
        p.BuffRules = new List<BuffEntry>(BuffRules);
    }

    private void ApplyDefaultLeaderSelectionIfNeeded()
    {
        if (!string.IsNullOrWhiteSpace(LeaderName) && AvailablePartyMembers.Any(member => member.SelectionKey == LeaderName))
            return;

        var preferredLeader = AvailablePartyMembers.FirstOrDefault(member => member.IsServerLeader)
            ?? AvailablePartyMembers.FirstOrDefault();
        if (preferredLeader != null)
            LeaderName = preferredLeader.SelectionKey;
    }

    private static bool IsManaSupportSkill(int skillId) => skillId is 1013 or 1126 or 1428;
    private static string BuildSelectionKey(PartyMember member) =>
        string.IsNullOrWhiteSpace(member.Name) ? $"obj:{member.ObjectId}" : member.Name;

    private static string BuildDisplay(PartyMember member, bool isServerLeader)
    {
        var baseLabel = string.IsNullOrWhiteSpace(member.Name)
            ? $"obj:{member.ObjectId}"
            : $"{member.Name} (obj:{member.ObjectId})";
        return isServerLeader ? $"{baseLabel} [leader]" : baseLabel;
    }
}


