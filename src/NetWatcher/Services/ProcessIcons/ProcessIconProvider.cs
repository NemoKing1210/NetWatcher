using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NetWatcher.Utils;

namespace NetWatcher.Services.ProcessIcons;

/// <summary>
/// Extracts the shell icon for an executable and caches it per path. Shell calls
/// are made off the UI thread by <see cref="ViewModels.ProcessFilterOption"/>;
/// the returned images are frozen so any thread can use them.
/// </summary>
public static class ProcessIconProvider
{
    private static readonly ConcurrentDictionary<string, ImageSource> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns the executable's large icon, or null when it has none.</summary>
    public static ImageSource? GetIcon(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (Cache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        var icon = Load(path);
        if (icon is not null)
        {
            Cache[path] = icon;
        }

        return icon;
    }

    private static ImageSource? Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var info = new NativeMethods.SHFILEINFO();
            var result = NativeMethods.SHGetFileInfo(
                path,
                0,
                ref info,
                (uint)Marshal.SizeOf<NativeMethods.SHFILEINFO>(),
                NativeMethods.ShgfiIcon | NativeMethods.ShgfiLargeIcon);

            if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                var source = Imaging.CreateBitmapSourceFromHIcon(
                    info.hIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally
            {
                NativeMethods.DestroyIcon(info.hIcon);
            }
        }
        catch (Exception)
        {
            return null;
        }
    }
}
