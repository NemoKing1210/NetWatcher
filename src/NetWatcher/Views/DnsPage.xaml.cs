using System.Windows;
using System.Windows.Controls;
using NetWatcher.ViewModels;

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
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e) =>
        _viewModel.FilterText = FilterBox.Text;

    private void Pause_Click(object sender, RoutedEventArgs e) => _viewModel.TogglePause();
}
