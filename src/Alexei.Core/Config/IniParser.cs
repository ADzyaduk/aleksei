namespace Alexei.Core.Config;

/// <summary>
/// Minimal INI file parser.
/// </summary>
public static class IniParser
{
    public static Dictionary<string, Dictionary<string, string>> Parse(string filePath)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(filePath)) return result;

        string currentSection = "";
        foreach (var rawLine in File.ReadAllLines(filePath))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#') || line.StartsWith(';'))
                continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentSection = line[1..^1].Trim();
                if (!result.ContainsKey(currentSection))
                    result[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                continue;
            }

            int eq = line.IndexOf('=');
            if (eq <= 0) continue;

            string key = line[..eq].Trim();
            string value = line[(eq + 1)..].Trim();

            if (!result.ContainsKey(currentSection))
                result[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            result[currentSection][key] = value;
        }

        return result;
    }

    public static void Write(string filePath, Dictionary<string, Dictionary<string, string>> data)
    {
        using var writer = new StreamWriter(filePath);
        foreach (var (section, kvps) in data)
        {
            writer.WriteLine($"[{section}]");
            foreach (var (key, value) in kvps)
                writer.WriteLine($"{key} = {value}");
            writer.WriteLine();
        }
    }
}
