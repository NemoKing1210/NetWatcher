using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using NetWatcher.Services.Localization;
using NetWatcher.Services.Theming;
using NetWatcher.Utils;
using NetWatcher.ViewModels;
using NetWatcher.Views;
using Wpf.Ui.Controls;

namespace NetWatcher;

/// <summary>
/// The Fluent shell: a title bar, a navigation pane that collapses to an icon
/// rail on narrow windows, and one content host that swaps between cached pages.
/// </summary>
public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _connections;
    private readonly DnsPageViewModel _dns;
    private readonly SettingsStore _settingsStore;
    private readonly DispatcherTimer _timer;
    private readonly ConnectionsPage _connectionsPage;
    private readonly DnsPage _dnsPage;
    private readonly SettingsPage _settingsPage;

    private FrameworkElement? _currentPage;
    private bool _paneIsCompact;

    /// <summary>Below this window width the 280 px pane leaves too little room for the tables.</summary>
    private const double CompactPaneBelow = 1220;

    public MainWindow(
        MainViewModel connections,
        DnsPageViewModel dns,
        ThemeController theme,
        LanguageController language,
        SettingsStore settingsStore)
    {
        InitializeComponent();

        _connections = connections;
        _dns = dns;
        _settingsStore = settingsStore;

        DataContext = connections;

        // Windows 10 has no Mica; keep the theme's solid window background there.
        if (!WindowBackdrop.IsSupported(WindowBackdropType.Mica))
        {
            SetCurrentValue(WindowBackdropTypeProperty, WindowBackdropType.None);
        }

        _connectionsPage = new ConnectionsPage(connections);
        _dnsPage = new DnsPage(dns);
        _settingsPage = new SettingsPage(theme, language);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            _connections.Tick();
            _dns.Tick();
        };

        Loaded += OnLoaded;
        Closing += OnClosing;
        SizeChanged += OnSizeChanged;
    }

    /// <summary>Opens the Settings page; used when a language change rebuilt the window.</summary>
    public void ShowSettings() => ShowPage(_settingsPage, SettingsItem);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var saved = _settingsStore.Load().Window;
        if (saved is not null)
        {
            Width = Math.Max(saved.Width, MinWidth);
            Height = Math.Max(saved.Height, MinHeight);
            Left = saved.X;
            Top = saved.Y;
        }

        ClampToMonitor();

        ShowPage(_connectionsPage, ConnectionsItem);
        _connections.Tick();
        _dns.Tick();
        _timer.Start();
    }

    /// <summary>
    /// The WPF window rectangle is physical here, so the clamp against the
    /// monitor's work area is independent of the DPI scale and monitor layout.
    /// </summary>
    private void ClampToMonitor()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero || !NativeMethods.GetWindowRect(handle, out var bounds))
        {
            return;
        }

        var monitor = NativeMethods.MonitorFromWindow(handle, NativeMethods.MonitorDefaultToNearest);
        var info = new NativeMethods.MONITORINFO
        {
            cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>(),
        };

        if (monitor == IntPtr.Zero || !NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            return;
        }

        var dpi = NativeMethods.GetDpiForWindow(handle);
        var scale = dpi == 0 ? 1d : dpi / 96d;

        var desired = new Rect(
            bounds.Left,
            bounds.Top,
            bounds.Right - bounds.Left,
            bounds.Bottom - bounds.Top);
        var workArea = new Rect(
            info.rcWork.Left,
            info.rcWork.Top,
            info.rcWork.Right - info.rcWork.Left,
            info.rcWork.Bottom - info.rcWork.Top);

        var clamped = WindowPlacement.Clamp(desired, workArea, MinWidth * scale, MinHeight * scale);
        if (clamped == desired)
        {
            return;
        }

        NativeMethods.SetWindowPos(
            handle,
            IntPtr.Zero,
            (int)Math.Round(clamped.Left),
            (int)Math.Round(clamped.Top),
            (int)Math.Round(clamped.Width),
            (int)Math.Round(clamped.Height),
            NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdatePaneMode(e.NewSize.Width);

    private void UpdatePaneMode(double width)
    {
        var compact = width < CompactPaneBelow;
        if (compact == _paneIsCompact)
        {
            return;
        }

        _paneIsCompact = compact;
        Nav.IsPaneOpen = !compact;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _timer.Stop();
        _connectionsPage.Detach();
        _dnsPage.Detach();
        PersistWindowBounds();
    }

    /// <summary>Saves the current geometry; the language rebuild calls this before the new window loads it.</summary>
    internal void PersistWindowBounds() =>
        _settingsStore.Save(_settingsStore.Load() with
        {
            Window = new WindowSettings(ActualWidth, ActualHeight, Left, Top),
        });

    private void ShowPage(FrameworkElement page, NavigationViewItem activeItem)
    {
        if (!ReferenceEquals(_currentPage, page))
        {
            Nav.ReplaceContent(page);
            _currentPage = page;
        }

        ConnectionsItem.IsActive = ReferenceEquals(activeItem, ConnectionsItem);
        DnsItem.IsActive = ReferenceEquals(activeItem, DnsItem);
        SettingsItem.IsActive = ReferenceEquals(activeItem, SettingsItem);
    }

    private void Connections_Click(object sender, RoutedEventArgs e) =>
        ShowPage(_connectionsPage, ConnectionsItem);

    private void Dns_Click(object sender, RoutedEventArgs e) => ShowPage(_dnsPage, DnsItem);

    private void Settings_Click(object sender, RoutedEventArgs e) => ShowPage(_settingsPage, SettingsItem);
}
