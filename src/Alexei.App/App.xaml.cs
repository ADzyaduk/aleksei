using System.IO;
using System.Windows;
using Alexei.App.Overlay;
using Alexei.App.ViewModels;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

namespace Alexei.App;

public partial class App : Application
{
    public static ILoggerFactory LoggerFactory { get; private set; } = null!;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var errorLog = Path.Combine(baseDir, "crash.log");
        var logsDir = Path.Combine(baseDir, "logs");
        Directory.CreateDirectory(logsDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(logsDir, "alexei-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        LoggerFactory = new SerilogLoggerFactory(Log.Logger);

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Fatal(args.Exception, "Unhandled UI exception");
            File.AppendAllText(errorLog, $"[UNHANDLED] {DateTime.Now}\n{args.Exception}\n\n");
            args.Handled = true;
        };

        try
        {
            var configDir = Path.Combine(baseDir, "config");
            if (!Directory.Exists(configDir))
                Directory.CreateDirectory(configDir);

            var vm = new MainViewModel(configDir);

            var mainWindow = new MainWindow { DataContext = vm };

            // Overlay window — transparent topmost widget synced to L2 window
            var overlay = new OverlayWindow { DataContext = vm.Overlay };
            var posService = new OverlayPositionService(overlay);

            // Close overlay and stop services when main window closes
            mainWindow.Closed += (_, _) =>
            {
                posService.Stop();
                overlay.Close();
            };

            mainWindow.Show();
            overlay.Show();
            posService.Start();
        }
        catch (Exception ex)
        {
            File.AppendAllText(errorLog, $"[STARTUP] {DateTime.Now}\n{ex}\n\n");
        }
    }
}
