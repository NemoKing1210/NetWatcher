using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using NetWatcher.Localization;
using NetWatcher.Models;
using NetWatcher.Services.Export;
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

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdatePauseVisual();
        UpdateBanner();
        UpdateProcessColumn();
    }

    /// <summary>Below this width the filter row and the buttons no longer share one line.</summary>
    private const double StackedToolbarWidth = 880;

    private void Toolbar_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var stacked = e.NewSize.Width < StackedToolbarWidth;
        if (stacked == (Grid.GetRow(ActionBar) == 1))
        {
            return;
        }

        if (stacked)
        {
            Grid.SetRow(ActionBar, 1);
            Grid.SetColumn(ActionBar, 0);
            Grid.SetColumnSpan(ActionBar, 2);
            ActionBar.HorizontalAlignment = HorizontalAlignment.Right;
            ActionBar.Margin = new Thickness(0, 8, 0, 0);

            Grid.SetColumnSpan(FilterBar, 2);
            FilterBar.Margin = new Thickness(0);
        }
        else
        {
            Grid.SetRow(ActionBar, 0);
            Grid.SetColumn(ActionBar, 1);
            Grid.SetColumnSpan(ActionBar, 1);
            ActionBar.HorizontalAlignment = HorizontalAlignment.Stretch;
            ActionBar.Margin = new Thickness(0);

            Grid.SetColumnSpan(FilterBar, 1);
            FilterBar.Margin = new Thickness(0, 0, 12, 0);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Detach();

    /// <summary>Stops observing the page's view model; the page may outlive the window during a language rebuild.</summary>
    internal void Detach() => _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

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
            case nameof(MainViewModel.SelectedProcess):
                UpdateProcessColumn();
                break;
        }
    }

    /// <summary>
    /// A focused process makes the Process column redundant, so it is removed
    /// from the table entirely; "All processes" brings it back.
    /// </summary>
    private void UpdateProcessColumn() =>
        ProcessColumn.Visibility = _viewModel.SelectedProcess?.IsAll == false
            ? Visibility.Collapsed
            : Visibility.Visible;

    private void UpdatePauseVisual()
    {
        var paused = _viewModel.Paused;
        var label = paused ? Strings.Resume : Strings.Pause;
        PauseText.Text = label;
        AutomationProperties.SetName(PauseButton, label);
        PauseIcon.Symbol = paused ? SymbolRegular.Play24 : SymbolRegular.Pause24;
        PauseButton.Appearance = paused ? ControlAppearance.Primary : ControlAppearance.Secondary;
    }

    private void UpdateBanner() => EtwBanner.IsOpen = !_viewModel.EtwAvailable;

    private void Pause_Click(object sender, RoutedEventArgs e) => _viewModel.TogglePause();

    private void Clear_Click(object sender, RoutedEventArgs e) => _viewModel.Clear();

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement button || button.ContextMenu is not { } menu)
        {
            return;
        }

        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e) => Export(ExportFormat.Csv);

    private void ExportMarkdown_Click(object sender, RoutedEventArgs e) => Export(ExportFormat.Markdown);

    private void ExportJson_Click(object sender, RoutedEventArgs e) => Export(ExportFormat.Json);

    private void Export(ExportFormat format)
    {
        var (filter, fileName) = format switch
        {
            ExportFormat.Markdown => (Strings.MarkdownFileFilter, "netwatcher.md"),
            ExportFormat.Json => (Strings.JsonFileFilter, "netwatcher.json"),
            _ => (Strings.CsvFileFilter, "netwatcher.csv"),
        };

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = Strings.Export,
            Filter = filter,
            FileName = fileName,
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            _viewModel.Export(dialog.FileName, format);
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
            Title = Strings.ConnectionDetails,
            CloseButtonText = Strings.Close,
            Content =
                $"{Strings.DetailsProcess}: {row.ProcessName} ({row.Pid})\n" +
                $"{Strings.DetailsDomain}: {row.DomainDisplay} ({OriginName(row.Binding.Origin)})\n" +
                $"{Strings.DetailsRemote}: {record.RemoteAddress}:{record.RemotePort}\n" +
                $"{Strings.DetailsLocalPort}: {record.LocalPort}\n" +
                $"{Strings.DetailsProtocol}: {row.ProtocolName} / {row.State}\n" +
                $"{Strings.DetailsSource}: {row.SourceName}\n" +
                $"{Strings.DetailsFirstSeen}: {record.FirstSeenUtc.ToLocalTime():O}\n" +
                $"{Strings.DetailsLastSeen}: {record.LastSeenUtc.ToLocalTime():O}",
        };

        await dialog.ShowDialogAsync();
    }

    private static string OriginName(DomainOrigin origin) => origin switch
    {
        DomainOrigin.Dns => Strings.OriginDns,
        DomainOrigin.SharedCdn => Strings.OriginSharedCdn,
        DomainOrigin.Ptr => Strings.OriginPtr,
        DomainOrigin.None => Strings.OriginNone,
        _ => Strings.OriginUnknown,
    };

    private void Copy(Func<ConnectionRowViewModel, string> selector)
    {
        if (ConnectionsGrid.SelectedItem is not ConnectionRowViewModel row)
        {
            return;
        }

        try
        {
            Clipboard.SetText(selector(row));
        }
        catch (ExternalException)
        {
            // Another process owns the clipboard; nothing was copied.
        }
    }
}
