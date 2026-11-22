using System.Globalization;
using System.Threading;
using System.Windows;
using Microsoft.Win32;
using NetWatcher.Logging;
using NetWatcher.Models;
using NetWatcher.Services.Connections;
using NetWatcher.Services.Correlation;
using NetWatcher.Services.Etw;
using NetWatcher.Services.Localization;
using NetWatcher.Services.ReverseDns;
using NetWatcher.Services.Storage;
using NetWatcher.Services.Theming;
using NetWatcher.Storage;
using NetWatcher.Utils;
using NetWatcher.ViewModels;
using Wpf.Ui.Appearance;

namespace NetWatcher;

public partial class App : Application
{
    private FileLogger? _logger;
    private EtwSessionService? _etw;
    private Aggregator? _aggregator;
    private Timer? _pollTimer;
    private MainViewModel? _viewModel;
    private DnsPageViewModel? _dnsViewModel;
    private SettingsStore? _settings;
    private ThemeController? _theme;
    private LanguageController? _language;
    private MainWindow? _mainWindow;
    private UserPreferenceChangedEventHandler? _preferenceChanged;
    private volatile bool _etwFailed;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _logger = new FileLogger();

        if (e.Args.Contains("--probe"))
        {
            var exitCode = EtwProbe.Run(durationSeconds: 60, _logger);
            Shutdown(exitCode);
            return;
        }

        _settings = new SettingsStore();
        _language = new LanguageController(_settings);
        _language.Initialize();
        _language.Changed += OnLanguageChanged;

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            _logger?.Error("Unhandled exception: " + args.ExceptionObject);
        DispatcherUnhandledException += (_, args) =>
            _logger?.Error("Dispatcher exception: " + args.Exception);

        var store = new NetworkStore();
        var resolver = new ProcessResolver();
        var pause = new PauseState();

        _aggregator = new Aggregator(store, resolver, _logger);
        _aggregator.Start();

        _etw = new EtwSessionService(resolver, _logger);
        _etw.ConnectionReceived += _aggregator.EnqueueConnection;
        _etw.DnsReceived += _aggregator.EnqueueDns;
        _etw.UnknownEvent += message => _logger?.Info("ETW unknown event: " + message);
        _etw.ParseFailed += message =>
        {
            _logger?.Error("ETW parse failure: " + message);
            _viewModel?.CountError();
        };
        _etw.Failed += message =>
        {
            _logger?.Error("ETW failed: " + message);
            _etwFailed = true;
            Dispatcher.InvokeAsync(() => _viewModel?.SetEtwAvailable(false));
        };

        var etwAvailable = _etw.Start();
        if (!etwAvailable)
        {
            _logger.Warn("ETW unavailable; running in table-polling mode");
        }

        var tableReader = new ConnectionTableReader(resolver);
        _pollTimer = new Timer(
            _ =>
            {
                try
                {
                    foreach (var record in tableReader.ReadTcpSnapshot(DateTime.UtcNow))
                    {
                        store.UpsertConnection(record);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Error("Table poll failed: " + ex.Message);
                    _viewModel?.CountError();
                }
            },
            state: null,
            dueTime: TimeSpan.FromSeconds(2),
            period: TimeSpan.FromSeconds(2));

        _logger.Info(string.Create(
            CultureInfo.InvariantCulture,
            $"Started. ETW available: {etwAvailable}, admin: {IsAdministrator()}"));

        _viewModel = new MainViewModel(
            store,
            new DomainCorrelator(store),
            new ReverseDnsResolver(),
            resolver,
            _logger,
            etwAvailable,
            IsAdministrator(),
            pause);

        if (_etwFailed)
        {
            _viewModel.SetEtwAvailable(false);
        }

        var dnsViewModel = new DnsPageViewModel(store, resolver, pause);
        _dnsViewModel = dnsViewModel;

        _theme = new ThemeController(new WpfUiThemeApplier(), _settings, GetSystemTheme);
        _theme.Initialize();

        _preferenceChanged = (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General)
            {
                Dispatcher.InvokeAsync(() => _theme?.OnSystemThemeChanged(GetSystemTheme()));
            }
        };
        SystemEvents.UserPreferenceChanged += _preferenceChanged;

        _mainWindow = new MainWindow(_viewModel, dnsViewModel, _theme, _language, _settings);
        MainWindow = _mainWindow;
        _mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_preferenceChanged is not null)
        {
            SystemEvents.UserPreferenceChanged -= _preferenceChanged;
        }

        if (_language is not null)
        {
            _language.Changed -= OnLanguageChanged;
        }

        _pollTimer?.Dispose();
        _etw?.Dispose();
        _aggregator?.Dispose();
        _logger?.Info("Shutdown complete");
        base.OnExit(e);
    }

    private void OnLanguageChanged(object? sender, EventArgs e) =>
        Dispatcher.InvokeAsync(RebuildWindowForLanguage);

    /// <summary>
    /// Localized XAML is resolved when a control is loaded, so a language change
    /// swaps the shell window while the collectors and view models keep running.
    /// </summary>
    private void RebuildWindowForLanguage()
    {
        if (_mainWindow is null ||
            _viewModel is null ||
            _dnsViewModel is null ||
            _theme is null ||
            _language is null ||
            _settings is null)
        {
            return;
        }

        var previous = _mainWindow;
        previous.PersistWindowBounds();

        var window = new MainWindow(_viewModel, _dnsViewModel, _theme, _language, _settings);
        _mainWindow = window;
        MainWindow = window;
        window.Show();
        window.ShowSettings();
        previous.Close();
    }

    private static bool IsAdministrator()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    /// <summary>The Windows theme, mapped to the two states the app distinguishes.</summary>
    private static AppTheme GetSystemTheme() =>
        ApplicationThemeManager.GetSystemTheme() == SystemTheme.Dark ? AppTheme.Dark : AppTheme.Light;
}
