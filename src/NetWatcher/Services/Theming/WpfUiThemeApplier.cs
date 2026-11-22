using Wpf.Ui.Appearance;

namespace NetWatcher.Services.Theming;

/// <summary>The single place that talks to WPF-UI's theme manager.</summary>
public sealed class WpfUiThemeApplier : IThemeApplier
{
    public void Apply(bool dark) =>
        ApplicationThemeManager.Apply(dark ? ApplicationTheme.Dark : ApplicationTheme.Light);
}
