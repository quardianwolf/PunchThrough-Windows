using System.Diagnostics;
using System.Windows;
using PunchThrough.Models;
using PunchThrough.Services;
using PunchThrough.Views;

namespace PunchThrough;

public partial class App : Application
{
    private TrayIcon? _trayIcon;
    private AppState? _appState;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // If not running from installed location, show setup
        if (!InstallService.IsInstalled())
        {
            // If already installed elsewhere, just launch from there
            if (InstallService.InstallExists())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = InstallService.InstalledExePath,
                    UseShellExecute = true
                });
                Shutdown();
                return;
            }

            // First time — show setup window
            var setup = new SetupWindow();
            setup.Show();
            return;
        }

        // Normal startup — running from installed location
        _appState = new AppState();
        _appState.LoadSettings();
        _appState.AppLanguage.Apply();
        _trayIcon = new TrayIcon(_appState);
        _trayIcon.Show();

        if (_appState.AutoConnect)
        {
            await Task.Delay(2000);
            await BypassService.Instance.ConnectAsync(_appState);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Kill SpoofDPI and clean up proxy synchronously — async deadlocks during shutdown
        try
        {
            foreach (var proc in System.Diagnostics.Process.GetProcessesByName("spoofdpi"))
            {
                try { proc.Kill(); } catch { }
            }
        }
        catch { }

        ProxyService.SetProxy(false, 0);

        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
