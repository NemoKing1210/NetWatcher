namespace NetWatcher.Services.Theming;

/// <summary>Applies an effective theme; the only UI-library dependency of theming.</summary>
public interface IThemeApplier
{
    void Apply(bool dark);
}
