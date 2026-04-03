namespace Alexei.Core.Config;

public sealed class ServerEntry
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string LoginHost { get; set; } = "127.0.0.1";
    public int LoginPort { get; set; } = 2106;
    public string GameHost { get; set; } = "127.0.0.1";
    public int GamePort { get; set; } = 7777;
    public string L2Path { get; set; } = "";
    public string Chronicle { get; set; } = "interlude";
    public bool OpcodeScramble { get; set; } = true;
    public string SkillFormat { get; set; } = "ddd";
    /// <summary>BlowfishInit opcode byte. Teon=0x00, Bartz kamael-like=0x2E.</summary>
    public byte GameKeyOpcode { get; set; } = 0x00;
}

public sealed class ServerConfig
{
    public string ActiveServerId { get; set; } = "";
    public List<ServerEntry> Servers { get; } = new();
    public int ProxyLoginPort { get; set; } = 2106;
    public int ProxyGamePort { get; set; } = 7777;

    public ServerEntry? ActiveServer => Servers.Find(s => s.Id == ActiveServerId);

    public static ServerConfig Load(string iniPath)
    {
        var data = IniParser.Parse(iniPath);
        var cfg = new ServerConfig();

        if (data.TryGetValue("general", out var general))
        {
            if (general.TryGetValue("active_server", out var active))
                cfg.ActiveServerId = active;
            if (general.TryGetValue("proxy_login_port", out var plp) && int.TryParse(plp, out var plpVal))
                cfg.ProxyLoginPort = plpVal;
            if (general.TryGetValue("proxy_game_port", out var pgp) && int.TryParse(pgp, out var pgpVal))
                cfg.ProxyGamePort = pgpVal;
        }

        foreach (var (section, kvps) in data)
        {
            if (section == "general") continue;

            var entry = new ServerEntry { Id = section };
            if (kvps.TryGetValue("name", out var name)) entry.Name = name;
            else entry.Name = section;
            if (kvps.TryGetValue("login_host", out var lh)) entry.LoginHost = lh;
            if (kvps.TryGetValue("login_port", out var lp) && int.TryParse(lp, out var lpVal)) entry.LoginPort = lpVal;
            if (kvps.TryGetValue("game_host", out var gh)) entry.GameHost = gh;
            if (kvps.TryGetValue("game_port", out var gp) && int.TryParse(gp, out var gpVal)) entry.GamePort = gpVal;
            if (kvps.TryGetValue("l2_path", out var l2)) entry.L2Path = l2;
            if (kvps.TryGetValue("chronicle", out var chr)) entry.Chronicle = chr;
            if (kvps.TryGetValue("opcode_scramble", out var os)) entry.OpcodeScramble = os.ToLower() == "true";
            if (kvps.TryGetValue("skill_format", out var sf)) entry.SkillFormat = sf;
            if (kvps.TryGetValue("game_key_opcode", out var gko) &&
                byte.TryParse(gko, System.Globalization.NumberStyles.HexNumber, null, out var gkoVal))
                entry.GameKeyOpcode = gkoVal;

            ApplyKnownServerDefaults(entry);

            cfg.Servers.Add(entry);
        }

        return cfg;
    }

    public void Save(string iniPath)
    {
        var data = new Dictionary<string, Dictionary<string, string>>();
        data["general"] = new()
        {
            ["active_server"] = ActiveServerId,
            ["proxy_login_port"] = ProxyLoginPort.ToString(),
            ["proxy_game_port"] = ProxyGamePort.ToString()
        };

        foreach (var s in Servers)
        {
            ApplyKnownServerDefaults(s);
            data[s.Id] = new()
            {
                ["name"] = s.Name,
                ["login_host"] = s.LoginHost,
                ["login_port"] = s.LoginPort.ToString(),
                ["game_host"] = s.GameHost,
                ["game_port"] = s.GamePort.ToString(),
                ["l2_path"] = s.L2Path,
                ["chronicle"] = s.Chronicle,
                ["opcode_scramble"] = s.OpcodeScramble.ToString().ToLower(),
                ["skill_format"] = s.SkillFormat,
                ["game_key_opcode"] = s.GameKeyOpcode.ToString("X2")
            };
        }

        IniParser.Write(iniPath, data);
    }

    private static void ApplyKnownServerDefaults(ServerEntry entry)
    {
        if (!string.Equals(entry.Id, "bartz", StringComparison.OrdinalIgnoreCase))
            return;

        if (string.IsNullOrWhiteSpace(entry.Chronicle) || string.Equals(entry.Chronicle, "interlude", StringComparison.OrdinalIgnoreCase))
            entry.Chronicle = "kamael-like";

        if (entry.GameKeyOpcode == 0x00)
            entry.GameKeyOpcode = 0x2E;

        if (string.IsNullOrWhiteSpace(entry.SkillFormat) || string.Equals(entry.SkillFormat, "ddd", StringComparison.OrdinalIgnoreCase))
            entry.SkillFormat = "dcb";
    }
}
