using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Alexei.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm)
            vm.Log.Clear();
    }

    private void CopyLog_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm)
        {
            var text = string.Join("\n", vm.Log.Entries.Select(x => x.Display));
            if (!string.IsNullOrEmpty(text))
                Clipboard.SetText(text);
        }
    }
}

/// <summary>
/// Converts a percentage (0-100) to a width based on ConverterParameter (max width).
/// </summary>
public sealed class BarWidthConverter : IValueConverter
{
    public static readonly BarWidthConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double pct = value is double d ? d : 0;
        double maxWidth = parameter is string s && double.TryParse(s, out var mw) ? mw : 200;
        return Math.Max(0, Math.Min(maxWidth, maxWidth * pct / 100));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
