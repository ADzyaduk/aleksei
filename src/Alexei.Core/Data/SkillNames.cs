using System.Reflection;
using System.Text.Json;

namespace Alexei.Core.Data;

public static class SkillNames
{
    private static readonly Dictionary<int, string> _map = Load();

    private static Dictionary<int, string> Load()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream("Alexei.Core.Data.skills_en.json");
            if (stream == null) return new();
            var doc = JsonDocument.Parse(stream);
            var result = new Dictionary<int, string>();
            foreach (var prop in doc.RootElement.EnumerateObject())
                if (int.TryParse(prop.Name, out int id))
                    result[id] = prop.Value.GetString() ?? "";
            return result;
        }
        catch
        {
            return new();
        }
    }

    public static string Get(int skillId) =>
        _map.TryGetValue(skillId, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : $"Skill {skillId}";
}
