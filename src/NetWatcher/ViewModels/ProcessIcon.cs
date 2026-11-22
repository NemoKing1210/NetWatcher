using System.ComponentModel;
using System.Windows.Media;
using NetWatcher.Services.ProcessIcons;

namespace NetWatcher.ViewModels;

/// <summary>
/// Lazily loads one executable's shell icon and raises <see cref="PropertyChanged"/>
/// when it arrives. The extraction runs off the UI thread; images are frozen and
/// cached by <see cref="ProcessIconProvider"/>.
/// </summary>
public sealed class ProcessIcon : INotifyPropertyChanged
{
    private string? _path;
    private bool _requested;
    private string? _requestedPath;
    private ImageSource? _image;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The icon, or null while it is loading or unavailable.</summary>
    public ImageSource? Image
    {
        get
        {
            if (_requested && string.Equals(_requestedPath, _path, StringComparison.OrdinalIgnoreCase))
            {
                return _image;
            }

            _requested = true;
            _requestedPath = _path;
            _image = null;

            if (_path is not null)
            {
                _ = LoadAsync(_path);
            }

            return null;
        }
    }

    /// <summary>Points the icon at another executable; re-loads on the next read.</summary>
    public void SetPath(string? path)
    {
        if (string.Equals(_path, path, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _path = path;
        _requested = false;
        _requestedPath = null;
        _image = null;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Image)));
    }

    private async Task LoadAsync(string path)
    {
        var icon = await Task.Run(() => ProcessIconProvider.GetIcon(path));

        if (!string.Equals(_requestedPath, path, StringComparison.OrdinalIgnoreCase) || icon is null)
        {
            return;
        }

        _image = icon;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Image)));
    }
}
