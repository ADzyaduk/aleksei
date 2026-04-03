using System.Collections.ObjectModel;
using System.Windows.Input;
using Alexei.App.Infrastructure;
using Alexei.Core.Config;
using Alexei.Core.GameState;

namespace Alexei.App.ViewModels;

public sealed class BuffTabVM : ViewModelBase
{
    private readonly ProfileManager _pm;

    private bool _enabled; public bool Enabled { get => _enabled; set => SetField(ref _enabled, value); }

    public ObservableCollection<SkillPickerItem> AvailableSkills { get; } = new();
    private SkillPickerItem? _selectedSkill;
    public SkillPickerItem? SelectedSkill { get => _selectedSkill; set => SetField(ref _selectedSkill, value); }

    private BuffEntry? _selectedBuff;
    public BuffEntry? SelectedBuff { get => _selectedBuff; set => SetField(ref _selectedBuff, value); }

    public ObservableCollection<BuffEntry> BuffList { get; } = new();

    public ICommand AddBuffCommand { get; }
    public ICommand RemoveBuffCommand { get; }

    public BuffTabVM(ProfileManager pm)
    {
        _pm = pm;
        AddBuffCommand = new RelayCommand(AddBuff, () => SelectedSkill != null);
        RemoveBuffCommand = new RelayCommand(RemoveBuff, () => SelectedBuff != null);
    }

    private void AddBuff()
    {
        if (SelectedSkill == null) return;
        BuffList.Add(new BuffEntry
        {
            SkillId = SelectedSkill.SkillId,
            Level = SelectedSkill.Level,
            IntervalSec = 1200,
            RebuffOnMissing = true,
            Target = "self",
            Enabled = true
        });
    }

    private void RemoveBuff()
    {
        if (SelectedBuff != null)
            BuffList.Remove(SelectedBuff);
    }

    public void UpdateAvailableSkills(IEnumerable<SkillInfo> skills)
    {
        var list = skills.Where(s => !s.IsPassive && !s.IsItemLike).OrderBy(s => s.SkillId)
            .Select(s => new SkillPickerItem(s.SkillId, s.Level)).ToList();

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
        var b = _pm.Current.Buffs;
        Enabled = b.Enabled;
        BuffList.Clear();
        foreach (var e in b.List) BuffList.Add(e);
    }

    public void ApplyToProfile()
    {
        var b = _pm.Current.Buffs;
        b.Enabled = Enabled;
        b.List = new List<BuffEntry>(BuffList);
    }
}


