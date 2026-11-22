using System.Windows.Controls;
using NetWatcher.Models;
using NetWatcher.Services.Localization;
using NetWatcher.Services.Theming;

namespace NetWatcher.Views;

/// <summary>
/// Appearance and language. The theme applies immediately; the language change
/// raises the controller event that makes the shell reload its localized XAML.
/// </summary>
public partial class SettingsPage : UserControl
{
    private readonly ThemeController _theme;
    private readonly LanguageController _language;
    private bool _syncing;

    public SettingsPage(ThemeController theme, LanguageController language)
    {
        InitializeComponent();

        _theme = theme;
        _language = language;

        var options = language.Options;
        LanguageBox.ItemsSource = options;
        SyncControls(options);
    }

    private void SyncControls(IReadOnlyList<LanguageOption> options)
    {
        _syncing = true;
        ThemeBox.SelectedItem = ThemeBox.Items
            .OfType<ComboBoxItem>()
            .First(item => item.Tag is AppTheme theme && theme == _theme.Preference);
        LanguageBox.SelectedItem = options.First(o => o.Language == _language.Preference);
        _syncing = false;
    }

    private void Theme_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || ThemeBox.SelectedItem is not ComboBoxItem { Tag: AppTheme preference })
        {
            return;
        }

        _theme.SetPreference(preference);
    }

    private void Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || LanguageBox.SelectedItem is not LanguageOption option)
        {
            return;
        }

        _language.SetPreference(option.Language);
    }
}
