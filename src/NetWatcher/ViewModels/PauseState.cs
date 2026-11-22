using System.ComponentModel;

namespace NetWatcher.ViewModels;

/// <summary>
/// The session-wide pause flag. Both pages show and toggle the same instance,
/// so freezing the view is one concept rather than one per page.
/// </summary>
public sealed class PauseState : INotifyPropertyChanged
{
    private bool _isPaused;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsPaused => _isPaused;

    public void Toggle()
    {
        _isPaused = !_isPaused;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPaused)));
    }
}
