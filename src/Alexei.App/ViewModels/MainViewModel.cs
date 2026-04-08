using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Alexei.App.Infrastructure;
using Alexei.Core.Config;
using Alexei.Core.Diagnostics;
using Alexei.Core.Engine;
using Alexei.Core.GameState;
using Alexei.Core.Protocol;
using Alexei.Core.Protocol.Handlers;
using Alexei.Core.Proxy;
using Microsoft.Extensions.Logging;

namespace Alexei.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly GameWorld _world;
    private readonly ServerConfig _serverConfig;
    private readonly ProfileManager _profileManager;
    private readonly string _configPath;
    private readonly DispatcherTimer _uiTimer;

    private LoginProxy? _loginProxy;
    private GameProxy? _gameProxy;
    private BotEngine? _botEngine;
    private GameLoop? _gameLoop;
    private CancellationTokenSource? _proxyCts;
    private bool _profileLoaded;
    private PacketEvidenceCollector? _packetCollector;

    // Sub-ViewModels
    public ServerTabVM Server { get; }
    public CharInfoTabVM CharInfo { get; }
    public CombatTabVM Combat { get; }
    public BuffTabVM Buffs { get; }
    public PartyTabVM Party { get; }
    public LootTabVM Loot { get; }
    public RecoveryTabVM Recovery { get; }
    public MonsterTabVM Monsters { get; }
    public LogTabVM Log { get; }
    public OverlayViewModel Overlay { get; }

    // State
    private bool _isProxyRunning;
    public bool IsProxyRunning { get => _isProxyRunning; set => SetField(ref _isProxyRunning, value); }

    private bool _isBotRunning;
    public bool IsBotRunning { get => _isBotRunning; set => SetField(ref _isBotRunning, value); }

    private bool _isSenderReady;
    public bool IsSenderReady { get => _isSenderReady; set => SetField(ref _isSenderReady, value); }

    private string _statusText = "Disconnected";
    public string StatusText { get => _statusText; set => SetField(ref _statusText, value); }

    private string _charName = "";
    public string CharName { get => _charName; set => SetField(ref _charName, value); }

    // Commands
    public ICommand StartProxyCommand { get; }
    public ICommand StopProxyCommand { get; }
    public ICommand StartBotCommand { get; }
    public ICommand StopBotCommand { get; }
    public ICommand SaveProfileCommand { get; }
    public ICommand TestSitCommand { get; }

    public MainViewModel(string configDir)
    {
        _configPath = configDir;
        var iniPath = Path.Combine(configDir, "servers.ini");
        _serverConfig = File.Exists(iniPath) ? ServerConfig.Load(iniPath) : CreateDefaultConfig(iniPath);

        _profileManager = new ProfileManager(Path.Combine(configDir, "profiles"));
        _world = new GameWorld();

        // Init sub-VMs
        Server = new ServerTabVM(_serverConfig);
        CharInfo = new CharInfoTabVM();
        Combat = new CombatTabVM(_profileManager);
        Buffs = new BuffTabVM(_profileManager);
        Party = new PartyTabVM(_profileManager);
        Loot = new LootTabVM(_profileManager);
        Recovery = new RecoveryTabVM(_profileManager);
        Monsters = new MonsterTabVM();
        Log = new LogTabVM();
        Overlay = new OverlayViewModel();

        // Commands
        StartProxyCommand = new RelayCommand(StartProxy, () => !IsProxyRunning);
        StopProxyCommand = new RelayCommand(StopProxy, () => IsProxyRunning);
        StartBotCommand = new RelayCommand(StartBot, () => IsSenderReady && !IsBotRunning);
        StopBotCommand = new RelayCommand(StopBot, () => IsBotRunning);
        SaveProfileCommand = new RelayCommand(SaveProfile);
        TestSitCommand = new RelayCommand(TestSit, () => IsSenderReady);

        // Polling timer for UI updates (like Python bot's tkinter.after(400ms))
        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _uiTimer.Tick += (_, _) => PollWorldState();
        _uiTimer.Start();

        InitializeDefaultServerAndProxy();
    }

    private void InitializeDefaultServerAndProxy()
    {
        var bartz = Server.Servers.FirstOrDefault(s => string.Equals(s.Id, "bartz", StringComparison.OrdinalIgnoreCase));
        if (bartz != null)
        {
            Server.SelectedServer = bartz;
            _serverConfig.ActiveServerId = bartz.Id;
            _serverConfig.Save(Path.Combine(_configPath, "servers.ini"));
        }

        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!IsProxyRunning)
                StartProxy();
        }), DispatcherPriority.Background);
    }

    /// <summary>
    /// Polling-based UI update — reads world state directly, no events needed.
    /// Same pattern as Python bot's tab._update() with tkinter.after(400).
    /// </summary>
    private void PollWorldState()
    {
        if (!_world.IsConnected && _world.Me.ObjectId == 0) return;

        // Update character info
        CharInfo.UpdateFromWorld(_world);
        Overlay.UpdateFromWorld(_world);
        Overlay.BotStatus = _botEngine?.IsRunning == true ? _botEngine.CurrentPhase : "Idle";
        Overlay.IsVisible = _world.IsConnected;

        // Update monsters every other tick (less frequent)
        Monsters.UpdateFromWorld(_world);

        Party.UpdatePartyMembers(_world.Party.Values, _world.PartyLeaderObjectId);

        // Push skill list to pickers when skills are available
        if (_world.Skills.Count > 0)
        {
            Combat.UpdateAvailableSkills(_world.Skills.Values);
            Buffs.UpdateAvailableSkills(_world.Skills.Values);
            Party.UpdateAvailableSkills(_world.Skills.Values);
        }

        // Load profile on first UserInfo
        if (!_profileLoaded && _world.Me.ObjectId != 0 && !string.IsNullOrEmpty(_world.Me.Name))
        {
            _profileLoaded = true;
            _profileManager.LoadForCharacter(_world.Me.Name, _serverConfig.ActiveServer?.Id);
            CharName = _world.Me.Name;
            Combat.Refresh();
            Buffs.Refresh();
            Party.Refresh();
            Loot.Refresh();
            Recovery.Refresh();
            Log.Add("INFO", $"Profile loaded for {_world.Me.Name}");

            // Fix up server-specific defaults (e.g. Bartz needs UseTargetEnter + 39dcb skill packet)
            var activeSrv = _serverConfig.ActiveServer;
            if (activeSrv != null && _profileManager.ApplyServerDefaults(activeSrv.Id))
            {
                _profileManager.Save();
                Log.Add("INFO", $"Applied {activeSrv.Id} profile defaults");
            }
        }
    }

    private void StartProxy()
    {
        var server = _serverConfig.ActiveServer;
        if (server == null) { Log.Add("ERROR", "No active server configured"); return; }

        _proxyCts = new CancellationTokenSource();
        _profileLoaded = false;

        // Setup dispatcher
        var detector = new OpcodeDetector();
        var dispatcherLogger = App.LoggerFactory.CreateLogger<PacketDispatcher>();
        var dispatcher = new PacketDispatcher(detector, _world, server.OpcodeScramble, dispatcherLogger);
        _packetCollector = CreatePacketCollector(server);

        // Register all handlers
        dispatcher.Register(new UserInfoHandler());
        dispatcher.Register(new NpcInfoHandler());
        dispatcher.Register(new StatusUpdateHandler());
        dispatcher.Register(new MoveToPointHandler());
        dispatcher.Register(new TargetSelectedHandler());
        dispatcher.Register(new DieHandler());
        dispatcher.Register(new SpawnItemHandler());
        dispatcher.Register(new DeleteObjectHandler());
        dispatcher.Register(new SkillListHandler());
        dispatcher.Register(new SkillCoolTimeHandler());
        dispatcher.Register(new AbnormalStatusHandler());
        dispatcher.Register(new ChangeWaitTypeHandler());
        dispatcher.Register(new AttackHandler());
        dispatcher.Register(new MagicSkillLaunchedHandler());
        dispatcher.Register(new PartySmallWindowAllHandler());
        dispatcher.Register(new PartySmallWindowAddHandler());
        dispatcher.Register(new PartySmallWindowDeleteHandler());
        dispatcher.Register(new PartySmallWindowUpdateHandler());
        dispatcher.Register(new ValidatePositionHandler());
        dispatcher.Register(new StopMoveHandler());
        dispatcher.Register(new Die2Handler());
        dispatcher.Register(new StatusUpdate2Handler());
        dispatcher.Register(new SystemMessageHandler());

        // Bartz uses different base opcodes (XOR key 0x1A detected per-session).
        // Mapping: wire ^ 0x1A = base. Identified from unknown-packet logs.
        //   0x0B→0x11: UserInfo (with Title string)
        //   0x11→0x0B: SkillList
        //   0x18→0x02: target/NPC status update (HP/MP/CP pairs)
        //   0x27→0x3D: target selected/state
        //   0x2F→0x35: MoveToPoint
        //   0x62→0x78: ChangeWaitType
        //   0x86→0x9C: StatusUpdate
        //   0xB9→0xA3: target-related companion packet
        if (server.Id == "bartz")
        {
            dispatcher.Register(new BartZUserInfoHandler(), 0x11);
            dispatcher.Register(new BartZSkillListHandler(), 0x45);
            dispatcher.Register(new StatusUpdateHandler(),  0x02);
            dispatcher.Register(new TargetSelectedHandler(),0x3D);
            dispatcher.Register(new MoveToPointHandler(),   0x35);
            dispatcher.Register(new ChangeWaitTypeHandler(),0x78);
            dispatcher.Register(new StatusUpdateHandler(),  0x9C);
            dispatcher.Register(new BartzTargetStatusHandler(), 0xA3);
        }

        if (_packetCollector != null)
        {
            dispatcher.PacketObserved += (observation, payload) => _packetCollector.Record(observation, payload);
        }

        // Packet logging — unknown packets always shown (limited), known only when ShowPackets is on
        int unknownUiCount = 0;
        dispatcher.PacketReceived += (opcode, payload, handler) =>
        {
            bool isUnknown = handler != null && handler.StartsWith("?hex=");
            if (isUnknown && unknownUiCount++ < 30)
            {
                Application.Current?.Dispatcher.BeginInvoke(() =>
                    Log.Add("UNK", $"S2C base=0x{opcode:X2} len={payload.Length} {handler}"));
                return;
            }

            if (Log.ShowPackets && !isUnknown)
            {
                string extra = "";
                if (opcode == Alexei.Core.Protocol.Opcodes.GameS2C.SystemMessage && payload.Length >= 4)
                {
                    int msgId = BitConverter.ToInt32(payload, 0);
                    extra = msgId switch
                    {
                        612 => " [SpoilSuccess]",
                        357 => " [AlreadySpoiled]",
                        _ => $" [msg={msgId}]"
                    };
                }
                Application.Current?.Dispatcher.BeginInvoke(() =>
                    Log.Add("PACKET", $"S2C 0x{opcode:X2} [{payload.Length}B] {handler ?? "unhandled"}{extra}"));
            }
        };

        // Opcode detection event
        detector.Detected += key =>
        {
            _world.IsOpcodeDetected = true;
            _world.OpcodeXorKey = key;
            // Pre-detection packets were misrouted: SpawnItemHandler parsed NpcInfo packets
            // as items, creating phantom entries. Clear them so AutoLootTask doesn't chase ghosts.
            _world.Items.Clear();
            Application.Current?.Dispatcher.BeginInvoke(() =>
                Log.Add("INFO", $"Opcode XOR key detected: 0x{key:X2}"));
        };

        // Login proxy
        _loginProxy = new LoginProxy(server.LoginHost, server.LoginPort,
            _serverConfig.ProxyLoginPort, _serverConfig.ProxyGamePort);
        _loginProxy.Log += msg => Application.Current?.Dispatcher.BeginInvoke(() => Log.Add("LOGIN", msg));

        // Game proxy
        _gameProxy = new GameProxy(server, _serverConfig.ProxyGamePort, _world, dispatcher, collector: _packetCollector);
        _gameProxy.Log += msg => Application.Current?.Dispatcher.BeginInvoke(() => Log.Add("GAME", msg));

        // Wire dynamic game server discovery: LoginProxy → GameProxy
        _loginProxy.GameServerDiscovered += (host, port) => _gameProxy.UpdateEndpoint(host, port);

        // Bot engine
        _botEngine = new BotEngine(_world, _profileManager, App.LoggerFactory.CreateLogger<BotEngine>(), _packetCollector);
        _gameLoop = new GameLoop(_botEngine);

        // Wire sender from proxy session → bot engine (fired when BlowfishInit received)
        _gameProxy.SenderReady += sender =>
        {
            _botEngine.SetSender(sender);
            sender.PacketSent += (opcode, len) =>
            {
                Application.Current?.Dispatcher.BeginInvoke(() =>
                    Log.Add("SEND", $"C2S inject: 0x{opcode:X2} [{len}B]"));
            };
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                IsSenderReady = true;
                StatusText = "Game ready — sender initialized";
                Log.Add("INFO", "PacketSender ready — bot can start");
            });
        };

        _gameProxy.ClientConnected += () =>
        {
            Application.Current?.Dispatcher.BeginInvoke(() => StatusText = "Game client connected...");
        };
        _gameProxy.ClientDisconnected += () =>
        {
            if (_packetCollector != null)
            {
                string reportPath = _packetCollector.FlushReport();
                Application.Current?.Dispatcher.BeginInvoke(() =>
                    Log.Add("INFO", $"Bartz packet report saved: {reportPath}"));
            }
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                StatusText = "Game disconnected";
                IsBotRunning = false;
                IsSenderReady = false;
                _profileLoaded = false;
            });
        };

        _ = _loginProxy.StartAsync(_proxyCts.Token);
        _ = _gameProxy.StartAsync(_proxyCts.Token);

        IsProxyRunning = true;
        StatusText = $"Proxy running — {server.Name}";
        Log.Add("INFO", $"Proxy started for {server.Name} ({server.LoginHost}:{server.LoginPort})");
    }

    private void StopProxy()
    {
        StopBot();
        _proxyCts?.Cancel();
        _loginProxy?.Stop();
        _gameProxy?.Stop();
        IsProxyRunning = false;
        IsSenderReady = false;
        StatusText = "Disconnected";
        Log.Add("INFO", "Proxy stopped");
    }

    private static PacketEvidenceCollector? CreatePacketCollector(ServerEntry server)
    {
        if (!string.Equals(server.Id, "bartz", StringComparison.OrdinalIgnoreCase))
            return null;

        string chronicle = string.IsNullOrWhiteSpace(server.Chronicle) || server.Chronicle.Equals("interlude", StringComparison.OrdinalIgnoreCase)
            ? "kamael-like"
            : server.Chronicle;
        string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        string root = Path.Combine(AppContext.BaseDirectory, "captures", "bartz-investigation", stamp);
        return new PacketEvidenceCollector(root, server.Id, chronicle);
    }

    private void StartBot()
    {
        if (_botEngine == null) return;
        ApplyViewModelsToProfile(saveToDisk: false);
        _botEngine.Start();
        _gameLoop?.Start();
        IsBotRunning = true;
        Log.Add("INFO", $"Bot started (party={_profileManager.Current.Party.Mode}, enabled={_profileManager.Current.Party.Enabled}, leader={_profileManager.Current.Party.LeaderName}, assist={_profileManager.Current.Party.AssistName})");
    }

    private void StopBot()
    {
        _botEngine?.Stop();
        _gameLoop?.Stop();
        IsBotRunning = false;
    }

    private void TestSit()
    {
        var sender = _gameProxy?.Sender;
        if (sender == null) { Log.Add("ERROR", "No sender available"); return; }

        Log.Add("INFO", "Sending Sit/Stand (ActionUse actionId=0)...");
        _ = Task.Run(async () =>
        {
            try
            {
                await sender.SendAsync(GamePackets.ActionUse(0));
                Application.Current?.Dispatcher.BeginInvoke(() =>
                    Log.Add("INFO", "Sit/Stand packet sent successfully"));
            }
            catch (Exception ex)
            {
                Application.Current?.Dispatcher.BeginInvoke(() =>
                    Log.Add("ERROR", $"Sit/Stand send failed: {ex.Message}"));
            }
        });
    }

    private void SaveProfile()
    {
        ApplyViewModelsToProfile(saveToDisk: true);
        Log.Add("INFO", "Profile saved");
    }

    private void ApplyViewModelsToProfile(bool saveToDisk)
    {
        Combat.ApplyToProfile();
        Buffs.ApplyToProfile();
        Party.ApplyToProfile();
        Loot.ApplyToProfile();
        Recovery.ApplyToProfile();

        if (saveToDisk)
            _profileManager.Save();
    }

    private static ServerConfig CreateDefaultConfig(string path)
    {
        var cfg = new ServerConfig { ActiveServerId = "teon" };
        cfg.Servers.Add(new ServerEntry
        {
            Id = "teon",
            Name = "Teon (Elmorelab)",
            LoginHost = "51.38.238.76",
            LoginPort = 2106,
            GameHost = "51.38.238.76",
            GamePort = 7777,
            L2Path = "C:/Users/KaZa/Downloads/L2IL_Teon/system/L2.exe",
            OpcodeScramble = true,
            SkillFormat = "ddd"
        });
        cfg.Servers.Add(new ServerEntry
        {
            Id = "bartz",
            Name = "Bartz",
            LoginHost = "87.98.152.71",
            LoginPort = 2106,
            GameHost = "87.98.152.71",
            GamePort = 7777,
            L2Path = "C:/Users/KaZa/Downloads/L2CT_Lionna/system/L2.exe",
            OpcodeScramble = true,
            // Bartz uses 0x2E BlowfishInit opcode, 0x39 dcb skill packets, 0x1F TargetEnter.
            // Set combatSkillPacket="39dcb" and useTargetEnter=true in the character profile JSON.
            SkillFormat = "dcb",
            GameKeyOpcode = 0x2E
        });
        cfg.Save(path);
        return cfg;
    }
}



