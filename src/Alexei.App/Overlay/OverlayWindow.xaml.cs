using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;

namespace Alexei.App.Overlay;

public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Make click-through (WS_EX_TRANSPARENT)
        var hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = Win32.GetWindowLongW(hwnd, Win32.GWL_EXSTYLE);
        Win32.SetWindowLongW(hwnd, Win32.GWL_EXSTYLE,
            exStyle | Win32.WS_EX_TRANSPARENT | Win32.WS_EX_TOOLWINDOW);
    }
}

public sealed class PctToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double pct = value is double d ? d : 0;
        double maxWidth = parameter is string s && double.TryParse(s, CultureInfo.InvariantCulture, out var mw) ? mw : 200;
        return Math.Max(0, Math.Min(maxWidth, maxWidth * pct / 100));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
