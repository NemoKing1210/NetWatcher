using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NetWatcher.Services.Theming;
using NetWatcher.Utils;
using NetWatcher.ViewModels;
using NetWatcher.Views;
using Wpf.Ui.Controls;

namespace NetWatcher;

/// <summary>
/// The Fluent shell: a title bar, a navigation pane that never collapses, and
/// one content host that swaps between two cached pages.
/// </summary>
public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _connections;
    private readonly DnsPageViewModel _dns;
    private readonly ThemeController _theme;
    private readonly SettingsStore _settingsStore = new();
    private readonly DispatcherTimer _timer;
    private readonly ConnectionsPage _connectionsPage;
    private readonly FrameworkElement _dnsPage;

    private FrameworkElement? _currentPage;

    public MainWindow(MainViewModel connections, DnsPageViewModel dns, ThemeController theme)
    {
        InitializeComponent();

        _connections = connections;
        _dns = dns;
        _theme = theme;

        DataContext = connections;

        _connectionsPage = new ConnectionsPage(connections);
        _dnsPage = new DnsPage(dns);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            _connections.Tick();
            _dns.Tick();
        };

        Loaded += OnLoaded;
        Closing += OnClosing;
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

        ShowPage(_connectionsPage, ConnectionsItem);
        _connections.Tick();
        _dns.Tick();
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

    private void ShowPage(FrameworkElement page, NavigationViewItem activeItem)
    {
        if (!ReferenceEquals(_currentPage, page))
        {
            Nav.ReplaceContent(page);
            _currentPage = page;
        }

        ConnectionsItem.IsActive = ReferenceEquals(activeItem, ConnectionsItem);
        DnsItem.IsActive = ReferenceEquals(activeItem, DnsItem);
        AllProcessesItem.IsActive = ReferenceEquals(activeItem, AllProcessesItem);
    }

    private void Connections_Click(object sender, RoutedEventArgs e) =>
        ShowPage(_connectionsPage, ConnectionsItem);

    private void Dns_Click(object sender, RoutedEventArgs e) => ShowPage(_dnsPage, DnsItem);

    private void AllProcesses_Click(object sender, RoutedEventArgs e)
    {
        _connections.SelectedProcess = null;
        ProcessList.SelectedItem = null;
        ShowPage(_connectionsPage, AllProcessesItem);
    }

    private void Theme_Click(object sender, RoutedEventArgs e) => _theme.Toggle();

    private void ProcessList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProcessList.SelectedItem is ProcessItemViewModel process)
        {
            _connections.SelectedProcess = process;
            ShowPage(_connectionsPage, ConnectionsItem);
        }
    }
}
