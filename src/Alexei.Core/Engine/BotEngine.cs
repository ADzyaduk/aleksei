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
    private readonly AutoCombatTask _autoCombatTask;
    private PacketSender? _sender;
    private DateTime _anchorRefreshArmedAt = DateTime.MinValue;

    public bool IsRunning { get; private set; }
    public string CurrentPhase { get; private set; } = "Idle";

    public event Action<string>? PhaseChanged;

    public BotEngine(GameWorld world, ProfileManager profileManager, ILogger? logger = null, PacketEvidenceCollector? collector = null)
    {
        _world = world;
        _profileManager = profileManager;
        _logger = logger;
        _autoCombatTask = new AutoCombatTask(logger, collector);

        _tasks.Add(new RecoveryTask());
        _tasks.Add(new AnchorTask());
        _tasks.Add(new AutoBuffTask(collector));
        _tasks.Add(new PartyHealTask(collector));
        _tasks.Add(new AutoHealTask());
        _tasks.Add(new PartyBuffTask(collector));
        _tasks.Add(new PartyModeTask(collector));
        _tasks.Add(_autoCombatTask);
        _tasks.Add(new AutoLootTask(collector));
    }

    public void SetSender(PacketSender sender) => _sender = sender;

    public void Start()
    {
        foreach (var task in _tasks) task.ResetState(_world);

        if (_world.Me.ObjectId != 0)
        {
            _world.Me.AnchorX = _world.Me.X;
            _world.Me.AnchorY = _world.Me.Y;
            _world.Me.AnchorZ = _world.Me.Z;
            _world.Me.AnchorSet = true;
            _anchorRefreshArmedAt = DateTime.UtcNow;
        }
        else
        {
            _world.Me.AnchorSet = false;
            _world.Me.AnchorX = 0;
            _world.Me.AnchorY = 0;
            _world.Me.AnchorZ = 0;
            _anchorRefreshArmedAt = DateTime.MinValue;
        }

        IsRunning = true;
        SetPhase("Running");
    }

    public void Stop()
    {
        IsRunning = false;
        _anchorRefreshArmedAt = DateTime.MinValue;
        foreach (var task in _tasks) task.ResetState(_world);
        _world.Me.AnchorSet = false;
        _world.Me.AnchorX = 0;
        _world.Me.AnchorY = 0;
        _world.Me.AnchorZ = 0;
        SetPhase("Stopped");
    }

    public async Task TickAsync(CancellationToken ct)
    {
        if (!IsRunning || _sender == null || !_world.IsConnected) return;
        if (_world.Me.ObjectId == 0) return;

        if (_anchorRefreshArmedAt != DateTime.MinValue &&
            _world.LastSelfMoveEvidenceUtc.HasValue &&
            _world.LastSelfMoveEvidenceUtc.Value >= _anchorRefreshArmedAt)
        {
            _world.Me.AnchorX = _world.Me.X;
            _world.Me.AnchorY = _world.Me.Y;
            _world.Me.AnchorZ = _world.Me.Z;
            _world.Me.AnchorSet = true;
            _anchorRefreshArmedAt = DateTime.MinValue;
        }

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
