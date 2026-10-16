using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AionMeter.App.Services;
using AionMeter.Core.Economy;

namespace AionMeter.App.Windows;

/// <summary>DaevaTrack: a small always-on-top box with the Kinah balances the server last sent.</summary>
public partial class KinahWindow : Window
{
    private static readonly Brush Up = new SolidColorBrush(Color.FromRgb(0x7C, 0xCF, 0x8A));
    private static readonly Brush Down = new SolidColorBrush(Color.FromRgb(0xE0, 0x7A, 0x6E));

    private readonly MeterService _meter;
    private int _shownVersion = -1;

    public KinahWindow(MeterService meter)
    {
        _meter = meter;
        InitializeComponent();
        Left = meter.Settings.KinahLeft;
        Top = meter.Settings.KinahTop;
        Refresh();
    }

    public void Refresh()
    {
        var version = _meter.Kinah.Version;
        if (version == _shownVersion) return;
        _shownVersion = version;
        Show(_meter.Kinah.Get(Currency.Kinah), KinahAmount, KinahChange);
        Show(_meter.Kinah.Get(Currency.BoundKinah), BoundAmount, BoundChange);
    }

    private static void Show((long Amount, long Change)? value, TextBlock amount, TextBlock change)
    {
        if (value is not { } v)
        {
            amount.Text = UiText.Current.KinahWaiting;
            amount.FontWeight = FontWeights.Normal;
            change.Text = "";
            return;
        }
        amount.Text = v.Amount.ToString("#,0");
        amount.FontWeight = FontWeights.SemiBold;
        change.Text = v.Change == 0 ? "" : v.Change.ToString("+#,0;-#,0");
        change.Foreground = v.Change > 0 ? Up : Down;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
        _meter.Settings.KinahLeft = Left;
        _meter.Settings.KinahTop = Top;
        _meter.Settings.Save();
    }
}
