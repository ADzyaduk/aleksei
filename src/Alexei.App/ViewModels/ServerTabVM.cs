using System.Collections.ObjectModel;
using Alexei.App.Infrastructure;
using Alexei.Core.Config;

namespace Alexei.App.ViewModels;

public sealed class ServerTabVM : ViewModelBase
{
    private readonly ServerConfig _config;

    public ObservableCollection<ServerEntry> Servers { get; } = new();

    private ServerEntry? _selectedServer;
    public ServerEntry? SelectedServer
    {
        get => _selectedServer;
        set
        {
            if (SetField(ref _selectedServer, value) && value != null)
                _config.ActiveServerId = value.Id;
        }
    }

    public ServerTabVM(ServerConfig config)
    {
        _config = config;
        foreach (var s in config.Servers)
            Servers.Add(s);
        _selectedServer = config.ActiveServer;
    }
}
