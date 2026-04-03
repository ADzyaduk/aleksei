namespace Alexei.Core.Engine;

/// <summary>
/// Periodic timer that ticks BotEngine at a fixed interval.
/// </summary>
public sealed class GameLoop
{
    private readonly BotEngine _engine;
    private readonly TimeSpan _interval;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public GameLoop(BotEngine engine, TimeSpan? interval = null)
    {
        _engine = engine;
        _interval = interval ?? TimeSpan.FromMilliseconds(100);
    }

    public void Start()
    {
        if (_loopTask != null) return;
        _cts = new CancellationTokenSource();
        _loopTask = RunAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _loopTask = null;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                await _engine.TickAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception)
            {
                // Log but continue
            }
        }
    }
}
