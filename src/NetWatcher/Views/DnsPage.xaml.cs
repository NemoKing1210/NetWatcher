using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using NetWatcher.Localization;
using NetWatcher.ViewModels;
using Wpf.Ui.Controls;

namespace NetWatcher.Views;

/// <summary>
/// The DNS events table: newest answers first, filtered by text, frozen by the
/// same pause state the Connections page uses.
/// </summary>
public partial class DnsPage : UserControl
{
    private readonly DnsPageViewModel _viewModel;

    public DnsPage(DnsPageViewModel viewModel)
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
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Detach();

    /// <summary>Stops observing the page's view model; the page may outlive the window during a language rebuild.</summary>
    internal void Detach() => _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DnsPageViewModel.IsPaused))
        {
            UpdatePauseVisual();
        }
    }

    private void UpdatePauseVisual()
    {
        var paused = _viewModel.IsPaused;
        var label = paused ? Strings.Resume : Strings.Pause;
        PauseText.Text = label;
        AutomationProperties.SetName(PauseButton, label);
        PauseIcon.Symbol = paused ? SymbolRegular.Play24 : SymbolRegular.Pause24;
        PauseButton.Appearance = paused ? ControlAppearance.Primary : ControlAppearance.Secondary;
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e) =>
        _viewModel.FilterText = FilterBox.Text;

    private void Pause_Click(object sender, RoutedEventArgs e) => _viewModel.TogglePause();
}
