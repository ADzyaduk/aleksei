using System.Windows;
using System.Windows.Threading;

namespace Alexei.App.Overlay;

/// <summary>
/// Syncs overlay window position with L2 game window via FindWindow/GetWindowRect.
/// </summary>
public sealed class OverlayPositionService
{
    private readonly Window _overlay;
    private readonly DispatcherTimer _timer;
    private IntPtr _gameHwnd;
    private string _windowTitle = "Lineage II";

    public OverlayPositionService(Window overlay)
    {
        _overlay = overlay;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _timer.Tick += OnTick;
    }

    public void Start(string windowTitle = "Lineage II")
    {
        _windowTitle = windowTitle;
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    private void OnTick(object? sender, EventArgs e)
    {
        // Re-find window if lost
        if (_gameHwnd == IntPtr.Zero || !Win32.IsWindow(_gameHwnd))
        {
            _gameHwnd = Win32.FindWindowW(null, _windowTitle);
            if (_gameHwnd == IntPtr.Zero)
            {
                _overlay.Visibility = Visibility.Hidden;
                return;
            }
        }

        if (Win32.IsIconic(_gameHwnd))
        {
            _overlay.Visibility = Visibility.Hidden;
            return;
        }

        if (Win32.GetWindowRect(_gameHwnd, out var rect))
        {
            _overlay.Left = rect.Left + 10;
            _overlay.Top = rect.Top + 30;
            _overlay.Visibility = Visibility.Visible;
        }
    }
}
