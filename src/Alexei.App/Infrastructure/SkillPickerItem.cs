using Alexei.Core.Data;

namespace Alexei.App.Infrastructure;

public record SkillPickerItem(int SkillId, int Level)
{
    public string Display => $"{SkillNames.Get(SkillId)} (lv{Level})";
}
