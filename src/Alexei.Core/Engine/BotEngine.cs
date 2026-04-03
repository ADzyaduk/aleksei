using Alexei.Core.Config;
using Alexei.Core.Diagnostics;
using Alexei.Core.Engine.Tasks;
using Alexei.Core.GameState;
using Alexei.Core.Proxy;
using Microsoft.Extensions.Logging;

namespace Alexei.Core.Engine;

public sealed class BotEngine
{
    private readonly GameWorld _world;
    private readonly ProfileManager _profileManager;
    private readonly ILogger? _logger;
    private readonly List<IBotTask> _tasks = new();
    private PacketSender? _sender;

    public bool IsRunning { get; private set; }
    public string CurrentPhase { get; private set; } = "Idle";

    public event Action<string>? PhaseChanged;

    public BotEngine(GameWorld world, ProfileManager profileManager, ILogger? logger = null, PacketEvidenceCollector? collector = null)
    {
        _world = world;
        _profileManager = profileManager;
        _logger = logger;

        _tasks.Add(new RecoveryTask());
        _tasks.Add(new AnchorTask());
        _tasks.Add(new AutoBuffTask());
        _tasks.Add(new PartyHealTask());
        _tasks.Add(new AutoHealTask());
        _tasks.Add(new AutoCombatTask(logger, collector));
        _tasks.Add(new AutoLootTask(collector));
    }

    public void SetSender(PacketSender sender) => _sender = sender;

    public void Start()
    {
        IsRunning = true;
        SetPhase("Running");
    }

    public void Stop()
    {
        IsRunning = false;
        _world.Me.AnchorSet = false;
        SetPhase("Stopped");
    }

    public async Task TickAsync(CancellationToken ct)
    {
        if (!IsRunning || _sender == null || !_world.IsConnected) return;
        if (_world.Me.ObjectId == 0) return;

        _world.CleanDeadNpcs(TimeSpan.FromSeconds(30));
        _world.CleanExpiredBuffs();

        var profile = _profileManager.Current;

        foreach (var task in _tasks)
        {
            if (!task.IsEnabled) continue;
            try
            {
                await task.ExecuteAsync(_world, _sender, profile, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Task {Task} failed", task.Name);
            }
        }
    }

    private void SetPhase(string phase)
    {
        CurrentPhase = phase;
        PhaseChanged?.Invoke(phase);
    }
}
