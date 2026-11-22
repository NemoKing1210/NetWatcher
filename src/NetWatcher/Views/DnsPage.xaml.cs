using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
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
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        UpdatePauseVisual();
    }

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
        PauseButton.Content = paused ? "Resume" : "Pause";
        PauseButton.Icon = new SymbolIcon { Symbol = paused ? SymbolRegular.Play24 : SymbolRegular.Pause24 };
        PauseButton.Appearance = paused ? ControlAppearance.Primary : ControlAppearance.Secondary;
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e) =>
        _viewModel.FilterText = FilterBox.Text;

    private void Pause_Click(object sender, RoutedEventArgs e) => _viewModel.TogglePause();
}
