using System.Collections.ObjectModel;
using Alexei.App.Infrastructure;

namespace Alexei.App.ViewModels;

public sealed class LogEntry
{
    public DateTime Time { get; set; }
    public string Level { get; set; } = "";
    public string Message { get; set; } = "";
    public string Display => $"[{Time:HH:mm:ss}] [{Level}] {Message}";
}

public sealed class LogTabVM : ViewModelBase
{
    private const int MaxEntries = 2000;

    public ObservableCollection<LogEntry> Entries { get; } = new();

    private string _filterLevel = "ALL";
    public string FilterLevel
    {
        get => _filterLevel;
        set => SetField(ref _filterLevel, value);
    }

    private bool _showPackets = false;
    public bool ShowPackets
    {
        get => _showPackets;
        set => SetField(ref _showPackets, value);
    }

    public void Add(string level, string message)
    {
        if (level == "PACKET" && !ShowPackets) return;
        if (FilterLevel != "ALL" && level != FilterLevel && level != "ERROR") return;

        Entries.Add(new LogEntry
        {
            Time = DateTime.Now,
            Level = level,
            Message = message
        });

        while (Entries.Count > MaxEntries)
            Entries.RemoveAt(0);
    }

    public void Clear() => Entries.Clear();
}
