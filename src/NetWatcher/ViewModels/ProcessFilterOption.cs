using System.ComponentModel;
using NetWatcher.Localization;

namespace NetWatcher.ViewModels;

/// <summary>
/// One choice in the Connections page's process filter: the "All processes"
/// entry or a live process. Options are stable per PID, so a selection survives
/// the 1 Hz refresh while its process stays visible.
/// </summary>
public sealed class ProcessFilterOption
{
    /// <summary>The unfiltered choice; always the first option in the combo box.</summary>
    public static readonly ProcessFilterOption All = new();

    public ProcessFilterOption(ProcessItemViewModel process)
    {
        Process = process;
        Icon.SetPath(process.Path);
        process.PropertyChanged += OnProcessChanged;
    }

    private ProcessFilterOption()
    {
    }

    /// <summary>The live process, or null for the "All processes" option.</summary>
    public ProcessItemViewModel? Process { get; }

    public bool IsAll => Process is null;

    /// <summary>The executable's icon; empty for "All processes".</summary>
    public ProcessIcon Icon { get; } = new();

    public string Name => Process?.Name ?? Strings.AllProcesses;

    public string Display => Process?.Display ?? Strings.AllProcesses;

    /// <summary>Readable name for UI Automation, which uses ToString for combo items.</summary>
    public override string ToString() => Display;

    private void OnProcessChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProcessItemViewModel.Path))
        {
            Icon.SetPath(Process?.Path);
        }
    }
}
