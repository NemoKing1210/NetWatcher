using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NetWatcher.ViewModels;
using Wpf.Ui.Controls;

namespace NetWatcher.Views;

/// <summary>
/// The live connection table with its filter, pause, clear, CSV export, and
/// row context menu. One instance lives for the whole session so the table's
/// sort, selection and scroll position survive page switches.
/// </summary>
public partial class ConnectionsPage : UserControl
{
    private readonly MainViewModel _viewModel;

    public ConnectionsPage(MainViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        UpdatePauseVisual();
        UpdateBanner();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.Paused):
                UpdatePauseVisual();
                break;
            case nameof(MainViewModel.EtwAvailable):
                UpdateBanner();
                break;
        }
    }

    private void UpdatePauseVisual()
    {
        var paused = _viewModel.Paused;
        PauseButton.Content = paused ? "Resume" : "Pause";
        PauseButton.Icon = new SymbolIcon { Symbol = paused ? SymbolRegular.Play24 : SymbolRegular.Pause24 };
        PauseButton.Appearance = paused ? ControlAppearance.Primary : ControlAppearance.Secondary;
    }

    private void UpdateBanner() => EtwBanner.IsOpen = !_viewModel.EtwAvailable;

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e) =>
        _viewModel.FilterText = FilterBox.Text;

    private void Pause_Click(object sender, RoutedEventArgs e) => _viewModel.TogglePause();

    private void Clear_Click(object sender, RoutedEventArgs e) => _viewModel.Clear();

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            FileName = "netwatcher.csv",
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            _viewModel.ExportCsv(dialog.FileName);
        }
    }

    /// <summary>
    /// Right-clicking a row must act on that row, not on whatever was selected
    /// before. DataGrid does not move the selection for a right click.
    /// </summary>
    private void ConnectionsGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source &&
            ItemsControl.ContainerFromElement(ConnectionsGrid, source) is DataGridRow row)
        {
            ConnectionsGrid.SelectedItem = row.Item;
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

    private async void Details_Click(object sender, RoutedEventArgs e)
    {
        if (ConnectionsGrid.SelectedItem is not ConnectionRowViewModel row)
        {
            return;
        }

        var record = row.Record;
        var dialog = new Wpf.Ui.Controls.MessageBox
        {
            Owner = Window.GetWindow(this),
            Title = "Connection details",
            CloseButtonText = "Close",
            Content =
                $"Process: {row.ProcessName} ({row.Pid})\n" +
                $"Domain: {row.DomainDisplay} ({row.Binding.Origin})\n" +
                $"Remote: {record.RemoteAddress}:{record.RemotePort}\n" +
                $"Local port: {record.LocalPort}\n" +
                $"Protocol: {row.ProtocolName} / {row.State}\n" +
                $"Source: {row.SourceName}\n" +
                $"First seen: {record.FirstSeenUtc.ToLocalTime():O}\n" +
                $"Last seen: {record.LastSeenUtc.ToLocalTime():O}",
        };

        await dialog.ShowDialogAsync();
    }

    private void Copy(Func<ConnectionRowViewModel, string> selector)
    {
        if (ConnectionsGrid.SelectedItem is ConnectionRowViewModel row)
        {
            Clipboard.SetText(selector(row));
        }
    }
}
