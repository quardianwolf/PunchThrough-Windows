using System.Net.NetworkInformation;
using System.Windows.Threading;
using PunchThrough.Models;

namespace PunchThrough.Services;

public class BypassService
{
    private static readonly Lazy<BypassService> _instance = new(() => new BypassService());
    public static BypassService Instance => _instance.Value;

    private const int MaxEngineRestarts = 3;

    private readonly ZapretService _zapret = new();
    private readonly TunnelService _tunnel = new();

    private AppState? _appState;
    private DispatcherTimer? _watchdog;
    private bool _watchdogBusy;
    private int _engineRestarts;
    private CancellationTokenSource? _networkDebounce;

    private BypassService()
    {
        _tunnel.Log += (message, level) => _appState?.AddLog(message, level);
        _tunnel.Lost += reason => OnUi(() =>
        {
            if (_appState == null) return;
            _appState.TunnelActive = false;
            _appState.TunnelStatusText = Strings.Format("TunnelStatusFailed", reason);
            _appState.AddLog(Strings.Format("TunnelLogLost", reason), LogLevel.Error);
        });
    }

    public async Task ToggleAsync(AppState appState)
    {
        switch (appState.ConnectionStatus.State)
        {
            case ConnectionState.Connected:
                await DisconnectAsync(appState);
                break;
            case ConnectionState.Disconnected:
            case ConnectionState.Error:
                await ConnectAsync(appState);
                break;
        }
    }

    public async Task ConnectAsync(AppState appState)
    {
        if (!appState.ConnectionStatus.CanToggle) return;

        _appState = appState;
        appState.ConnectionStatus = ConnectionStatus.Connecting;
        appState.AddLog(Strings.Format("LogStarting", appState.ProxyMode.GetDisplayName()));

        try
        {
            await SetupDnsAsync(appState);

            await Task.Run(() => _zapret.Start(
                appState.ProxyMode, appState.CustomProxyDomains.ToArray(), appState.ZapretStrategy, appState.EnableQuic));

            _engineRestarts = 0;
            appState.ConnectionStatus = ConnectionStatus.Connected;
            appState.AddLog(Strings.Format("LogActive", appState.ProxyMode.GetDisplayName()));
            StartMonitoring();
        }
        catch (Exception ex)
        {
            await Task.Run(StopEverything);
            appState.ConnectionStatus = ConnectionStatus.CreateError(ex.Message);
            appState.AddLog(Strings.Format("LogConnectFailed", ex.Message), LogLevel.Error);
            return;
        }

        // The tunnel is an extra: its failure is reported but does not take the bypass down
        await StartTunnelAsync(appState);
    }

    public async Task DisconnectAsync(AppState appState)
    {
        if (!appState.ConnectionStatus.CanToggle) return;

        appState.ConnectionStatus = ConnectionStatus.Disconnecting;
        appState.AddLog(Strings.Get("LogStopping"));

        StopMonitoring();
        await Task.Run(StopEverything);

        appState.TunnelActive = false;
        appState.TunnelStatusText = "";
        appState.ConnectionStatus = ConnectionStatus.Disconnected;
        appState.AddLog(Strings.Get("LogStopped"));
    }

    public async Task ReconnectAsync(AppState appState)
    {
        if (appState.ConnectionStatus.State != ConnectionState.Connected) return;
        await DisconnectAsync(appState);
        await ConnectAsync(appState);
    }

    /// <summary>Apply changed tunnel settings without restarting the DPI bypass.</summary>
    public async Task RestartTunnelAsync(AppState appState)
    {
        if (appState.ConnectionStatus.State != ConnectionState.Connected) return;
        await StartTunnelAsync(appState);
    }

    /// <summary>
    /// Blocking teardown for application exit. Must not await anything that resumes on the
    /// UI thread — the UI thread is the one waiting here.
    /// </summary>
    public void ShutdownSync()
    {
        try { StopMonitoring(); } catch { }
        StopEverything();
    }

    public bool IsRunning() => _zapret.IsRunning;

    private void StopEverything()
    {
        try { _tunnel.Stop(); } catch { }
        try { _zapret.Stop(); } catch { }
        try { DnsService.Cleanup(); } catch { }
    }

    // --- DNS ---

