using System.Windows;
using NetWatcher.Logging;
using NetWatcher.Services.Etw;

namespace NetWatcher;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Contains("--probe"))
        {
            var exitCode = EtwProbe.Run(durationSeconds: 60, new FileLogger());
            Shutdown(exitCode);
            return;
        }

        new MainWindow().Show();
    }
}
