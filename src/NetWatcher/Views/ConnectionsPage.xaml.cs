using System.Windows;
using System.Windows.Controls;
using NetWatcher.ViewModels;

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
    }

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
