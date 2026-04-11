using System.Diagnostics;
using System.Threading;
using System.Windows;
using PunchThrough.Models;
using PunchThrough.Services;
using PunchThrough.Views;

namespace PunchThrough;

public partial class App : Application
{
    private TrayIcon? _trayIcon;
    private AppState? _appState;
    private Mutex? _mutex;
    private bool _ownsMutex;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Single instance check — if another instance is running, quit silently
        _mutex = new Mutex(true, "PunchThrough_SingleInstance_B7E8C3A1", out _ownsMutex);
        if (!_ownsMutex)
        {
            _mutex.Dispose();
            _mutex = null;
            Shutdown();
            return;
        }

        // If not running from installed location, show setup
        if (!InstallService.IsInstalled())
        {
            // Always show setup — overwrites old files with new version
            var setup = new SetupWindow();
            setup.Show();
            return;
        }

        // Normal startup
        _appState = new AppState();
        _appState.LoadSettings();
        _appState.AppLanguage.Apply();

        // Sync startup registry with saved settings — some machines lose the entry
        StartupService.SetEnabled(_appState.LaunchAtStartup);

        _trayIcon = new TrayIcon(_appState);
        _trayIcon.Show();

        if (_appState.AutoConnect)
        {
            await Task.Delay(3000); // longer delay for slow machines at boot
            await BypassService.Instance.ConnectAsync(_appState);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_appState != null)
        {
            try { BypassService.Instance.DisconnectAsync(_appState).Wait(3000); } catch { }
            DnsService.Cleanup(); // Safety net
        }

        _trayIcon?.Dispose();
        if (_ownsMutex)
        {
            try { _mutex?.ReleaseMutex(); } catch { }
        }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
