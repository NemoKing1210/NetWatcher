using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NetWatcher.Utils;
using NetWatcher.ViewModels;

namespace NetWatcher;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly SettingsStore _settingsStore = new();
    private readonly DispatcherTimer _timer;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => _viewModel.Tick();

        Loaded += OnLoaded;
        Closing += OnClosing;

        _viewModel.Tick();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var settings = _settingsStore.Load().Window;
        if (settings is not null)
        {
            Width = Math.Max(settings.Width, MinWidth);
            Height = Math.Max(settings.Height, MinHeight);
            Left = settings.X;
            Top = settings.Y;
        }

        _timer.Start();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _timer.Stop();
        _settingsStore.Save(_settingsStore.Load() with
        {
            Window = new WindowSettings(ActualWidth, ActualHeight, Left, Top),
        });
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e) =>
        _viewModel.FilterText = FilterBox.Text;

    private void Pause_Click(object sender, RoutedEventArgs e) => _viewModel.TogglePause();

    private void Clear_Click(object sender, RoutedEventArgs e) => _viewModel.Clear();

    private void AllProcesses_Click(object sender, RoutedEventArgs e)
    {
        ProcessCombo.SelectedItem = null;
        ProcessList.SelectedItem = null;
        _viewModel.SelectedProcess = null;
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            FileName = "netwatcher.csv",
        };

        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.ExportCsv(dialog.FileName);
        }
    }

    private void CopyIp_Click(object sender, RoutedEventArgs e) => Copy(row => row.Ip);

    private void CopyDomain_Click(object sender, RoutedEventArgs e) => Copy(row => row.DomainDisplay);

    private void CopyRow_Click(object sender, RoutedEventArgs e) =>
        Copy(row => string.Join(
            '\t',
            row.Time,
            row.ProcessName,
            row.Pid.ToString(),
            row.DomainDisplay,
            row.Ip,
            row.Port.ToString(),
            row.ProtocolName,
            row.State,
            row.SourceName,
            row.LastActivity));

    private void Details_Click(object sender, RoutedEventArgs e)
    {
        if (ConnectionsGrid.SelectedItem is not ConnectionRowViewModel row)
        {
            return;
        }

        var record = row.Record;
        MessageBox.Show(
            this,
            $"Process: {row.ProcessName} ({row.Pid})\n" +
            $"Domain: {row.DomainDisplay} ({row.Binding.Origin})\n" +
            $"Remote: {record.RemoteAddress}:{record.RemotePort}\n" +
            $"Local port: {record.LocalPort}\n" +
            $"Protocol: {row.ProtocolName} / {row.State}\n" +
            $"Source: {row.SourceName}\n" +
            $"First seen: {record.FirstSeenUtc.ToLocalTime():O}\n" +
            $"Last seen: {record.LastSeenUtc.ToLocalTime():O}",
            "Connection details");
    }

    private void Copy(Func<ConnectionRowViewModel, string> selector)
    {
        if (ConnectionsGrid.SelectedItem is ConnectionRowViewModel row)
        {
            Clipboard.SetText(selector(row));
        }
    }
}