    private static async Task SetupDnsAsync(AppState appState)
    {
        appState.AddLog(Strings.Get("LogDnsSetup"));
        try
        {
            var profile = appState.DnsProfile;
            var result = await Task.Run(() => DnsService.Apply(profile, appState.EnableDoH));

            if (appState.DnsServer == DnsServer.Custom && !DnsServerExtensions.IsValidCustom(appState.CustomDns))
                appState.AddLog(Strings.Get("LogDnsCustomInvalid"), LogLevel.Warning);

            appState.AddLog(Strings.Format("LogDnsReady", profile.Primary, result.Interfaces));

            if (result.DohRequested && !result.DohActive)
                appState.AddLog(Strings.Get("LogDohUnavailable"), LogLevel.Warning);
        }
        catch (Exception ex)
        {
            // Poisoned DNS is only half of the blocking; the packet-level bypass is still worth starting
            appState.AddLog(Strings.Format("LogDnsFailed", ex.Message), LogLevel.Warning);
        }
    }

    // --- Tunnel ---

    private async Task StartTunnelAsync(AppState appState)
    {
        if (appState.TunnelMode == TunnelMode.Off)
        {
            await Task.Run(_tunnel.Stop);
            appState.TunnelActive = false;
            appState.TunnelStatusText = "";
            return;
        }

        appState.TunnelActive = false;
        appState.TunnelStatusText = Strings.Get("TunnelStatusConnecting");
        appState.AddLog(Strings.Format("TunnelLogStarting", appState.TunnelHost));

        try
        {
            var config = TunnelConfig.From(appState);
            await Task.Run(() => _tunnel.StartAsync(config));

            var where = "";
            try
            {
                var (ip, country) = await _tunnel.QueryExitAsync();
                where = $" — {ip} ({country})";
            }
            catch { }

            appState.TunnelActive = true;
            appState.TunnelStatusText = appState.TunnelMode.GetDisplayName() + where;
            appState.AddLog(Strings.Format("TunnelLogActive", appState.TunnelMode.GetDisplayName() + where));
        }
        catch (Exception ex)
        {
            appState.TunnelActive = false;
            appState.TunnelStatusText = Strings.Format("TunnelStatusFailed", ex.Message);
            appState.AddLog(Strings.Format("TunnelLogFailed", ex.Message), LogLevel.Error);
        }
    }

    // --- Monitoring ---

    private void StartMonitoring()
    {
        StopMonitoring();

        _watchdog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _watchdog.Tick += OnWatchdogTick;
        _watchdog.Start();

        NetworkChange.NetworkAddressChanged += OnNetworkChanged;
    }

    private void StopMonitoring()
    {
        NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
        _networkDebounce?.Cancel();

        if (_watchdog == null) return;
        _watchdog.Stop();
        _watchdog.Tick -= OnWatchdogTick;
        _watchdog = null;
    }

    /// <summary>"Connected" has to stay true: bring a dead winws back, or say that it is gone.</summary>
    private async void OnWatchdogTick(object? sender, EventArgs e)
    {
        var appState = _appState;
        if (_watchdogBusy || appState == null) return;
        if (appState.ConnectionStatus.State != ConnectionState.Connected || _zapret.IsRunning) return;

        _watchdogBusy = true;
        try
        {
            if (_engineRestarts >= MaxEngineRestarts)
                throw new InvalidOperationException(Strings.Get("ErrEngineKeepsStopping"));

            _engineRestarts++;
            appState.AddLog(Strings.Format("LogEngineRestart", _engineRestarts, MaxEngineRestarts), LogLevel.Warning);

            await Task.Run(() => _zapret.Start(
                appState.ProxyMode, appState.CustomProxyDomains.ToArray(), appState.ZapretStrategy, appState.EnableQuic));
        }
        catch (Exception ex)
        {
            if (appState.ConnectionStatus.State == ConnectionState.Connected)
            {
                StopMonitoring();
                await Task.Run(StopEverything);
                appState.TunnelActive = false;
                appState.TunnelStatusText = "";
                appState.ConnectionStatus = ConnectionStatus.CreateError(ex.Message);
                appState.AddLog(Strings.Format("LogConnectFailed", ex.Message), LogLevel.Error);
            }
        }
        finally
        {
            _watchdogBusy = false;
        }
    }

    /// <summary>A newly connected adapter (Wi-Fi ↔ Ethernet) still has the ISP's DNS — fix it.</summary>
    private void OnNetworkChanged(object? sender, EventArgs e)
    {
        _networkDebounce?.Cancel();
        var cts = _networkDebounce = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(3000, cts.Token);
                var added = DnsService.ApplyToNewInterfaces();
                if (added > 0)
                    _appState?.AddLog(Strings.Format("LogDnsNewAdapter", added));
            }
            catch { }
        });
    }

    private static void OnUi(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }
}
